using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Task6.Controllers;
using Task6.Data;
using Task6.Models;
using Task6.Tests.Helpers;

namespace Task6.Tests.Controllers
{
    public class RoomControllerTests : IDisposable
    {
        private readonly EscapeRoomDbContext _db;
        private readonly RoomController _controller;

        public RoomControllerTests()
        {
            _db = TestDb.Create();
            DodajSobu("Zatvor Alcatraz", "Bijeg iz zatvora", Tezina.Teska, 6);
            DodajSobu("Laboratorija", "Horor u laboratoriji", Tezina.Srednja, 4);
            DodajSobu("Ukleta kuca", "Horor prica", Tezina.Teska, 4);
            DodajSobu("Piramida", "Egipatska misterija", Tezina.Lagana, 8);
            _controller = new RoomController(_db);
        }

        public void Dispose() => _db.Dispose();

        private EscapeRoom DodajSobu(string naziv, string opis, Tezina tezina, int kapacitet)
        {
            var soba = new EscapeRoom { Naziv = naziv, Opis = opis, Tezina = tezina, Kapacitet = kapacitet, Cijena = 40 };
            _db.EscapeRooms.Add(soba);
            _db.SaveChanges();
            return soba;
        }

        public static IEnumerable<object[]> FilteriIzJsona() =>
            TestData.FromJson<FilterSlucaj>("filteri_soba.json").Select(s => new object[] { s });

        [Theory]
        [MemberData(nameof(FilteriIzJsona))]
        public async Task Index_FilteriIzJsona_VracaOcekivaniBrojSoba(FilterSlucaj slucaj)
        {
            var result = await _controller.Index(slucaj.SearchTerm, slucaj.FilterTezina, slucaj.BrojIgraca, null);

            var model = Assert.IsAssignableFrom<List<EscapeRoom>>(Assert.IsType<ViewResult>(result).Model);
            Assert.Equal(slucaj.OcekivaniBroj, model.Count);
        }

        [Fact]
        public async Task Index_ZeljeniDatum_VracaSamoSobeSaSlobodnimTerminomTogDana()
        {
            var datum = new DateTime(2030, 5, 10);
            var soba = _db.EscapeRooms.First(s => s.Naziv == "Piramida");
            TestDb.AddTermin(_db, soba, DateTime.SpecifyKind(datum.AddHours(18), DateTimeKind.Utc));
            var zauzeta = _db.EscapeRooms.First(s => s.Naziv == "Laboratorija");
            TestDb.AddTermin(_db, zauzeta, DateTime.SpecifyKind(datum.AddHours(18), DateTimeKind.Utc), dostupnost: false);

            var result = await _controller.Index(null, null, null, datum);

            var view = Assert.IsType<ViewResult>(result);
            var model = Assert.IsAssignableFrom<List<EscapeRoom>>(view.Model);
            Assert.Equal("Piramida", Assert.Single(model).Naziv);
            Assert.Equal("2030-05-10", view.ViewData["ZeljeniDatum"]);
        }

        [Fact]
        public async Task Details_SobaNePostoji_VracaNotFound()
        {
            var result = await _controller.Details(999);

            Assert.IsType<NotFoundResult>(result);
        }

        [Fact]
        public async Task Details_SobaBezRecenzija_ProsjekJeNula()
        {
            var soba = _db.EscapeRooms.First();

            var result = await _controller.Details(soba.RoomID);

            var view = Assert.IsType<ViewResult>(result);
            Assert.Equal(0.0, view.ViewData["AverageRating"]);
            Assert.Equal(0, view.ViewData["ReviewCount"]);
        }

        [Fact]
        public async Task Details_SobaSaRecenzijamaITerminima_RacunaProsjekIVracaSlobodneTermine()
        {
            var soba = _db.EscapeRooms.First();
            var user = TestDb.AddUser(_db, "k@test.com");
            _db.Recenzije.AddRange(
                new Recenzija { KorisnikID = user.Id, RoomID = soba.RoomID, Ocjena = 5, Komentar = "A" },
                new Recenzija { KorisnikID = user.Id, RoomID = soba.RoomID, Ocjena = 2, Komentar = "B" });
            _db.SaveChanges();
            TestDb.AddTermin(_db, soba, DateTime.UtcNow.AddDays(1));
            TestDb.AddTermin(_db, soba, DateTime.UtcNow.AddDays(2), dostupnost: false);

            var result = await _controller.Details(soba.RoomID);

            var view = Assert.IsType<ViewResult>(result);
            Assert.Equal(3.5, view.ViewData["AverageRating"]);
            Assert.Equal(2, view.ViewData["ReviewCount"]);
            Assert.Single(Assert.IsAssignableFrom<List<Termin>>(view.ViewData["Termini"]));
        }
    }

    public class EscapeRoomsControllerTests : IDisposable
    {
        private readonly EscapeRoomDbContext _db;
        private readonly EscapeRoomsController _controller;
        private readonly EscapeRoom _soba;

        public EscapeRoomsControllerTests()
        {
            _db = TestDb.Create();
            _soba = TestDb.AddRoom(_db);
            _controller = new EscapeRoomsController(_db);
        }

        public void Dispose() => _db.Dispose();

        [Fact]
        public async Task Details_SobaNePostoji_VracaNotFound()
        {
            var result = await _controller.Details(999);

            Assert.IsType<NotFoundResult>(result);
        }

        [Fact]
        public async Task Details_SobaBezRecenzija_ProsjekINulaRecenzija()
        {
            var result = await _controller.Details(_soba.RoomID);

            var view = Assert.IsType<ViewResult>(result);
            Assert.Equal(0.0, view.ViewData["AverageRating"]);
            Assert.Equal(0, view.ViewData["ReviewCount"]);
        }

        [Fact]
        public async Task Details_RazniTermini_VracaSamoBuduceSlobodneTermine()
        {
            var sutra = DateTime.UtcNow.AddDays(1);
            TestDb.AddTermin(_db, _soba, sutra, "18:00");
            TestDb.AddTermin(_db, _soba, sutra, "20:00", dostupnost: false);
            TestDb.AddTermin(_db, _soba, DateTime.UtcNow.AddDays(-1), "18:00");

            var result = await _controller.Details(_soba.RoomID);

            var termini = Assert.IsAssignableFrom<List<Termin>>(Assert.IsType<ViewResult>(result).ViewData["Termini"]);
            Assert.Equal("18:00", Assert.Single(termini).Vrijeme);
        }

        [Fact]
        public async Task Details_DanasnjiTermini_VracaSamoOneKojiNisuProsli()
        {
            var danas = DateTime.UtcNow.Date;
            TestDb.AddTermin(_db, _soba, danas, "23:59");
            TestDb.AddTermin(_db, _soba, danas, "00:00");

            var result = await _controller.Details(_soba.RoomID);

            var termini = Assert.IsAssignableFrom<List<Termin>>(Assert.IsType<ViewResult>(result).ViewData["Termini"]);
            Assert.All(termini, t => Assert.Equal("23:59", t.Vrijeme));
        }

        [Fact]
        public async Task Details_SobaSaRecenzijama_RacunaProsjek()
        {
            var user = TestDb.AddUser(_db, "k@test.com");
            _db.Recenzije.AddRange(
                new Recenzija { KorisnikID = user.Id, RoomID = _soba.RoomID, Ocjena = 4, Komentar = "A" },
                new Recenzija { KorisnikID = user.Id, RoomID = _soba.RoomID, Ocjena = 5, Komentar = "B" });
            _db.SaveChanges();

            var result = await _controller.Details(_soba.RoomID);

            var view = Assert.IsType<ViewResult>(result);
            Assert.Equal(4.5, view.ViewData["AverageRating"]);
            Assert.Equal(2, view.ViewData["ReviewCount"]);
        }
    }

    public class HomeControllerTests : IDisposable
    {
        private readonly EscapeRoomDbContext _db;
        private readonly HomeController _controller;

        public HomeControllerTests()
        {
            _db = TestDb.Create();
            _controller = new HomeController(_db)
            {
                ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { TraceIdentifier = "trace-123" } }
            };
        }

        public void Dispose() => _db.Dispose();

        [Fact]
        public async Task Index_ViseOdPetSoba_VracaNajvisePetSoba()
        {
            for (var i = 1; i <= 7; i++)
                TestDb.AddRoom(_db, "Soba " + i);

            var result = await _controller.Index();

            Assert.Equal(5, Assert.IsAssignableFrom<List<EscapeRoom>>(Assert.IsType<ViewResult>(result).Model).Count);
        }

        [Fact]
        public void Privacy_PozivAkcije_VracaView()
        {
            var result = _controller.Privacy();

            Assert.IsType<ViewResult>(result);
        }

        [Fact]
        public void Error_BezAktivnosti_KoristiTraceIdentifier()
        {
            var result = _controller.Error();

            var model = Assert.IsType<ErrorViewModel>(Assert.IsType<ViewResult>(result).Model);
            Assert.Equal("trace-123", model.RequestId);
            Assert.True(model.ShowRequestId);
        }
    }
}
