using Microsoft.Extensions.Logging;
using Moq;
using Supabase.Storage;
using Supabase.Storage.Interfaces;
using Task6.Services;
using Task6.Tests.Helpers;
using StorageFileOptions = Supabase.Storage.FileOptions;

namespace Task6.Tests.Services
{
    public class SupabaseStorageServiceTests
    {
        private const string BucketName = "room-images";
        private const string PublicRoot = "https://storage.example.test/room-images/";

        private readonly Mock<IStorageClient<Bucket, FileObject>> _storage = new();
        private readonly Mock<IStorageFileApi<FileObject>> _bucket = new();
        private readonly SpyLogger<SupabaseStorageService> _logger = new((_, _) => false);
        private readonly SupabaseStorageService _service;

        public SupabaseStorageServiceTests()
        {
            _storage.Setup(c => c.From(BucketName)).Returns(_bucket.Object);
            _service = new SupabaseStorageService(_storage.Object, _logger);
        }

        [Fact]
        public async Task UploadImageAsync_IspravnaSlika_VracaJavniUrl()
        {
            byte[] podaci = [1, 2, 3];
            using var stream = new MemoryStream(podaci);
            _bucket.Setup(b => b.GetPublicUrl(It.IsAny<string>(), null))
                .Returns((string putanja, TransformOptions? _) => PublicRoot + putanja);

            var url = await _service.UploadImageAsync(stream, "soba.png", "image/png");

            Assert.NotNull(url);
            Assert.StartsWith(PublicRoot, url);
            Assert.EndsWith("_soba.png", url);
            var putanja = url[PublicRoot.Length..];
            Assert.True(Guid.TryParse(putanja[..^"_soba.png".Length], out _));
            _bucket.Verify(b => b.Upload(
                podaci, putanja, It.Is<StorageFileOptions>(o => o.ContentType == "image/png" && o.Upsert),
                It.IsAny<EventHandler<float>>(), It.IsAny<bool>()), Times.Once);
        }

        [Fact]
        public async Task UploadImageAsync_GreskaUSpremistu_VracaNullIZapisujeGresku()
        {
            using var stream = new MemoryStream([1, 2, 3]);
            _bucket.Setup(b => b.Upload(It.IsAny<byte[]>(), It.IsAny<string>(), It.IsAny<StorageFileOptions>(),
                    It.IsAny<EventHandler<float>>(), It.IsAny<bool>()))
                .ThrowsAsync(new InvalidOperationException("Spremište nedostupno."));

            Assert.Null(await _service.UploadImageAsync(stream, "soba.png", "image/png"));

            Assert.Contains(_logger.Entries, e => e.Level == LogLevel.Error);
        }

        [Fact]
        public async Task DeleteImageAsync_JavniUrl_BriseRelativnuPutanju()
        {
            await _service.DeleteImageAsync(PublicRoot + "folder/soba.png");

            _bucket.Verify(b => b.Remove(It.Is<List<string>>(p => p.SequenceEqual(new[] { "folder/soba.png" }))), Times.Once);
        }

        [Fact]
        public async Task DeleteImageAsync_PrazanUrl_NePozivaSpremiste()
        {
            await _service.DeleteImageAsync("");

            _storage.Verify(c => c.From(It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task DeleteImageAsync_GreskaUSpremistu_ZapisujeUpozorenje()
        {
            _bucket.Setup(b => b.Remove(It.IsAny<List<string>>()))
                .ThrowsAsync(new InvalidOperationException("Spremište nedostupno."));

            await _service.DeleteImageAsync(PublicRoot + "soba.png");

            Assert.Contains(_logger.Entries, e => e.Level == LogLevel.Warning);
        }
    }
}
