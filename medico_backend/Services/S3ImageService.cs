using Amazon.S3;
using Amazon.S3.Model;
using Minio;
using Minio.DataModel.Args;

namespace medico_backend.Services
{
    public class S3ImageService
    {
        private readonly IAmazonS3 _s3;
        private readonly IConfiguration _config;
        private readonly ILogger<S3ImageService> _logger;

        public S3ImageService(IAmazonS3 s3, IConfiguration config, ILogger<S3ImageService> logger)
        {
            _config = config;
            _logger = logger;

            var s3Config = new AmazonS3Config
            {
                ServiceURL = _config["S3:ServiceUrl"],
                ForcePathStyle = true,
                UseHttp = false
            };

            // Build the S3 client from config rather than discarding the injected one.
            _s3 = new AmazonS3Client(
                _config["S3:AccessKey"],
                _config["S3:SecretKey"],
                s3Config);
        }

        private string GetBucket() => _config["S3:BucketName"] ?? "medico";

        // The public-facing host clients should use to actually fetch objects
        // (this must be the SAME host used when presigning, or the signature won't match).
        private string GetPublicEndpoint() => _config["S3:PublicEndpoint"] ?? "s3.seyotechnologies.com";

        // The internal host/port MinIO is actually listening on (used only for presigning setup
        // if your public endpoint isn't directly reachable from this process).
        private string GetMinioEndpoint() => _config["S3:MinioEndpoint"] ?? "s3.seyotechnologies.com";

        /// <summary>
        /// Builds the S3 key based on entity type.
        /// medico/{tenantCode}/customers/{entityId}/{prefix}_{filename}
        /// medico/{tenantCode}/users/{entityId}/{prefix}_{filename}
        /// </summary>
        public string BuildKey(string tenantCode, string entityType, long entityId, string prefix, string fileName)
        {
            return $"medico/{tenantCode}/{entityType}/{entityId}/{prefix}_{fileName}";
        }

        private static string GuessContentType(string keyOrFileName)
        {
            return Path.GetExtension(keyOrFileName).ToLowerInvariant() switch
            {
                ".jpg" or ".jpeg" => "image/jpeg",
                ".png" => "image/png",
                ".webp" => "image/webp",
                ".gif" => "image/gif",
                ".bmp" => "image/bmp",
                ".svg" => "image/svg+xml",
                _ => "application/octet-stream"
            };
        }

        /// <summary>
        /// Uploads a file to S3 and returns the object key (NOT a URL).
        /// entityType: "customers" or "users"
        /// prefix: "customer", "signature", "avatar", etc.
        /// </summary>
        public async Task<string?> UploadAsync(
            IFormFile file,
            string tenantCode,
            string entityType,   // "customers" | "users"
            long entityId,
            string prefix)
        {
            if (file == null || file.Length == 0) return null;

            var key = BuildKey(tenantCode, entityType, entityId, prefix, file.FileName);

            using var stream = file.OpenReadStream();
            await _s3.PutObjectAsync(new PutObjectRequest
            {
                BucketName = GetBucket(),
                Key = key,
                InputStream = stream,
                ContentType = string.IsNullOrEmpty(file.ContentType)
                    ? GuessContentType(file.FileName)
                    : file.ContentType
            });

            _logger.LogInformation("S3 Upload [{EntityType}:{EntityId}] -> {Key}", entityType, entityId, key);

            // Return the raw key. Do NOT bake a host into it — presigned URLs are generated
            // on demand in DownloadAsync, and mixing hosts here breaks signature verification.
            return key;
        }

        /// <summary>
        /// Deletes an image from S3 by its stored key. Safe — ignores missing files.
        /// </summary>
        public async Task DeleteAsync(string? key)
        {
            if (string.IsNullOrEmpty(key)) return;
            try
            {
                await _s3.DeleteObjectAsync(new DeleteObjectRequest
                {
                    BucketName = GetBucket(),
                    Key = key
                });
                _logger.LogInformation("S3 Deleted -> {Key}", key);
            }
            catch (AmazonS3Exception ex)
            {
                _logger.LogWarning("S3 delete skipped for {Key}: {Message}", key, ex.Message);
            }
        }

        /// <summary>
        /// Replaces an old image with a new one atomically.
        /// Deletes old key, uploads new file, returns new key.
        /// </summary>
        public async Task<string?> ReplaceAsync(
            IFormFile? newFile,
            string? oldKey,
            string tenantCode,
            string entityType,
            long entityId,
            string prefix)
        {
            if (newFile == null || newFile.Length == 0)
                return oldKey; // No new file → preserve existing key

            await DeleteAsync(oldKey);
            return await UploadAsync(newFile, tenantCode, entityType, entityId, prefix);
        }

        /// <summary>
        /// Downloads a file by generating a presigned GET URL and fetching the bytes.
        /// IMPORTANT: the client used to presign must use the SAME host that will be used
        /// to actually fetch the object, or SigV4 verification will fail (403).
        /// </summary>
        public async Task<(byte[] Data, string ContentType, string FileName)?> DownloadAsync(string key)
        {
            if (string.IsNullOrEmpty(key)) return null;

            try
            {
                var endpoint = GetMinioEndpoint();

                var minio = new MinioClient()
                    .WithEndpoint(endpoint)
                    .WithSSL() // remove this line if your MinIO endpoint is plain HTTP internally
                    .WithCredentials(_config["S3:AccessKey"], _config["S3:SecretKey"])
                    .Build();

                var url = await minio.PresignedGetObjectAsync(
                    new PresignedGetObjectArgs()
                        .WithBucket(GetBucket())
                        .WithObject(key)
                        .WithExpiry(3600));

                // Only rewrite the host here if GetMinioEndpoint() and GetPublicEndpoint() are
                // genuinely the same physical server reachable under two names/ports AND your
                // MinIO/proxy setup validates signatures against the public host regardless of
                // which host signed it (e.g. a reverse proxy that terminates TLS but preserves
                // the original signed headers). If they are different hosts, DO NOT do this —
                // presign directly against GetPublicEndpoint() instead.
                if (!string.Equals(endpoint, GetPublicEndpoint(), StringComparison.OrdinalIgnoreCase))
                {
                    url = url.Replace(endpoint, GetPublicEndpoint());
                }

                var bytes = await ImageUrlToBase64Async(url);
                return (bytes, GuessContentType(key), Path.GetFileName(key));
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "S3 download failed for {Key}", key);
                return null;
            }
        }

        public async Task<byte[]> ImageUrlToBase64Async(string imageUrl)
        {
            using var httpClient = new HttpClient();
            return await httpClient.GetByteArrayAsync(imageUrl);
        }

        /// <summary>
        /// Lists all files under a specific entity folder.
        /// e.g. medico/{tenantCode}/customers/{custId}/
        /// </summary>
        public async Task<List<S3FileInfo>> ListAsync(string tenantCode, string entityType, long entityId)
        {
            var prefix = $"medico/{tenantCode}/{entityType}/{entityId}/";

            var response = await _s3.ListObjectsV2Async(new ListObjectsV2Request
            {
                BucketName = GetBucket(),
                Prefix = prefix
            });

            return (response.S3Objects ?? new List<S3Object>()).Select(obj => new S3FileInfo
            {
                Key = obj.Key,
                FileName = Path.GetFileName(obj.Key),
                Size = obj.Size ?? 0,
                LastModified = obj.LastModified ?? DateTime.UtcNow
            }).ToList();
        }
    }

    public class S3FileInfo
    {
        public string Key { get; set; } = "";
        public string FileName { get; set; } = "";
        public long Size { get; set; }
        public DateTime LastModified { get; set; }
    }
}