using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Task6.Data;
using Task6.Services;
using Task6.Tests.Helpers;

namespace Task6.Tests.Services
{
    public class DiscountCleanupServiceTests
    {
        // Zamjenski IServiceProvider: umjesto pravog DI kontejnera vraća InMemory bazu.
        private static IServiceProvider ProviderSa(EscapeRoomDbContext db)
        {
            var services = new ServiceCollection();
            services.AddSingleton(db);
            return services.BuildServiceProvider();
        }

        [Fact]
        public async Task CleanupExpiredDiscountsAsync_IstekaoPopust_ResetujePopustSobe()
        {
            using var db = TestDb.Create();
            var soba = TestDb.AddRoom(db);
            soba.DiscountPercent = 20;
            soba.DiscountStart = DateTime.UtcNow.AddDays(-5);
            soba.DiscountEnd = DateTime.UtcNow.AddDays(-1);
            db.SaveChanges();
            var service = new DiscountCleanupService(ProviderSa(db), NullLogger<DiscountCleanupService>.Instance);

            await service.CleanupExpiredDiscountsAsync(CancellationToken.None);

            Assert.Equal(0, soba.DiscountPercent);
            Assert.Null(soba.DiscountStart);
            Assert.Null(soba.DiscountEnd);
        }

        [Fact]
        public async Task CleanupExpiredDiscountsAsync_AktivanPopust_OstajeNepromijenjen()
        {
            using var db = TestDb.Create();
            var soba = TestDb.AddRoom(db);
            soba.DiscountPercent = 20;
            soba.DiscountStart = DateTime.UtcNow.AddDays(-1);
            soba.DiscountEnd = DateTime.UtcNow.AddDays(3);
            db.SaveChanges();
            var service = new DiscountCleanupService(ProviderSa(db), NullLogger<DiscountCleanupService>.Instance);

            await service.CleanupExpiredDiscountsAsync(CancellationToken.None);

            Assert.Equal(20, soba.DiscountPercent);
            Assert.NotNull(soba.DiscountEnd);
        }

        [Fact]
        public async Task ExecuteAsync_PokretanjeServisa_IzvrsavaCiscenjeOdmah()
        {
            using var db = TestDb.Create();
            var logger = new SpyLogger<DiscountCleanupService>((level, message) => message == "No expired discounts found.");
            var service = new DiscountCleanupService(ProviderSa(db), logger);

            await service.StartAsync(CancellationToken.None);
            await logger.WaitForSignalAsync();
            await service.StopAsync(CancellationToken.None);

            Assert.Contains(logger.Entries, e => e.Message == "Discount cleanup service started.");
            Assert.Contains(logger.Entries, e => e.Message == "No expired discounts found.");
        }

        [Fact]
        public async Task ExecuteAsync_BazaNijeDostupna_ZapisujeGreskuINastavljaRad()
        {
            var provider = new Mock<IServiceProvider>();
            provider.Setup(p => p.GetService(typeof(IServiceScopeFactory)))
                .Throws(new InvalidOperationException("Baza nije dostupna"));
            var logger = new SpyLogger<DiscountCleanupService>((level, message) => level == LogLevel.Error);
            var service = new DiscountCleanupService(provider.Object, logger);

            await service.StartAsync(CancellationToken.None);
            await logger.WaitForSignalAsync();
            await service.StopAsync(CancellationToken.None);

            Assert.Contains(logger.Entries, e => e.Level == LogLevel.Error);
        }

        [Fact]
        public async Task ExecuteAsync_OtkazanoCiscenje_ZavrsavaBezGreske()
        {
            var provider = new Mock<IServiceProvider>();
            provider.Setup(p => p.GetService(typeof(IServiceScopeFactory)))
                .Throws(new OperationCanceledException());
            var logger = new SpyLogger<DiscountCleanupService>((_, message) => message == "Discount cleanup service stopped.");
            using var service = new DiscountCleanupService(provider.Object, logger);

            await service.StartAsync(CancellationToken.None);
            await logger.WaitForSignalAsync();
            await service.StopAsync(CancellationToken.None);

            Assert.DoesNotContain(logger.Entries, e => e.Level == LogLevel.Error);
            provider.Verify(p => p.GetService(typeof(IServiceScopeFactory)), Times.Once);
        }
    }

    public class TerminCleanupServiceTests
    {
        private static IServiceProvider ProviderSa(EscapeRoomDbContext db)
        {
            var services = new ServiceCollection();
            services.AddSingleton(db);
            return services.BuildServiceProvider();
        }

        [Fact]
        public async Task CleanupExpiredTerminiAsync_ProsliDostupniTermin_BriseTermin()
        {
            using var db = TestDb.Create();
            var soba = TestDb.AddRoom(db);
            TestDb.AddTermin(db, soba, DateTime.Now.AddDays(-1), "10:00");
            var service = new TerminCleanupService(ProviderSa(db), NullLogger<TerminCleanupService>.Instance);

            await service.CleanupExpiredTerminiAsync(CancellationToken.None);

            Assert.Empty(db.Termini);
        }

        [Fact]
        public async Task CleanupExpiredTerminiAsync_BuduciTermin_OstajeUBazi()
        {
            using var db = TestDb.Create();
            var soba = TestDb.AddRoom(db);
            TestDb.AddTermin(db, soba, DateTime.Now.AddDays(2), "10:00");
            var service = new TerminCleanupService(ProviderSa(db), NullLogger<TerminCleanupService>.Instance);

            await service.CleanupExpiredTerminiAsync(CancellationToken.None);

            Assert.Single(db.Termini);
        }

        [Fact]
        public async Task CleanupExpiredTerminiAsync_ZauzetProsliTermin_NeBriseSe()
        {
            using var db = TestDb.Create();
            var soba = TestDb.AddRoom(db);
            TestDb.AddTermin(db, soba, DateTime.Now.AddDays(-1), "10:00", dostupnost: false);
            var service = new TerminCleanupService(ProviderSa(db), NullLogger<TerminCleanupService>.Instance);

            await service.CleanupExpiredTerminiAsync(CancellationToken.None);

            Assert.Single(db.Termini);
        }

        [Fact]
        public async Task CleanupExpiredTerminiAsync_NeispravnoVrijeme_KoristiSamoDatum()
        {
            using var db = TestDb.Create();
            var soba = TestDb.AddRoom(db);
            TestDb.AddTermin(db, soba, DateTime.Now.AddDays(-1), "nije-vrijeme");
            var service = new TerminCleanupService(ProviderSa(db), NullLogger<TerminCleanupService>.Instance);

            await service.CleanupExpiredTerminiAsync(CancellationToken.None);

            Assert.Empty(db.Termini);
        }

        [Fact]
        public async Task ExecuteAsync_PokretanjeServisa_IzvrsavaCiscenjeOdmah()
        {
            using var db = TestDb.Create();
            var logger = new SpyLogger<TerminCleanupService>((level, message) => message == "No expired termini found.");
            var service = new TerminCleanupService(ProviderSa(db), logger);

            await service.StartAsync(CancellationToken.None);
            await logger.WaitForSignalAsync();
            await service.StopAsync(CancellationToken.None);

            Assert.Contains(logger.Entries, e => e.Message == "Termin cleanup service started.");
            Assert.Contains(logger.Entries, e => e.Message == "No expired termini found.");
        }

        [Fact]
        public async Task ExecuteAsync_BazaNijeDostupna_ZapisujeGreskuINastavljaRad()
        {
            var provider = new Mock<IServiceProvider>();
            provider.Setup(p => p.GetService(typeof(IServiceScopeFactory)))
                .Throws(new InvalidOperationException("Baza nije dostupna"));
            var logger = new SpyLogger<TerminCleanupService>((level, message) => level == LogLevel.Error);
            var service = new TerminCleanupService(provider.Object, logger);

            await service.StartAsync(CancellationToken.None);
            await logger.WaitForSignalAsync();
            await service.StopAsync(CancellationToken.None);

            Assert.Contains(logger.Entries, e => e.Level == LogLevel.Error);
        }

        [Fact]
        public async Task ExecuteAsync_OtkazanoCiscenje_ZavrsavaBezGreske()
        {
            var provider = new Mock<IServiceProvider>();
            provider.Setup(p => p.GetService(typeof(IServiceScopeFactory)))
                .Throws(new OperationCanceledException());
            var logger = new SpyLogger<TerminCleanupService>((_, message) => message == "Termin cleanup service stopped.");
            using var service = new TerminCleanupService(provider.Object, logger);

            await service.StartAsync(CancellationToken.None);
            await logger.WaitForSignalAsync();
            await service.StopAsync(CancellationToken.None);

            Assert.DoesNotContain(logger.Entries, e => e.Level == LogLevel.Error);
            provider.Verify(p => p.GetService(typeof(IServiceScopeFactory)), Times.Once);
        }
    }
}
