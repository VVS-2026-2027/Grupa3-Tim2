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
    public class AdminControllerTests : IDisposable
    {
        private readonly EscapeRoomDbContext _db;
        private readonly Mock<IEmailService> _email;
        private readonly Mock<IStorageService> _storage;
        private readonly AdminController _controller;
        private readonly ApplicationUser _admin;

        public AdminControllerTests()
        {
            _db = TestDb.Create();
            _admin = TestDb.AddUser(_db, "admin@test.com", Uloga.Admin);
            _email = new Mock<IEmailService>();
            _storage = new Mock<IStorageService>();
            _controller = new AdminController(_db, _email.Object, _storage.Object);
            ControllerSetup.WithUser(_controller, _admin.Email);
        }

        public void Dispose() => _db.Dispose();

        private void PrijaviObicnogKorisnika()
        {
            var korisnik = TestDb.AddUser(_db, "korisnik@test.com");
            ControllerSetup.WithUser(_controller, korisnik.Email);
        }

        private static EscapeRoom NovaSoba(int id = 0) => new() { RoomID = id, Naziv = "Nova soba", Opis = "Opis", Kapacitet = 5, Tezina = Tezina.Lagana };

        public static IEnumerable<object[]> CijeneIzCsv() => TestData.FromCsv("cijene_soba.csv");

        // ---------- Kontrola pristupa (sve akcije provjeravaju ulogu Admin) ----------

        public static IEnumerable<object[]> AkcijeAdmina() => new[]
        {
            new object[] { (Func<AdminController, Task<IActionResult>>)(c => c.Dashboard()) },
            new object[] { (Func<AdminController, Task<IActionResult>>)(c => c.Rooms()) },
            new object[] { (Func<AdminController, Task<IActionResult>>)(c => c.CreateRoom()) },
            new object[] { (Func<AdminController, Task<IActionResult>>)(c => c.CreateRoom(NovaSoba(), null)) },
            new object[] { (Func<AdminController, Task<IActionResult>>)(c => c.EditRoom(1)) },
            new object[] { (Func<AdminController, Task<IActionResult>>)(c => c.EditRoom(NovaSoba(1), null)) },
            new object[] { (Func<AdminController, Task<IActionResult>>)(c => c.DeleteRoom(1)) },
            new object[] { (Func<AdminController, Task<IActionResult>>)(c => c.Reservations()) },
            new object[] { (Func<AdminController, Task<IActionResult>>)(c => c.Popusti()) },
            new object[] { (Func<AdminController, Task<IActionResult>>)(c => c.Popusti(new DiscountViewModel())) },
            new object[] { (Func<AdminController, Task<IActionResult>>)(c => c.Payments()) },
            new object[] { (Func<AdminController, Task<IActionResult>>)(c => c.UpdatePaymentStatus(1, true)) },
            new object[] { (Func<AdminController, Task<IActionResult>>)(c => c.Reviews()) },
            new object[] { (Func<AdminController, Task<IActionResult>>)(c => c.DeleteReview(1)) },
            new object[] { (Func<AdminController, Task<IActionResult>>)(c => c.Notifications()) },
            new object[] { (Func<AdminController, Task<IActionResult>>)(c => c.CreateNotification()) },
            new object[] { (Func<AdminController, Task<IActionResult>>)(c => c.CreateNotification(new Obavijest())) },
            new object[] { (Func<AdminController, Task<IActionResult>>)(c => c.DeleteNotification(1)) },
            new object[] { (Func<AdminController, Task<IActionResult>>)(c => c.Termini()) },
            new object[] { (Func<AdminController, Task<IActionResult>>)(c => c.CreateTermin()) },
            new object[] { (Func<AdminController, Task<IActionResult>>)(c => c.CreateTermin(new Termin())) },
            new object[] { (Func<AdminController, Task<IActionResult>>)(c => c.EditTermin(1)) },
            new object[] { (Func<AdminController, Task<IActionResult>>)(c => c.EditTermin(new Termin())) },
            new object[] { (Func<AdminController, Task<IActionResult>>)(c => c.DeleteTermin(1)) },
            new object[] { (Func<AdminController, Task<IActionResult>>)(c => c.Users()) },
            new object[] { (Func<AdminController, Task<IActionResult>>)(c => c.ChangeUserRole("x", Uloga.Admin)) },
            new object[] { (Func<AdminController, Task<IActionResult>>)(c => c.DeleteUser("x")) },
            new object[] { (Func<AdminController, Task<IActionResult>>)(c => c.ReservationDetails(1)) },
            new object[] { (Func<AdminController, Task<IActionResult>>)(c => c.Cancel(1)) },
            new object[] { (Func<AdminController, Task<IActionResult>>)(c => c.Support()) },
            new object[] { (Func<AdminController, Task<IActionResult>>)(c => c.SupportDetails(1)) },
            new object[] { (Func<AdminController, Task<IActionResult>>)(c => c.ReplySupport(1, "Odgovor")) },
            new object[] { (Func<AdminController, Task<IActionResult>>)(c => c.DeleteSupport(1)) }
        };

        [Theory]
        [MemberData(nameof(AkcijeAdmina))]
        public async Task SveAkcije_KorisnikNijeAdmin_PreusmjeravaNaPocetnu(Func<AdminController, Task<IActionResult>> akcija)
        {
            PrijaviObicnogKorisnika();

            var result = await akcija(_controller);

            var redirect = Assert.IsType<RedirectToActionResult>(result);
            Assert.Equal("Index", redirect.ActionName);
            Assert.Equal("Home", redirect.ControllerName);
        }

        // ---------- Dashboard i liste ----------

        [Fact]
        public async Task Dashboard_BezRecenzija_ProsjecnaOcjenaNula()
        {
            var result = await _controller.Dashboard();

            var stats = Assert.IsType<ViewResult>(result).ViewData["Stats"]!;
            Assert.Equal(0.0, Convert.ToDouble(stats.GetType().GetProperty("AverageRating")!.GetValue(stats)));
        }

        [Fact]
        public async Task Dashboard_PostojePodaci_VracaTacnuStatistiku()
        {
            var soba = TestDb.AddRoom(_db);
            _db.Recenzije.Add(new Recenzija { KorisnikID = _admin.Id, RoomID = soba.RoomID, Ocjena = 4, Komentar = "Dobro" });
            _db.SaveChanges();

            var result = await _controller.Dashboard();

            var stats = Assert.IsType<ViewResult>(result).ViewData["Stats"]!;
            Assert.Equal(1, stats.GetType().GetProperty("TotalRooms")!.GetValue(stats));
            Assert.Equal(4.0, Convert.ToDouble(stats.GetType().GetProperty("AverageRating")!.GetValue(stats)));
        }

        [Fact]
        public async Task Rooms_PostojeSobe_VracaSveSobe()
        {
            TestDb.AddRoom(_db, "A");
            TestDb.AddRoom(_db, "B");

            var result = await _controller.Rooms();

            Assert.Equal(2, Assert.IsAssignableFrom<List<EscapeRoom>>(Assert.IsType<ViewResult>(result).Model).Count);
        }

        [Fact]
        public async Task CreateRoomGet_Admin_VracaFormu()
        {
            var result = await _controller.CreateRoom();

            Assert.IsType<ViewResult>(result);
        }

        // ---------- CreateRoom (POST) ----------

        [Theory]
        [MemberData(nameof(CijeneIzCsv))]
        public async Task CreateRoomPost_CijeneIzCsv_SpremaSamoValidneCijene(string unesenaCijena, string validna)
        {
            ControllerSetup.WithForm(_controller, new() { ["Cijena"] = unesenaCijena });

            await _controller.CreateRoom(NovaSoba(), null);

            Assert.Equal(bool.Parse(validna), _db.EscapeRooms.Any(r => r.Naziv == "Nova soba"));
        }

        [Fact]
        public async Task CreateRoomPost_ValidnaSoba_SaljeEmailSvimKorisnicima()
        {
            TestDb.AddUser(_db, "k1@test.com");
            ControllerSetup.WithForm(_controller, new() { ["Cijena"] = "45.5" });

            var result = await _controller.CreateRoom(NovaSoba(), null);

            Assert.Equal("Rooms", Assert.IsType<RedirectToActionResult>(result).ActionName);
            Assert.Equal(45.5, _db.EscapeRooms.Single(r => r.Naziv == "Nova soba").Cijena);
            _email.Verify(e => e.SendEmailAsync(It.IsAny<string>(), "The Last Key - Nova soba: Nova soba", It.IsAny<string>()), Times.Exactly(2));
        }

        [Fact]
        public async Task CreateRoomPost_NedozvoljenFormatSlike_VracaGreskuINeUploaduje()
        {
            ControllerSetup.WithForm(_controller, new() { ["Cijena"] = "40" });
            var gif = ControllerSetup.CreateFile("slika.gif", "image/gif", 1000);

            var result = await _controller.CreateRoom(NovaSoba(), gif);

            Assert.IsType<ViewResult>(result);
            Assert.True(_controller.ModelState.ContainsKey("ImageUrl"));
            _storage.Verify(s => s.UploadImageAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task CreateRoomPost_SlikaVecaOd5MB_VracaGresku()
        {
            ControllerSetup.WithForm(_controller, new() { ["Cijena"] = "40" });
            var velika = ControllerSetup.CreateFile("slika.png", "image/png", 6 * 1024 * 1024);

            var result = await _controller.CreateRoom(NovaSoba(), velika);

            Assert.IsType<ViewResult>(result);
            Assert.Empty(_db.EscapeRooms);
        }

        [Fact]
        public async Task CreateRoomPost_IspravnaSlika_UploadujeISpremaUrl()
        {
            ControllerSetup.WithForm(_controller, new() { ["Cijena"] = "40" });
            _storage.Setup(s => s.UploadImageAsync(It.IsAny<Stream>(), "soba.jpg", "image/jpeg")).ReturnsAsync("https://slike/soba.jpg");

            await _controller.CreateRoom(NovaSoba(), ControllerSetup.CreateFile("soba.jpg", "image/jpeg", 2048));

            Assert.Equal("https://slike/soba.jpg", _db.EscapeRooms.Single().ImageUrl);
        }

        // ---------- EditRoom ----------

        [Fact]
        public async Task EditRoomGet_SobaNePostoji_VracaNotFound()
        {
            var result = await _controller.EditRoom(999);

            Assert.IsType<NotFoundResult>(result);
        }

        [Fact]
        public async Task EditRoomGet_SobaPostoji_VracaSobu()
        {
            var soba = TestDb.AddRoom(_db);

            var result = await _controller.EditRoom(soba.RoomID);

            Assert.Same(soba, Assert.IsType<ViewResult>(result).Model);
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
            ControllerSetup.WithForm(_controller, new() { ["Cijena"] = "40" });

            var result = await _controller.EditRoom(NovaSoba(999), null);

            Assert.IsType<NotFoundResult>(result);
        }

        [Fact]
        public async Task EditRoomPost_BezNoveSlike_ZadrzavaStaruSliku()
        {
            var soba = TestDb.AddRoom(_db);
            soba.ImageUrl = "https://slike/stara.jpg";
            _db.SaveChanges();
            _db.ChangeTracker.Clear();
            ControllerSetup.WithForm(_controller, new() { ["Cijena"] = "60" });

            var result = await _controller.EditRoom(NovaSoba(soba.RoomID), null);

            Assert.Equal("Rooms", Assert.IsType<RedirectToActionResult>(result).ActionName);
            var izmijenjena = _db.EscapeRooms.Single();
            Assert.Equal("https://slike/stara.jpg", izmijenjena.ImageUrl);
            Assert.Equal(60, izmijenjena.Cijena);
        }

        [Fact]
        public async Task EditRoomPost_NovaSlika_BriseStaruIUploadujeNovu()
        {
            var soba = TestDb.AddRoom(_db);
            soba.ImageUrl = "https://slike/stara.jpg";
            _db.SaveChanges();
            _db.ChangeTracker.Clear();
            ControllerSetup.WithForm(_controller, new() { ["Cijena"] = "60" });
            _storage.Setup(s => s.UploadImageAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string>())).ReturnsAsync("https://slike/nova.png");

            await _controller.EditRoom(NovaSoba(soba.RoomID), ControllerSetup.CreateFile("nova.png", "image/png", 1024));

            _storage.Verify(s => s.DeleteImageAsync("https://slike/stara.jpg"), Times.Once);
            Assert.Equal("https://slike/nova.png", _db.EscapeRooms.Single().ImageUrl);
        }

        [Fact]
        public async Task EditRoomPost_NovaSlikaBezStare_NeBriseNista()
        {
            var soba = TestDb.AddRoom(_db);
            _db.ChangeTracker.Clear();
            ControllerSetup.WithForm(_controller, new() { ["Cijena"] = "60" });

            await _controller.EditRoom(NovaSoba(soba.RoomID), ControllerSetup.CreateFile("nova.webp", "image/webp", 1024));

            _storage.Verify(s => s.DeleteImageAsync(It.IsAny<string>()), Times.Never);
        }

        [Theory]
        [InlineData("slika.bmp", "image/bmp", 1024)]
        [InlineData("slika.jpg", "image/jpeg", 6 * 1024 * 1024)]
        public async Task EditRoomPost_NeispravnaSlika_VracaFormu(string ime, string tip, long velicina)
        {
            var soba = TestDb.AddRoom(_db);
            _db.ChangeTracker.Clear();
            ControllerSetup.WithForm(_controller, new() { ["Cijena"] = "60" });

            var result = await _controller.EditRoom(NovaSoba(soba.RoomID), ControllerSetup.CreateFile(ime, tip, velicina));

            Assert.IsType<ViewResult>(result);
            Assert.True(_controller.ModelState.ContainsKey("ImageUrl"));
        }

        // ---------- DeleteRoom ----------

        [Fact]
        public async Task DeleteRoom_SobaNePostoji_VracaNotFound()
        {
            var result = await _controller.DeleteRoom(999);

            Assert.IsType<NotFoundResult>(result);
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

        // ---------- Rezervacije ----------

        [Fact]
        public async Task Reservations_PostojeRezervacije_VracaSve()
        {
            var soba = TestDb.AddRoom(_db);
            TestDb.AddRezervacija(_db, _admin, TestDb.AddTermin(_db, soba, DateTime.UtcNow.AddDays(1)));

            var result = await _controller.Reservations();

            Assert.Single(Assert.IsAssignableFrom<List<Rezervacija>>(Assert.IsType<ViewResult>(result).Model));
        }

        [Fact]
        public async Task ReservationDetails_NePostoji_VracaNotFound()
        {
            var result = await _controller.ReservationDetails(999);

            Assert.IsType<NotFoundResult>(result);
        }

        [Fact]
        public async Task ReservationDetails_Postoji_VracaRezervaciju()
        {
            var rez = TestDb.AddRezervacija(_db, _admin, TestDb.AddTermin(_db, TestDb.AddRoom(_db), DateTime.UtcNow.AddDays(1)));

            var result = await _controller.ReservationDetails(rez.RezervacijaID);

            Assert.Same(rez, Assert.IsType<ViewResult>(result).Model);
        }

        [Fact]
        public async Task Cancel_RezervacijaNePostoji_VracaNotFound()
        {
            var result = await _controller.Cancel(999);

            Assert.IsType<NotFoundResult>(result);
        }

        [Fact]
        public async Task Cancel_AktivnaRezervacija_BriseOslobadjaTerminISaljeEmail()
        {
            var korisnik = TestDb.AddUser(_db, "korisnik@test.com");
            var termin = TestDb.AddTermin(_db, TestDb.AddRoom(_db), DateTime.UtcNow.AddDays(1), dostupnost: false);
            var rez = TestDb.AddRezervacija(_db, korisnik, termin);

            var result = await _controller.Cancel(rez.RezervacijaID);

            Assert.Equal("Reservations", Assert.IsType<RedirectToActionResult>(result).ActionName);
            Assert.Empty(_db.Rezervacije);
            Assert.True(termin.Dostupnost);
            _email.Verify(e => e.SendEmailAsync("korisnik@test.com", "The Last Key - Rezervacija otkazana", It.IsAny<string>()), Times.Once);
        }

        [Fact]
        public async Task Cancel_VecOtkazanaRezervacija_BriseBezSlanjaEmaila()
        {
            var termin = TestDb.AddTermin(_db, TestDb.AddRoom(_db), DateTime.UtcNow.AddDays(1));
            var rez = TestDb.AddRezervacija(_db, TestDb.AddUser(_db, "k@test.com"), termin, status: false);

            await _controller.Cancel(rez.RezervacijaID);

            Assert.Empty(_db.Rezervacije);
            _email.Verify(e => e.SendEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        }

        // ---------- Popusti ----------

        [Fact]
        public async Task PopustiGet_PostojeSobe_PopunjavaListuSoba()
        {
            TestDb.AddRoom(_db, "A");

            var result = await _controller.Popusti();

            var model = Assert.IsType<DiscountViewModel>(Assert.IsType<ViewResult>(result).Model);
            Assert.Equal("A", Assert.Single(model.Rooms).Naziv);
        }

        [Theory]
        [InlineData(-5, 1, 5, true)]
        [InlineData(150, 1, 5, true)]
        [InlineData(20, 5, 1, true)]
        [InlineData(20, 1, 5, false)]
        public async Task PopustiPost_NeispravniPodaci_VracaFormuSaGreskom(int procenat, int pocetak, int kraj, bool odabranaSoba)
        {
            var soba = TestDb.AddRoom(_db);
            var model = new DiscountViewModel
            {
                DiscountPercent = procenat,
                StartDate = DateTime.UtcNow.AddDays(pocetak),
                EndDate = DateTime.UtcNow.AddDays(kraj),
                SelectedRoomIds = odabranaSoba ? new List<int> { soba.RoomID } : new List<int>()
            };

            var result = await _controller.Popusti(model);

            Assert.IsType<ViewResult>(result);
            Assert.False(_controller.ModelState.IsValid);
            Assert.Equal(0, soba.DiscountPercent);
        }

        [Fact]
        public async Task PopustiPost_BezDatuma_VracaFormuSaGreskom()
        {
            var soba = TestDb.AddRoom(_db);

            var result = await _controller.Popusti(new DiscountViewModel { DiscountPercent = 10, SelectedRoomIds = new List<int> { soba.RoomID } });

            Assert.IsType<ViewResult>(result);
        }

        [Fact]
        public async Task PopustiPost_IspravniPodaci_PrimjenjujePopustIObavjestavaKorisnike()
        {
            var odabrana = TestDb.AddRoom(_db, "Odabrana", cijena: 100);
            var ostala = TestDb.AddRoom(_db, "Ostala");
            var model = new DiscountViewModel
            {
                DiscountPercent = 20,
                StartDate = DateTime.UtcNow,
                EndDate = DateTime.UtcNow.AddDays(7),
                SelectedRoomIds = new List<int> { odabrana.RoomID }
            };

            var result = await _controller.Popusti(model);

            Assert.Equal("Popusti", Assert.IsType<RedirectToActionResult>(result).ActionName);
            Assert.Equal(20, odabrana.DiscountPercent);
            Assert.Equal(0, ostala.DiscountPercent);
            _email.Verify(e => e.SendEmailAsync("admin@test.com", "The Last Key - Novi popust", It.Is<string>(s => s.Contains("80"))), Times.Once);
        }

        // ---------- Plaćanja ----------

        [Fact]
        public async Task Payments_PostojePlacanja_VracaSve()
        {
            var rez = TestDb.AddRezervacija(_db, _admin, TestDb.AddTermin(_db, TestDb.AddRoom(_db), DateTime.UtcNow.AddDays(1)));
            _db.Placanja.Add(new Placanje { RezervacijaID = rez.RezervacijaID, Iznos = 50, Datum = DateTime.UtcNow });
            _db.SaveChanges();

            var result = await _controller.Payments();

            Assert.Single(Assert.IsAssignableFrom<List<Placanje>>(Assert.IsType<ViewResult>(result).Model));
        }

        [Fact]
        public async Task UpdatePaymentStatus_PlacanjeNePostoji_VracaNotFound()
        {
            var result = await _controller.UpdatePaymentStatus(999, true);

            Assert.IsType<NotFoundResult>(result);
        }

        [Theory]
        [InlineData(true, "The Last Key - Plaćanje potvrđeno")]
        [InlineData(false, "The Last Key - Plaćanje nije potvrđeno")]
        public async Task UpdatePaymentStatus_PromjenaStatusa_MijenjaStatusISaljeOdgovarajuciEmail(bool status, string naslov)
        {
            var korisnik = TestDb.AddUser(_db, "korisnik@test.com");
            var rez = TestDb.AddRezervacija(_db, korisnik, TestDb.AddTermin(_db, TestDb.AddRoom(_db), DateTime.UtcNow.AddDays(1)));
            var placanje = new Placanje { RezervacijaID = rez.RezervacijaID, Iznos = 50, Datum = DateTime.UtcNow, Status = !status };
            _db.Placanja.Add(placanje);
            _db.SaveChanges();

            await _controller.UpdatePaymentStatus(placanje.PlacanjeID, status);

            Assert.Equal(status, placanje.Status);
            _email.Verify(e => e.SendEmailAsync("korisnik@test.com", naslov, It.IsAny<string>()), Times.Once);
        }

        [Fact]
        public async Task UpdatePaymentStatus_KorisnikBezEmaila_NeSaljeEmail()
        {
            var korisnik = TestDb.AddUser(_db, "korisnik@test.com");
            korisnik.Email = null;
            var rez = TestDb.AddRezervacija(_db, korisnik, TestDb.AddTermin(_db, TestDb.AddRoom(_db), DateTime.UtcNow.AddDays(1)));
            var placanje = new Placanje { RezervacijaID = rez.RezervacijaID, Iznos = 50, Datum = DateTime.UtcNow };
            _db.Placanja.Add(placanje);
            _db.SaveChanges();

            await _controller.UpdatePaymentStatus(placanje.PlacanjeID, true);

            _email.Verify(e => e.SendEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        }

        // ---------- Recenzije ----------

        [Fact]
        public async Task Reviews_PostojeRecenzije_VracaSve()
        {
            var soba = TestDb.AddRoom(_db);
            _db.Recenzije.Add(new Recenzija { KorisnikID = _admin.Id, RoomID = soba.RoomID, Ocjena = 3, Komentar = "Ok" });
            _db.SaveChanges();

            var result = await _controller.Reviews();

            Assert.Single(Assert.IsAssignableFrom<List<Recenzija>>(Assert.IsType<ViewResult>(result).Model));
        }

        [Fact]
        public async Task DeleteReview_NePostoji_VracaNotFound()
        {
            var result = await _controller.DeleteReview(999);

            Assert.IsType<NotFoundResult>(result);
        }

        [Fact]
        public async Task DeleteReview_Postoji_BriseRecenziju()
        {
            var soba = TestDb.AddRoom(_db);
            var recenzija = new Recenzija { KorisnikID = _admin.Id, RoomID = soba.RoomID, Ocjena = 1, Komentar = "Loše" };
            _db.Recenzije.Add(recenzija);
            _db.SaveChanges();

            await _controller.DeleteReview(recenzija.RecenzijaID);

            Assert.Empty(_db.Recenzije);
        }

        // ---------- Obavijesti ----------

        [Fact]
        public async Task Notifications_PostojeObavijesti_VracaSve()
        {
            _db.Obavijesti.Add(new Obavijest { Naslov = "N", Sadrzaj = "S", Datum = DateTime.UtcNow });
            _db.SaveChanges();

            var result = await _controller.Notifications();

            Assert.Single(Assert.IsAssignableFrom<List<Obavijest>>(Assert.IsType<ViewResult>(result).Model));
        }

        [Fact]
        public async Task CreateNotificationGet_Admin_VracaFormu()
        {
            var result = await _controller.CreateNotification();

            Assert.IsType<ViewResult>(result);
        }

        [Theory]
        [InlineData(TipObavijesti.Popust, 1)]
        [InlineData(TipObavijesti.Novost, 1)]
        [InlineData(TipObavijesti.Rezervacija, 0)]
        [InlineData(TipObavijesti.Otkazivanje, 0)]
        public async Task CreateNotificationPost_TipObavijesti_SaljeEmailSamoZaPopustINovost(TipObavijesti tip, int brojEmailova)
        {
            var result = await _controller.CreateNotification(new Obavijest { Naslov = "Naslov", Sadrzaj = "Tekst", TipObavijesti = tip });

            Assert.Equal("Notifications", Assert.IsType<RedirectToActionResult>(result).ActionName);
            Assert.Single(_db.Obavijesti);
            _email.Verify(e => e.SendEmailAsync(It.IsAny<string>(), "The Last Key - Naslov", It.IsAny<string>()), Times.Exactly(brojEmailova));
        }

        [Fact]
        public async Task DeleteNotification_NePostoji_VracaNotFound()
        {
            var result = await _controller.DeleteNotification(999);

            Assert.IsType<NotFoundResult>(result);
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

        // ---------- Termini ----------

        [Fact]
        public async Task Termini_PostojeTermini_VracaSve()
        {
            TestDb.AddTermin(_db, TestDb.AddRoom(_db), DateTime.UtcNow.AddDays(1));

            var result = await _controller.Termini();

            Assert.Single(Assert.IsAssignableFrom<List<Termin>>(Assert.IsType<ViewResult>(result).Model));
        }

        [Fact]
        public async Task CreateTerminGet_Admin_PopunjavaListuSoba()
        {
            TestDb.AddRoom(_db);

            var result = await _controller.CreateTermin();

            Assert.Single(Assert.IsAssignableFrom<List<EscapeRoom>>(Assert.IsType<ViewResult>(result).ViewData["Rooms"]));
        }

        [Fact]
        public async Task CreateTerminPost_TerminUProslosti_VracaFormuSaGreskom()
        {
            var soba = TestDb.AddRoom(_db);

            var result = await _controller.CreateTermin(new Termin { RoomID = soba.RoomID, Datum = DateTime.Now.AddDays(-1), Vrijeme = "10:00" });

            Assert.IsType<ViewResult>(result);
            Assert.Empty(_db.Termini);
        }

        [Fact]
        public async Task CreateTerminPost_BuduciTermin_SpremaTerminUUtc()
        {
            var soba = TestDb.AddRoom(_db);

            var result = await _controller.CreateTermin(new Termin { RoomID = soba.RoomID, Datum = DateTime.Now.AddDays(2), Vrijeme = "18:00", Dostupnost = true });

            Assert.Equal("Termini", Assert.IsType<RedirectToActionResult>(result).ActionName);
            Assert.Equal(DateTimeKind.Utc, Assert.Single(_db.Termini).Datum.Kind);
        }

        [Fact]
        public async Task EditTerminGet_NePostoji_VracaNotFound()
        {
            var result = await _controller.EditTermin(999);

            Assert.IsType<NotFoundResult>(result);
        }

        [Fact]
        public async Task EditTerminGet_Postoji_VracaTermin()
        {
            var termin = TestDb.AddTermin(_db, TestDb.AddRoom(_db), DateTime.UtcNow.AddDays(1));

            var result = await _controller.EditTermin(termin.TerminID);

            Assert.Same(termin, Assert.IsType<ViewResult>(result).Model);
        }

        [Fact]
        public async Task EditTerminPost_TerminUProslosti_VracaFormuSaGreskom()
        {
            var termin = TestDb.AddTermin(_db, TestDb.AddRoom(_db), DateTime.UtcNow.AddDays(1));
            _db.ChangeTracker.Clear();

            var result = await _controller.EditTermin(new Termin { TerminID = termin.TerminID, RoomID = termin.RoomID, Datum = DateTime.Now.AddDays(-2), Vrijeme = "nije-vrijeme" });

            Assert.IsType<ViewResult>(result);
            Assert.False(_controller.ModelState.IsValid);
        }

        [Fact]
        public async Task EditTerminPost_BuduciTermin_AzuriraTermin()
        {
            var termin = TestDb.AddTermin(_db, TestDb.AddRoom(_db), DateTime.UtcNow.AddDays(1), "10:00");
            _db.ChangeTracker.Clear();

            var result = await _controller.EditTermin(new Termin { TerminID = termin.TerminID, RoomID = termin.RoomID, Datum = DateTime.Now.AddDays(3), Vrijeme = "20:00" });

            Assert.Equal("Termini", Assert.IsType<RedirectToActionResult>(result).ActionName);
            Assert.Equal("20:00", _db.Termini.Single().Vrijeme);
        }

        [Fact]
        public async Task DeleteTermin_NePostoji_VracaNotFound()
        {
            var result = await _controller.DeleteTermin(999);

            Assert.IsType<NotFoundResult>(result);
        }

        [Fact]
        public async Task DeleteTermin_Postoji_BriseTermin()
        {
            var termin = TestDb.AddTermin(_db, TestDb.AddRoom(_db), DateTime.UtcNow.AddDays(1));

            await _controller.DeleteTermin(termin.TerminID);

            Assert.Empty(_db.Termini);
        }

        // ---------- Korisnici ----------

        [Fact]
        public async Task Users_PostojeKorisnici_VracaSve()
        {
            TestDb.AddUser(_db, "k@test.com");

            var result = await _controller.Users();

            Assert.Equal(2, Assert.IsAssignableFrom<List<ApplicationUser>>(Assert.IsType<ViewResult>(result).Model).Count);
        }

        [Fact]
        public async Task ChangeUserRole_KorisnikNePostoji_VracaNotFound()
        {
            var result = await _controller.ChangeUserRole("nepostojeci", Uloga.Zaposlenik);

            Assert.IsType<NotFoundResult>(result);
        }

        [Fact]
        public async Task ChangeUserRole_KorisnikPostoji_MijenjaUlogu()
        {
            var korisnik = TestDb.AddUser(_db, "k@test.com");

            await _controller.ChangeUserRole(korisnik.Id, Uloga.Zaposlenik);

            Assert.Equal(Uloga.Zaposlenik, korisnik.Uloga);
        }

        [Fact]
        public async Task DeleteUser_KorisnikNePostoji_VracaNotFound()
        {
            var result = await _controller.DeleteUser("nepostojeci");

            Assert.IsType<NotFoundResult>(result);
        }

        [Fact]
        public async Task DeleteUser_AdminBriseSebe_OdbijaBrisanje()
        {
            await _controller.DeleteUser(_admin.Id);

            Assert.Contains(_db.Users, u => u.Id == _admin.Id);
            Assert.Equal("Ne možete deaktivirati vlastiti račun.", _controller.TempData["UserError"]);
        }

        [Fact]
        public async Task DeleteUser_DrugiKorisnik_BriseKorisnika()
        {
            var korisnik = TestDb.AddUser(_db, "k@test.com");

            await _controller.DeleteUser(korisnik.Id);

            Assert.DoesNotContain(_db.Users, u => u.Id == korisnik.Id);
            Assert.Equal("Račun je uspješno deaktiviran.", _controller.TempData["UserSuccess"]);
        }

        // ---------- Podrška ----------

        private Podrska DodajUpit()
        {
            var upit = new Podrska { Email = "korisnik@test.com", NaslovPoruke = "Pitanje", Sadrzaj = "Tekst", Datum = DateTime.UtcNow, KorisnikID = _admin.Id };
            _db.Podrske.Add(upit);
            _db.SaveChanges();
            return upit;
        }

        [Fact]
        public async Task Support_PostojeUpiti_VracaSve()
        {
            DodajUpit();

            var result = await _controller.Support();

            Assert.Single(Assert.IsAssignableFrom<List<Podrska>>(Assert.IsType<ViewResult>(result).Model));
        }

        [Fact]
        public async Task SupportDetails_NePostoji_VracaNotFound()
        {
            var result = await _controller.SupportDetails(999);

            Assert.IsType<NotFoundResult>(result);
        }

        [Fact]
        public async Task SupportDetails_Postoji_VracaUpit()
        {
            var upit = DodajUpit();

            var result = await _controller.SupportDetails(upit.PorukaID);

            Assert.Same(upit, Assert.IsType<ViewResult>(result).Model);
        }

        [Fact]
        public async Task ReplySupport_UpitNePostoji_VracaNotFound()
        {
            var result = await _controller.ReplySupport(999, "Odgovor");

            Assert.IsType<NotFoundResult>(result);
        }

        [Fact]
        public async Task ReplySupport_PrazanOdgovor_PostavljaGreskuINeSaljeEmail()
        {
            var upit = DodajUpit();

            await _controller.ReplySupport(upit.PorukaID, " ");

            Assert.False(upit.Odgovoreno);
            Assert.Equal("Odgovor ne može biti prazan.", _controller.TempData["SupportError"]);
            _email.Verify(e => e.SendEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task ReplySupport_IspravanOdgovor_SpremaOdgovorISaljeEmail()
        {
            var upit = DodajUpit();

            var result = await _controller.ReplySupport(upit.PorukaID, "Riješeno je.");

            Assert.Equal("SupportDetails", Assert.IsType<RedirectToActionResult>(result).ActionName);
            Assert.True(upit.Odgovoreno);
            Assert.Equal("Riješeno je.", upit.Odgovor);
            _email.Verify(e => e.SendEmailAsync("korisnik@test.com", "The Last Key - Odgovor na vaš upit", It.Is<string>(s => s.Contains("Riješeno je."))), Times.Once);
        }

        [Fact]
        public async Task DeleteSupport_NePostoji_VracaNotFound()
        {
            var result = await _controller.DeleteSupport(999);

            Assert.IsType<NotFoundResult>(result);
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
