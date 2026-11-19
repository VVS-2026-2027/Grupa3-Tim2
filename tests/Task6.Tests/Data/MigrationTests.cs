using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Task6.Data;
using Task6.Migrations;
using Task6.Models;

namespace Task6.Tests.Data
{
    public class MigrationTests
    {
        private static EscapeRoomDbContext CreateContext()
        {
            var options = new DbContextOptionsBuilder<EscapeRoomDbContext>()
                .UseNpgsql("Host=localhost;Database=unit_tests;Username=unit_tests")
                .Options;
            return new EscapeRoomDbContext(options);
        }

        [Fact]
        public void GenerateScript_PraznaBaza_KreiraTabeleIDodajeKolone()
        {
            using var db = CreateContext();

            var sql = db.GetService<IMigrator>().GenerateScript();

            Assert.Contains("CREATE TABLE \"EscapeRooms\"", sql);
            Assert.Contains("CREATE TABLE \"Rezervacije\"", sql);
            Assert.Contains("ALTER TABLE \"Podrske\" ADD \"Odgovor\"", sql);
            Assert.Contains("ALTER TABLE \"EscapeRooms\" ADD \"ImageUrl\"", sql);
        }

        [Fact]
        public void GenerateScript_PovratakNaPraznuBazu_UklanjaKoloneITabele()
        {
            using var db = CreateContext();

            var sql = db.GetService<IMigrator>().GenerateScript(
                fromMigration: "20260609105757_DodajImageUrl", toMigration: Migration.InitialDatabase);

            Assert.Contains("ALTER TABLE \"EscapeRooms\" DROP COLUMN \"ImageUrl\"", sql);
            Assert.Contains("ALTER TABLE \"Podrske\" DROP COLUMN \"Odgovor\"", sql);
            Assert.Contains("DROP TABLE \"Rezervacije\"", sql);
            Assert.Contains("DROP TABLE \"EscapeRooms\"", sql);
        }

        [Fact]
        public void AddDiscountFields_DodajeIspustaKoloneZaPopust()
        {
            var migration = new AddDiscountFields();
            string[] kolone = [nameof(EscapeRoom.DiscountEnd), nameof(EscapeRoom.DiscountPercent), nameof(EscapeRoom.DiscountStart)];

            var dodaneKolone = migration.UpOperations.OfType<AddColumnOperation>().OrderBy(o => o.Name).ToArray();
            var uklonjeneKolone = migration.DownOperations.OfType<DropColumnOperation>().OrderBy(o => o.Name).ToArray();

            Assert.Equal(kolone, dodaneKolone.Select(o => o.Name));
            Assert.All(dodaneKolone, o => Assert.Equal("EscapeRooms", o.Table));
            Assert.Equal(kolone, uklonjeneKolone.Select(o => o.Name));
            Assert.All(uklonjeneKolone, o => Assert.Equal("EscapeRooms", o.Table));
        }

        [Fact]
        public void Snapshot_Soba_ImaSlikuIPoljaZaPopust()
        {
            using var db = CreateContext();

            var model = db.GetService<IMigrationsAssembly>().ModelSnapshot!.Model;
            var soba = model.FindEntityType(typeof(EscapeRoom).FullName!)!;

            Assert.NotNull(soba.FindProperty(nameof(EscapeRoom.ImageUrl)));
            Assert.NotNull(soba.FindProperty(nameof(EscapeRoom.DiscountPercent)));
            Assert.NotNull(soba.FindProperty(nameof(EscapeRoom.DiscountStart)));
            Assert.NotNull(soba.FindProperty(nameof(EscapeRoom.DiscountEnd)));
        }
    }
}
