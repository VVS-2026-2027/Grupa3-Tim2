using Microsoft.AspNetCore.Mvc;
using Moq;
using Task6.Controllers;
using Task6.Data;
using Task6.Models;
using Task6.Services;
using Task6.Tests.Helpers;
using Task6.ViewModels;

namespace Task6.Tests.Controllers
{
    public class ZaposlenikControllerTests : IDisposable
    {
        private readonly EscapeRoomDbContext _db;
        private readonly Mock<IEmailService> _email;
        private readonly Mock<IStorageService> _storage;
        private readonly ZaposlenikController _controller;
        private readonly ApplicationUser _zaposlenik;

        public ZaposlenikControllerTests()
        {
            _db = TestDb.Create();
            _zaposlenik = TestDb.AddUser(_db, "zaposlenik@test.com", Uloga.Zaposlenik);
            _email = new Mock<IEmailService>();
            _storage = new Mock<IStorageService>();
            _controller = new ZaposlenikController(_db, _email.Object, _storage.Object);
            ControllerSetup.WithUser(_controller, _zaposlenik.Email);
        }

        public void Dispose() => _db.Dispose();

        private static EscapeRoom NovaSoba(int id = 0) => new() { RoomID = id, Naziv = "Nova soba", Opis = "Opis", Kapacitet = 5, Tezina = Tezina.Teska };

        public static IEnumerable<object[]> CijeneIzCsv() => TestData.FromCsv("cijene_soba.csv");

        // ---------- Kontrola pristupa ----------

        public static IEnumerable<object[]> AkcijeZaposlenika() => new[]
        {
            new object[] { (Func<ZaposlenikController, Task<IActionResult>>)(c => c.Dashboard()) },
            new object[] { (Func<ZaposlenikController, Task<IActionResult>>)(c => c.Rooms()) },
            new object[] { (Func<ZaposlenikController, Task<IActionResult>>)(c => c.CreateRoom()) },
            new object[] { (Func<ZaposlenikController, Task<IActionResult>>)(c => c.CreateRoom(NovaSoba(), null)) },
            new object[] { (Func<ZaposlenikController, Task<IActionResult>>)(c => c.EditRoom(1)) },
            new object[] { (Func<ZaposlenikController, Task<IActionResult>>)(c => c.EditRoom(NovaSoba(1), null)) },
            new object[] { (Func<ZaposlenikController, Task<IActionResult>>)(c => c.DeleteRoom(1)) },
            new object[] { (Func<ZaposlenikController, Task<IActionResult>>)(c => c.Termini()) },
            new object[] { (Func<ZaposlenikController, Task<IActionResult>>)(c => c.CreateTermin()) },
            new object[] { (Func<ZaposlenikController, Task<IActionResult>>)(c => c.CreateTermin(new Termin())) },
            new object[] { (Func<ZaposlenikController, Task<IActionResult>>)(c => c.EditTermin(1)) },
            new object[] { (Func<ZaposlenikController, Task<IActionResult>>)(c => c.EditTermin(new Termin())) },
            new object[] { (Func<ZaposlenikController, Task<IActionResult>>)(c => c.DeleteTermin(1)) },
            new object[] { (Func<ZaposlenikController, Task<IActionResult>>)(c => c.Notifications()) },
            new object[] { (Func<ZaposlenikController, Task<IActionResult>>)(c => c.CreateNotification()) },
            new object[] { (Func<ZaposlenikController, Task<IActionResult>>)(c => c.CreateNotification(new Obavijest())) },
            new object[] { (Func<ZaposlenikController, Task<IActionResult>>)(c => c.DeleteNotification(1)) },
            new object[] { (Func<ZaposlenikController, Task<IActionResult>>)(c => c.Reservations()) },
            new object[] { (Func<ZaposlenikController, Task<IActionResult>>)(c => c.Popusti()) },
            new object[] { (Func<ZaposlenikController, Task<IActionResult>>)(c => c.Popusti(new DiscountViewModel())) },
            new object[] { (Func<ZaposlenikController, Task<IActionResult>>)(c => c.ReservationDetails(1)) },
            new object[] { (Func<ZaposlenikController, Task<IActionResult>>)(c => c.Cancel(1)) },
            new object[] { (Func<ZaposlenikController, Task<IActionResult>>)(c => c.Reviews()) },
            new object[] { (Func<ZaposlenikController, Task<IActionResult>>)(c => c.DeleteReview(1)) },
            new object[] { (Func<ZaposlenikController, Task<IActionResult>>)(c => c.Support()) },
            new object[] { (Func<ZaposlenikController, Task<IActionResult>>)(c => c.SupportDetails(1)) },
            new object[] { (Func<ZaposlenikController, Task<IActionResult>>)(c => c.ReplySupport(1, "Odgovor")) },
            new object[] { (Func<ZaposlenikController, Task<IActionResult>>)(c => c.DeleteSupport(1)) }
        };

        [Theory]
        [MemberData(nameof(AkcijeZaposlenika))]
        public async Task SveAkcije_KorisnikNijeZaposlenik_PreusmjeravaNaPocetnu(Func<ZaposlenikController, Task<IActionResult>> akcija)
        {
            var admin = TestDb.AddUser(_db, "admin@test.com", Uloga.Admin);
            ControllerSetup.WithUser(_controller, admin.Email);

            var result = await akcija(_controller);

            var redirect = Assert.IsType<RedirectToActionResult>(result);
            Assert.Equal("Home", redirect.ControllerName);
        }

        // ---------- Dashboard i sobe ----------

        [Fact]
        public async Task Dashboard_PostojePodaci_VracaStatistiku()
        {
            TestDb.AddRoom(_db);

            var result = await _controller.Dashboard();

            var stats = Assert.IsType<ViewResult>(result).ViewData["Stats"]!;
            Assert.Equal(1, stats.GetType().GetProperty("TotalRooms")!.GetValue(stats));
            Assert.Equal(0, stats.GetType().GetProperty("TotalSupport")!.GetValue(stats));
        }

        [Fact]
        public async Task Rooms_PostojeSobe_VracaSve()
        {
            TestDb.AddRoom(_db);

            var result = await _controller.Rooms();

            Assert.Single(Assert.IsAssignableFrom<List<EscapeRoom>>(Assert.IsType<ViewResult>(result).Model));
        }

        [Fact]
        public async Task CreateRoomGet_Zaposlenik_VracaFormu()
        {
            Assert.IsType<ViewResult>(await _controller.CreateRoom());
        }

        [Theory]
        [MemberData(nameof(CijeneIzCsv))]
        public async Task CreateRoomPost_CijeneIzCsv_SpremaSamoValidneCijene(string unesenaCijena, string validna)
        {
            ControllerSetup.WithForm(_controller, new() { ["Cijena"] = unesenaCijena });

            await _controller.CreateRoom(NovaSoba(), null);

            Assert.Equal(bool.Parse(validna), _db.EscapeRooms.Any());
        }

        [Fact]
        public async Task CreateRoomPost_ValidnaSobaSaSlikom_UploadujeSlikuIObavjestavaKorisnike()
        {
            ControllerSetup.WithForm(_controller, new() { ["Cijena"] = "35" });
            _storage.Setup(s => s.UploadImageAsync(It.IsAny<Stream>(), "soba.png", "image/png")).ReturnsAsync("https://slike/soba.png");

            var result = await _controller.CreateRoom(NovaSoba(), ControllerSetup.CreateFile("soba.png", "image/png", 1024));

            Assert.Equal("Rooms", Assert.IsType<RedirectToActionResult>(result).ActionName);
            Assert.Equal("https://slike/soba.png", _db.EscapeRooms.Single().ImageUrl);
            _email.Verify(e => e.SendEmailAsync("zaposlenik@test.com", "The Last Key - Nova soba: Nova soba", It.IsAny<string>()), Times.Once);
        }

        [Theory]
        [InlineData("slika.gif", "image/gif", 1024)]
        [InlineData("slika.jpg", "image/jpg", 6 * 1024 * 1024)]
        public async Task CreateRoomPost_NeispravnaSlika_VracaFormuBezSpremanja(string ime, string tip, long velicina)
        {
            ControllerSetup.WithForm(_controller, new() { ["Cijena"] = "35" });

            var result = await _controller.CreateRoom(NovaSoba(), ControllerSetup.CreateFile(ime, tip, velicina));

            Assert.IsType<ViewResult>(result);
            Assert.Empty(_db.EscapeRooms);
        }

        [Fact]
        public async Task EditRoomGet_NePostoji_VracaNotFound()
        {
            Assert.IsType<NotFoundResult>(await _controller.EditRoom(999));
        }

        [Fact]
        public async Task EditRoomGet_Postoji_VracaSobu()
        {
            var soba = TestDb.AddRoom(_db);

            Assert.Same(soba, Assert.IsType<ViewResult>(await _controller.EditRoom(soba.RoomID)).Model);
        }

        [Theory]
        [InlineData("12,5")]
        [InlineData("١٢")]
        public async Task EditRoomPost_NeispravnaCijena_VracaFormuIZadrzavaCijenu(string cijena)
        {
            var soba = TestDb.AddRoom(_db, cijena: 25);
            _db.ChangeTracker.Clear();
            ControllerSetup.WithForm(_controller, new() { ["Cijena"] = cijena });

            var result = await _controller.EditRoom(NovaSoba(soba.RoomID), null);

            Assert.IsType<ViewResult>(result);
            Assert.False(_controller.ModelState.IsValid);
            Assert.Equal(25, _db.EscapeRooms.Single().Cijena);
        }

        [Fact]
        public async Task EditRoomPost_SobaNePostoji_VracaNotFound()
        {
            ControllerSetup.WithForm(_controller, new() { ["Cijena"] = "35" });

            Assert.IsType<NotFoundResult>(await _controller.EditRoom(NovaSoba(999), null));
        }

        [Fact]
        public async Task EditRoomPost_BezNoveSlike_ZadrzavaStaruSliku()
        {
            var soba = TestDb.AddRoom(_db);
            soba.ImageUrl = "https://slike/stara.jpg";
            _db.SaveChanges();
            _db.ChangeTracker.Clear();
            ControllerSetup.WithForm(_controller, new() { ["Cijena"] = "70" });

            await _controller.EditRoom(NovaSoba(soba.RoomID), null);

            Assert.Equal("https://slike/stara.jpg", _db.EscapeRooms.Single().ImageUrl);
        }

        [Fact]
        public async Task EditRoomPost_NovaSlika_BriseStaruIUploadujeNovu()
        {
            var soba = TestDb.AddRoom(_db);
            soba.ImageUrl = "https://slike/stara.jpg";
            _db.SaveChanges();
            _db.ChangeTracker.Clear();
            ControllerSetup.WithForm(_controller, new() { ["Cijena"] = "70" });
            _storage.Setup(s => s.UploadImageAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string>())).ReturnsAsync("https://slike/nova.jpg");

            await _controller.EditRoom(NovaSoba(soba.RoomID), ControllerSetup.CreateFile("nova.jpg", "image/jpeg", 1024));

            _storage.Verify(s => s.DeleteImageAsync("https://slike/stara.jpg"), Times.Once);
            Assert.Equal("https://slike/nova.jpg", _db.EscapeRooms.Single().ImageUrl);
        }

        [Fact]
        public async Task EditRoomPost_NovaSlikaBezStare_NeBriseNista()
        {
            var soba = TestDb.AddRoom(_db);
            _db.ChangeTracker.Clear();
            ControllerSetup.WithForm(_controller, new() { ["Cijena"] = "70" });

            await _controller.EditRoom(NovaSoba(soba.RoomID), ControllerSetup.CreateFile("nova.jpg", "image/jpeg", 1024));

            _storage.Verify(s => s.DeleteImageAsync(It.IsAny<string>()), Times.Never);
        }

        [Theory]
        [InlineData("slika.tiff", "image/tiff", 1024)]
        [InlineData("slika.png", "image/png", 6 * 1024 * 1024)]
        public async Task EditRoomPost_NeispravnaSlika_VracaFormu(string ime, string tip, long velicina)
        {
            var soba = TestDb.AddRoom(_db);
            _db.ChangeTracker.Clear();
            ControllerSetup.WithForm(_controller, new() { ["Cijena"] = "70" });

            Assert.IsType<ViewResult>(await _controller.EditRoom(NovaSoba(soba.RoomID), ControllerSetup.CreateFile(ime, tip, velicina)));
        }

        [Fact]
        public async Task DeleteRoom_NePostoji_VracaNotFound()
        {
            Assert.IsType<NotFoundResult>(await _controller.DeleteRoom(999));
        }

        [Fact]
        public async Task DeleteRoom_SobaSaSlikom_BriseSlikuISobu()
        {
            var soba = TestDb.AddRoom(_db);
            soba.ImageUrl = "https://slike/soba.jpg";
            _db.SaveChanges();

            await _controller.DeleteRoom(soba.RoomID);

            _storage.Verify(s => s.DeleteImageAsync("https://slike/soba.jpg"), Times.Once);
            Assert.Empty(_db.EscapeRooms);
        }

        [Fact]
        public async Task DeleteRoom_SobaBezSlike_NePozivaBrisanjeSlike()
        {
            var soba = TestDb.AddRoom(_db);

            await _controller.DeleteRoom(soba.RoomID);

            _storage.Verify(s => s.DeleteImageAsync(It.IsAny<string>()), Times.Never);
        }

        // ---------- Termini ----------

        [Fact]
        public async Task Termini_ViseTermina_VracaSortiranePoDatumu()
        {
            var soba = TestDb.AddRoom(_db);
            TestDb.AddTermin(_db, soba, DateTime.UtcNow.AddDays(5));
            TestDb.AddTermin(_db, soba, DateTime.UtcNow.AddDays(1));

            var result = await _controller.Termini();

            var model = Assert.IsAssignableFrom<List<Termin>>(Assert.IsType<ViewResult>(result).Model);
            Assert.True(model[0].Datum < model[1].Datum);
        }

        [Fact]
        public async Task CreateTerminGet_Zaposlenik_PopunjavaListuSoba()
        {
            TestDb.AddRoom(_db);

            Assert.Single(Assert.IsAssignableFrom<List<EscapeRoom>>(Assert.IsType<ViewResult>(await _controller.CreateTermin()).ViewData["Rooms"]));
        }

        [Fact]
        public async Task CreateTerminPost_TerminUProslosti_VracaFormu()
        {
            var soba = TestDb.AddRoom(_db);

            var result = await _controller.CreateTermin(new Termin { RoomID = soba.RoomID, Datum = DateTime.Now.AddDays(-1), Vrijeme = "09:00" });

            Assert.IsType<ViewResult>(result);
            Assert.Empty(_db.Termini);
        }

        [Fact]
        public async Task CreateTerminPost_BuduciTermin_SpremaTermin()
        {
            var soba = TestDb.AddRoom(_db);

            var result = await _controller.CreateTermin(new Termin { RoomID = soba.RoomID, Datum = DateTime.Now.AddDays(1), Vrijeme = "nije-vrijeme" });

            Assert.Equal("Termini", Assert.IsType<RedirectToActionResult>(result).ActionName);
            Assert.Single(_db.Termini);
        }

        [Fact]
        public async Task EditTerminGet_NePostoji_VracaNotFound()
        {
            Assert.IsType<NotFoundResult>(await _controller.EditTermin(999));
        }

        [Fact]
        public async Task EditTerminGet_Postoji_VracaTermin()
        {
            var termin = TestDb.AddTermin(_db, TestDb.AddRoom(_db), DateTime.UtcNow.AddDays(1));

            Assert.Same(termin, Assert.IsType<ViewResult>(await _controller.EditTermin(termin.TerminID)).Model);
        }

        [Fact]
        public async Task EditTerminPost_TerminUProslosti_VracaFormu()
        {
            var termin = TestDb.AddTermin(_db, TestDb.AddRoom(_db), DateTime.UtcNow.AddDays(1));
            _db.ChangeTracker.Clear();

            var result = await _controller.EditTermin(new Termin { TerminID = termin.TerminID, RoomID = termin.RoomID, Datum = DateTime.Now.AddDays(-1), Vrijeme = "10:00" });

            Assert.IsType<ViewResult>(result);
        }

        [Fact]
        public async Task EditTerminPost_BuduciTermin_AzuriraTermin()
        {
            var termin = TestDb.AddTermin(_db, TestDb.AddRoom(_db), DateTime.UtcNow.AddDays(1), "10:00");
            _db.ChangeTracker.Clear();

            await _controller.EditTermin(new Termin { TerminID = termin.TerminID, RoomID = termin.RoomID, Datum = DateTime.Now.AddDays(2), Vrijeme = "21:00" });

            Assert.Equal("21:00", _db.Termini.Single().Vrijeme);
        }

        [Fact]
        public async Task DeleteTermin_NePostoji_VracaNotFound()
        {
            Assert.IsType<NotFoundResult>(await _controller.DeleteTermin(999));
        }

        [Fact]
        public async Task DeleteTermin_Postoji_BriseTermin()
        {
            var termin = TestDb.AddTermin(_db, TestDb.AddRoom(_db), DateTime.UtcNow.AddDays(1));

            await _controller.DeleteTermin(termin.TerminID);

            Assert.Empty(_db.Termini);
        }

        // ---------- Obavijesti ----------

        [Fact]
        public async Task Notifications_ViseObavijesti_VracaNajnovijuPrvu()
        {
            _db.Obavijesti.AddRange(
                new Obavijest { Naslov = "Stara", Sadrzaj = "S", Datum = DateTime.UtcNow.AddDays(-2) },
                new Obavijest { Naslov = "Nova", Sadrzaj = "S", Datum = DateTime.UtcNow });
            _db.SaveChanges();

            var result = await _controller.Notifications();

            Assert.Equal("Nova", Assert.IsAssignableFrom<List<Obavijest>>(Assert.IsType<ViewResult>(result).Model)[0].Naslov);
        }

        [Fact]
        public async Task CreateNotificationGet_Zaposlenik_VracaFormu()
        {
            Assert.IsType<ViewResult>(await _controller.CreateNotification());
        }

        [Fact]
        public async Task CreateNotificationPost_NovaObavijest_SpremaBezSlanjaEmaila()
        {
            var result = await _controller.CreateNotification(new Obavijest { Naslov = "Novost", Sadrzaj = "Tekst", TipObavijesti = TipObavijesti.Novost });

            Assert.Equal("Notifications", Assert.IsType<RedirectToActionResult>(result).ActionName);
            Assert.Single(_db.Obavijesti);
            _email.Verify(e => e.SendEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task DeleteNotification_NePostoji_VracaNotFound()
        {
            Assert.IsType<NotFoundResult>(await _controller.DeleteNotification(999));
        }

        [Fact]
        public async Task DeleteNotification_Postoji_BriseObavijest()
        {
            var obavijest = new Obavijest { Naslov = "N", Sadrzaj = "S", Datum = DateTime.UtcNow };
            _db.Obavijesti.Add(obavijest);
            _db.SaveChanges();

            await _controller.DeleteNotification(obavijest.ObavijestID);

            Assert.Empty(_db.Obavijesti);
        }

        // ---------- Rezervacije ----------

        [Fact]
        public async Task Reservations_PostojeRezervacije_VracaSve()
        {
            TestDb.AddRezervacija(_db, _zaposlenik, TestDb.AddTermin(_db, TestDb.AddRoom(_db), DateTime.UtcNow.AddDays(1)));

            Assert.Single(Assert.IsAssignableFrom<List<Rezervacija>>(Assert.IsType<ViewResult>(await _controller.Reservations()).Model));
        }

        [Fact]
        public async Task ReservationDetails_NePostoji_VracaNotFound()
        {
            Assert.IsType<NotFoundResult>(await _controller.ReservationDetails(999));
        }

        [Fact]
        public async Task ReservationDetails_Postoji_VracaRezervaciju()
        {
            var rez = TestDb.AddRezervacija(_db, _zaposlenik, TestDb.AddTermin(_db, TestDb.AddRoom(_db), DateTime.UtcNow.AddDays(1)));

            Assert.Same(rez, Assert.IsType<ViewResult>(await _controller.ReservationDetails(rez.RezervacijaID)).Model);
        }

        [Fact]
        public async Task Cancel_NePostoji_VracaNotFound()
        {
            Assert.IsType<NotFoundResult>(await _controller.Cancel(999));
        }

        [Fact]
        public async Task Cancel_AktivnaRezervacija_BriseIObavjestavaKorisnika()
        {
            var korisnik = TestDb.AddUser(_db, "korisnik@test.com");
            var termin = TestDb.AddTermin(_db, TestDb.AddRoom(_db), DateTime.UtcNow.AddDays(1), dostupnost: false);
            var rez = TestDb.AddRezervacija(_db, korisnik, termin);

            await _controller.Cancel(rez.RezervacijaID);

            Assert.Empty(_db.Rezervacije);
            Assert.True(termin.Dostupnost);
            Assert.Equal("Rezervacija je uspješno otkazana i izbrisana.", _controller.TempData["CancelSuccess"]);
            _email.Verify(e => e.SendEmailAsync("korisnik@test.com", "The Last Key - Rezervacija otkazana", It.IsAny<string>()), Times.Once);
        }

        [Fact]
        public async Task Cancel_OtkazanaRezervacija_BriseBezEmaila()
        {
            var rez = TestDb.AddRezervacija(_db, TestDb.AddUser(_db, "k@test.com"), TestDb.AddTermin(_db, TestDb.AddRoom(_db), DateTime.UtcNow.AddDays(1)), status: false);

            await _controller.Cancel(rez.RezervacijaID);

            _email.Verify(e => e.SendEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        }

        // ---------- Popusti ----------

        [Fact]
        public async Task PopustiGet_PostojeSobe_PopunjavaListu()
        {
            TestDb.AddRoom(_db, "A");

            var model = Assert.IsType<DiscountViewModel>(Assert.IsType<ViewResult>(await _controller.Popusti()).Model);

            Assert.Single(model.Rooms);
        }

        [Theory]
        [InlineData(101, 1, 5, true)]
        [InlineData(10, 3, 3, true)]
        [InlineData(10, 1, 5, false)]
        public async Task PopustiPost_NeispravniPodaci_VracaFormu(int procenat, int pocetak, int kraj, bool odabranaSoba)
        {
            var soba = TestDb.AddRoom(_db);
            var danas = DateTime.UtcNow.Date;
            var model = new DiscountViewModel
            {
                DiscountPercent = procenat,
                StartDate = danas.AddDays(pocetak),
                EndDate = danas.AddDays(kraj),
                SelectedRoomIds = odabranaSoba ? new List<int> { soba.RoomID } : new List<int>()
            };

            Assert.IsType<ViewResult>(await _controller.Popusti(model));
            Assert.Equal(0, soba.DiscountPercent);
        }

        [Fact]
        public async Task PopustiPost_IspravniPodaci_PrimjenjujePopustIObavjestava()
        {
            var soba = TestDb.AddRoom(_db, cijena: 50);
            var model = new DiscountViewModel
            {
                DiscountPercent = 10,
                StartDate = DateTime.UtcNow,
                EndDate = DateTime.UtcNow.AddDays(3),
                SelectedRoomIds = new List<int> { soba.RoomID }
            };

            await _controller.Popusti(model);

            Assert.Equal(10, soba.DiscountPercent);
            Assert.Equal("Popust je primijenjen i svi korisnici su obaviješteni.", _controller.TempData["DiscountSuccess"]);
            _email.Verify(e => e.SendEmailAsync("zaposlenik@test.com", "The Last Key - Novi popust", It.Is<string>(s => s.Contains("45"))), Times.Once);
        }

        // ---------- Recenzije ----------

        [Fact]
        public async Task Reviews_PostojeRecenzije_VracaSve()
        {
            var soba = TestDb.AddRoom(_db);
            _db.Recenzije.Add(new Recenzija { KorisnikID = _zaposlenik.Id, RoomID = soba.RoomID, Ocjena = 5, Komentar = "Top" });
            _db.SaveChanges();

            Assert.Single(Assert.IsAssignableFrom<List<Recenzija>>(Assert.IsType<ViewResult>(await _controller.Reviews()).Model));
        }

        [Fact]
        public async Task DeleteReview_NePostoji_VracaNotFound()
        {
            Assert.IsType<NotFoundResult>(await _controller.DeleteReview(999));
        }

        [Fact]
        public async Task DeleteReview_Postoji_BriseRecenziju()
        {
            var soba = TestDb.AddRoom(_db);
            var recenzija = new Recenzija { KorisnikID = _zaposlenik.Id, RoomID = soba.RoomID, Ocjena = 1, Komentar = "Loše" };
            _db.Recenzije.Add(recenzija);
            _db.SaveChanges();

            await _controller.DeleteReview(recenzija.RecenzijaID);

            Assert.Empty(_db.Recenzije);
        }

        // ---------- Podrška ----------

        private Podrska DodajUpit()
        {
            var upit = new Podrska { Email = "korisnik@test.com", NaslovPoruke = "Pitanje", Sadrzaj = "Tekst", Datum = DateTime.UtcNow, KorisnikID = _zaposlenik.Id };
            _db.Podrske.Add(upit);
            _db.SaveChanges();
            return upit;
        }

        [Fact]
        public async Task Support_PostojeUpiti_VracaSve()
        {
            DodajUpit();

            Assert.Single(Assert.IsAssignableFrom<List<Podrska>>(Assert.IsType<ViewResult>(await _controller.Support()).Model));
        }

        [Fact]
        public async Task SupportDetails_NePostoji_VracaNotFound()
        {
            Assert.IsType<NotFoundResult>(await _controller.SupportDetails(999));
        }

        [Fact]
        public async Task SupportDetails_Postoji_VracaUpit()
        {
            var upit = DodajUpit();

            Assert.Same(upit, Assert.IsType<ViewResult>(await _controller.SupportDetails(upit.PorukaID)).Model);
        }

        [Fact]
        public async Task ReplySupport_NePostoji_VracaNotFound()
        {
            Assert.IsType<NotFoundResult>(await _controller.ReplySupport(999, "Odgovor"));
        }

        [Fact]
        public async Task ReplySupport_PrazanOdgovor_NeSpremaOdgovor()
        {
            var upit = DodajUpit();

            await _controller.ReplySupport(upit.PorukaID, "");

            Assert.False(upit.Odgovoreno);
            Assert.Equal("Odgovor ne može biti prazan.", _controller.TempData["SupportError"]);
        }

        [Fact]
        public async Task ReplySupport_IspravanOdgovor_SpremaISaljeEmail()
        {
            var upit = DodajUpit();

            await _controller.ReplySupport(upit.PorukaID, "Pogledajte profil.");

            Assert.True(upit.Odgovoreno);
            Assert.NotNull(upit.DatumOdgovora);
            _email.Verify(e => e.SendEmailAsync("korisnik@test.com", "The Last Key - Odgovor na vaš upit", It.IsAny<string>()), Times.Once);
        }

        [Fact]
        public async Task DeleteSupport_NePostoji_VracaNotFound()
        {
            Assert.IsType<NotFoundResult>(await _controller.DeleteSupport(999));
        }

        [Fact]
        public async Task DeleteSupport_Postoji_BriseUpit()
        {
            var upit = DodajUpit();

            await _controller.DeleteSupport(upit.PorukaID);

            Assert.Empty(_db.Podrske);
        }
    }
}
