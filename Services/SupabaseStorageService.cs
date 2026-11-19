using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Supabase.Storage;
using Supabase.Storage.Interfaces;

namespace Task6.Services
{
    public class SupabaseStorageService : IStorageService
    {
        private readonly IStorageClient<Bucket, FileObject> _storage;
        private const string BucketName = "room-images";
        private readonly ILogger<SupabaseStorageService> _logger;

        public SupabaseStorageService(IConfiguration configuration, ILogger<SupabaseStorageService> logger)
            : this(CreateStorage(configuration), logger)
        {
        }

        internal SupabaseStorageService(IStorageClient<Bucket, FileObject> storage, ILogger<SupabaseStorageService> logger)
        {
            _storage = storage;
            _logger = logger;
        }

        private static IStorageClient<Bucket, FileObject> CreateStorage(IConfiguration configuration)
        {
            var supabaseUrl = configuration["Supabase:Url"]!;
            var serviceKey = configuration["Supabase:ServiceKey"]!;

            var supabase = new Supabase.Client(supabaseUrl, serviceKey);
            supabase.InitializeAsync().Wait();
            return supabase.Storage;
        }

        public async Task<string?> UploadImageAsync(Stream fileStream, string fileName, string contentType)
        {
            try
            {
                var bucket = _storage.From(BucketName);

                var uniqueName = $"{Guid.NewGuid()}_{fileName}";

                using var memoryStream = new MemoryStream();
                await fileStream.CopyToAsync(memoryStream);
                var bytes = memoryStream.ToArray();

                await bucket.Upload(bytes, uniqueName, new Supabase.Storage.FileOptions
                {
                    ContentType = contentType,
                    Upsert = true
                });

                var publicUrl = bucket.GetPublicUrl(uniqueName);

                _logger.LogInformation("Image uploaded successfully: {Url}", publicUrl);

                return publicUrl;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to upload image to Supabase Storage");
                return null;
            }
        }

        public async Task DeleteImageAsync(string imageUrl)
        {
            if (string.IsNullOrEmpty(imageUrl)) return;

            try
            {
                var bucket = _storage.From(BucketName);

                var fileName = imageUrl.Split($"/{BucketName}/").Last();

                await bucket.Remove(new List<string> { fileName });

                _logger.LogInformation("Image deleted successfully: {FileName}", fileName);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to delete image from Supabase Storage: {Url}", imageUrl);
            }
        }
    }
}
