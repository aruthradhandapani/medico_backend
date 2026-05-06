using Amazon.S3;
using Amazon.S3.Model;

namespace medico_backend.Services
{
    public class S3PrescriptionService
    {
        private readonly IAmazonS3 _s3;
        private readonly ILogger<S3PrescriptionService> _logger;

        public S3PrescriptionService(IAmazonS3 s3, ILogger<S3PrescriptionService> logger)
        {
            _s3 = s3;
            _logger = logger;
        }

        /// <summary>
        /// Uploads to custom bucket.
        /// Key: {clientFolder}/{fileName}_{yyyyMMddHHmmss}{ext}
        /// Creates folder if not exists.
        /// </summary>
        public async Task<string?> UploadAsync(
            IFormFile file,
            string bucketName,
            string clientFolder,
            string fileName)
        {
            if (file == null || file.Length == 0) return null;

            var ext = Path.GetExtension(file.FileName);
            var baseName = Path.GetFileNameWithoutExtension(fileName);
            var stamp = DateTime.UtcNow.ToString("yyyyMMddHHmmss");
            var finalKey = $"{clientFolder.TrimEnd('/')}/{baseName}_{stamp}{ext}";

            // Create virtual folder if it doesn't exist
            var listResponse = await _s3.ListObjectsV2Async(new ListObjectsV2Request
            {
                BucketName = bucketName,
                Prefix = $"{clientFolder.TrimEnd('/')}/",
                MaxKeys = 1
            });

            if (listResponse.KeyCount == 0)
            {
                await _s3.PutObjectAsync(new PutObjectRequest
                {
                    BucketName = bucketName,
                    Key = $"{clientFolder.TrimEnd('/')}/",
                    InputStream = new MemoryStream(),
                    ContentType = "application/x-directory"
                });
                _logger.LogInformation("Prescription folder created → [{Bucket}] {Folder}/", bucketName, clientFolder);
            }

            using var stream = file.OpenReadStream();
            await _s3.PutObjectAsync(new PutObjectRequest
            {
                BucketName = bucketName,
                Key = finalKey,
                InputStream = stream,
                ContentType = file.ContentType
            });

            _logger.LogInformation("Prescription Upload [{Bucket}] → {Key}", bucketName, finalKey);
            return finalKey;
        }

        /// <summary>
        /// Downloads from a specific bucket and key.
        /// </summary>
        public async Task<(byte[] Data, string ContentType, string FileName)?> DownloadAsync(
            string bucketName,
            string key)
        {
            try
            {
                using var response = await _s3.GetObjectAsync(bucketName, key);
                using var ms = new MemoryStream();
                await response.ResponseStream.CopyToAsync(ms);
                return (ms.ToArray(), response.Headers.ContentType, Path.GetFileName(key));
            }
            catch (AmazonS3Exception ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                _logger.LogWarning("Prescription file not found → Bucket: {Bucket}, Key: {Key}", bucketName, key);
                return null;
            }
        }

        /// <summary>
        /// Deletes from a specific bucket and key.
        /// </summary>
        public async Task DeleteAsync(string bucketName, string? key)
        {
            if (string.IsNullOrEmpty(key)) return;
            try
            {
                await _s3.DeleteObjectAsync(new DeleteObjectRequest
                {
                    BucketName = bucketName,
                    Key = key
                });
                _logger.LogInformation("Prescription Deleted → Bucket: {Bucket}, Key: {Key}", bucketName, key);
            }
            catch (AmazonS3Exception ex)
            {
                _logger.LogWarning("Prescription delete skipped → Bucket: {Bucket}, Key: {Key} — {Message}", bucketName, key, ex.Message);
            }
        }

        /// <summary>
        /// Lists all files under a client folder in a specific bucket.
        /// </summary>
        public async Task<List<PrescriptionFileInfo>> ListAsync(string bucketName, string clientFolder)
        {
            var response = await _s3.ListObjectsV2Async(new ListObjectsV2Request
            {
                BucketName = bucketName,
                Prefix = $"{clientFolder.TrimEnd('/')}/"
            });

            return (response.S3Objects ?? [])
                .Where(obj => !obj.Key.EndsWith("/"))   // exclude folder markers
                .Select(obj => new PrescriptionFileInfo
                {
                    Key = obj.Key,
                    FileName = Path.GetFileName(obj.Key),
                    Size = obj.Size ?? 0,
                    LastModified = obj.LastModified ?? DateTime.UtcNow
                }).ToList();
        }

        /// <summary>
        /// Strips bucket prefix from stored full path to get the S3 key.
        /// e.g. "cms/folder/file.jpg" → "folder/file.jpg"
        /// </summary>
        public static string ExtractKey(string fullPath, string bucketName)
            => fullPath.StartsWith(bucketName + "/")
                ? fullPath[(bucketName.Length + 1)..]
                : fullPath;
    }

    public class PrescriptionFileInfo
    {
        public string Key { get; set; } = "";
        public string FileName { get; set; } = "";
        public long Size { get; set; }
        public DateTime LastModified { get; set; }
    }
}