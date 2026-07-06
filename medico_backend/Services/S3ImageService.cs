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
            _s3 = s3;
            _config = config;
            _logger = logger;

            var s3Config = new AmazonS3Config
            {
                ServiceURL = _config["S3:ServiceUrl"],
                ForcePathStyle = true,
                UseHttp = false
            };

            _s3 = new AmazonS3Client(
                _config["S3:AccessKey"],
                _config["S3:SecretKey"],
                s3Config);
        }

        private string GetBucket() => _config["S3:BucketName"] ?? "medico"; // was "labcare"

        /// <summary>
        /// Builds the S3 key based on entity type.
        /// LabCare/{tenantCode}/customers/{entityId}/{prefix}_{filename}
        /// LabCare/{tenantCode}/users/{entityId}/{prefix}_{filename}
        /// </summary>
        public string BuildKey(string tenantCode, string entityType, long entityId, string prefix, string fileName)
        {
            return $"medico/{tenantCode}/{entityType}/{entityId}/{prefix}_{fileName}";
        }

        /// <summary>
        /// Uploads a file to S3 and returns the object key.
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
                ContentType = file.ContentType
            });

            _logger.LogInformation("S3 Upload [{EntityType}:{EntityId}] → {Key}", entityType, entityId, key);
            key = key.Replace("http://127.0.0.1:9000", "https://s3.seyotechnologies.com");
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
                _logger.LogInformation("S3 Deleted → {Key}", key);
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
        /// Downloads a file from S3 and returns it as a byte array with metadata.
        /// </summary>
        public async Task<(byte[] Data, string ContentType, string FileName)?> DownloadAsync(string key)
        {
            //try
            //{

            //    var config = new AmazonS3Config
            //    {
            //        ServiceURL = _config["S3:ServiceUrl"],
            //        ForcePathStyle = true,
            //        UseHttp = false // because you're using HTTPS
            //    };

            //    _s3 = new AmazonS3Client(
            //        _config["S3:AccessKey"],
            //        _config["S3:SecretKey"],
            //        config)?? new AmazonS3Client();


            //    using var response = await _s3.GetObjectAsync(request);
            //    using var ms = new MemoryStream();
            //    await response.ResponseStream.CopyToAsync(ms);
            //    return (ms.ToArray(), response.Headers.ContentType, Path.GetFileName(key));
            //}
            try
            {
               
            }
            catch (AmazonS3Exception ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                return null;
            }

            try
            {
                var minio = new MinioClient()
     .WithEndpoint("127.0.0.1:9000")
     .WithCredentials("minioadmin", "minioadmin")
     .Build();

                var url = await minio.PresignedGetObjectAsync(
                    new PresignedGetObjectArgs()
                        .WithBucket("medico")
                        .WithObject($"{key}")
                        .WithExpiry(3600));

                url = url.Replace("http://127.0.0.1:9000", "https://s3.seyotechnologies.com");

                return (ImageUrlToBase64Async(url).Result,"image/png", Path.GetFileName(key));

            }
            catch (Exception ex)
            {
                Console.Write(ex.Message.ToString());
                return null;
            }
        }

        public async Task<byte[]> ImageUrlToBase64Async(string imageUrl)
        {
            using var httpClient = new HttpClient();

            byte[] imageBytes = await httpClient.GetByteArrayAsync(imageUrl);

            return imageBytes;
        }

        /// <summary>
        /// Lists all files under a specific entity folder.
        /// e.g. LabCare/{tenantCode}/customers/{custId}/
        /// </summary>
        public async Task<List<S3FileInfo>> ListAsync(string tenantCode, string entityType, long entityId)
        {
            var prefix = $"medico/{tenantCode}/{entityType}/{entityId}/";

            var response = await _s3.ListObjectsV2Async(new ListObjectsV2Request
            {
                BucketName = GetBucket(),
                Prefix = prefix
            });

            return (response.S3Objects ?? []).Select(obj => new S3FileInfo
            {
                Key = obj.Key,
                FileName = Path.GetFileName(obj.Key),
                Size = obj.Size ?? 0,                        // long? → long
                LastModified = obj.LastModified ?? DateTime.UtcNow   // DateTime? → DateTime
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