using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Task6.Controllers;
using Task6.Data;
using Task6.Models;
using Task6.Services;
using Task6.Tests.Helpers;

namespace Task6.Tests.Controllers
{
    public class ReservationControllerTests : IDisposable
    {
        private readonly EscapeRoomDbContext _db;
        private readonly Mock<Microsoft.AspNetCore.Identity.UserManager<ApplicationUser>> _userManager;
        private readonly Mock<IEmailService> _email;
        private readonly ReservationController _controller;
        private readonly ApplicationUser _user;

        public ReservationControllerTests()
        {
            _db = TestDb.Create();
            _user = TestDb.AddUser(_db, "korisnik@test.com");
            _userManager = ControllerSetup.CreateUserManagerMock();
            _userManager.Setup(m => m.GetUserAsync(It.IsAny<ClaimsPrincipal>())).ReturnsAsync(_user);
            _email = new Mock<IEmailService>();
            _controller = new ReservationController(_db, _userManager.Object, _email.Object);
            ControllerSetup.WithUser(_controller, _user.Email, _user.Id);
        }

        public void Dispose() => _db.Dispose();

        private void BezPrijavljenogKorisnika() =>
            _userManager.Setup(m => m.GetUserAsync(It.IsAny<ClaimsPrincipal>())).ReturnsAsync((ApplicationUser?)null);

        // ---------- MyReservations ----------

        [Fact]
        public async Task MyReservations_KorisnikNijePrijavljen_PreusmjeravaNaLogin()
        {
            BezPrijavljenogKorisnika();

            var result = await _controller.MyReservations();

            var redirect = Assert.IsType<RedirectToActionResult>(result);
            Assert.Equal("Login", redirect.ActionName);
            Assert.Equal("Account", redirect.ControllerName);
        }

        [Fact]
        public async Task MyReservations_KorisnikImaRezervacije_VracaSamoNjegoveRezervacije()
        {
            var soba = TestDb.AddRoom(_db);
            var drugi = TestDb.AddUser(_db, "drugi@test.com");
            TestDb.AddRezervacija(_db, _user, TestDb.AddTermin(_db, soba, DateTime.UtcNow.AddDays(1)));
            TestDb.AddRezervacija(_db, drugi, TestDb.AddTermin(_db, soba, DateTime.UtcNow.AddDays(2)));

            var result = await _controller.MyReservations();

            var view = Assert.IsType<ViewResult>(result);
            var model = Assert.IsAssignableFrom<List<Rezervacija>>(view.Model);
            Assert.Single(model);
            Assert.Equal(_user.Id, model[0].KorisnikID);
        }

        [Fact]
        public async Task MyReservations_PostojiPlacanje_VracaListuPlacenihRezervacija()
        {
            var soba = TestDb.AddRoom(_db);
            var rez = TestDb.AddRezervacija(_db, _user, TestDb.AddTermin(_db, soba, DateTime.UtcNow.AddDays(1)));
            _db.Placanja.Add(new Placanje { RezervacijaID = rez.RezervacijaID, Iznos = 100, Datum = DateTime.UtcNow, Status = true });
            _db.SaveChanges();

            var result = await _controller.MyReservations();

            var view = Assert.IsType<ViewResult>(result);
            var placene = Assert.IsType<List<int>>(view.ViewData["PlaceneRezervacije"]);
            Assert.Contains(rez.RezervacijaID, placene);
        }

        // ---------- Details ----------

        [Fact]
        public async Task Details_KorisnikNijePrijavljen_PreusmjeravaNaLogin()
        {
            BezPrijavljenogKorisnika();

            var result = await _controller.Details(1);

            Assert.IsType<RedirectToActionResult>(result);
        }

        [Fact]
        public async Task Details_RezervacijaDrugogKorisnika_VracaNotFound()
        {
            var drugi = TestDb.AddUser(_db, "drugi@test.com");
            var rez = TestDb.AddRezervacija(_db, drugi, TestDb.AddTermin(_db, TestDb.AddRoom(_db), DateTime.UtcNow.AddDays(1)));

            var result = await _controller.Details(rez.RezervacijaID);

            Assert.IsType<NotFoundResult>(result);
        }

        [Fact]
        public async Task Details_VlastitaRezervacija_VracaViewSaRezervacijom()
        {
            var rez = TestDb.AddRezervacija(_db, _user, TestDb.AddTermin(_db, TestDb.AddRoom(_db), DateTime.UtcNow.AddDays(1)));

            var result = await _controller.Details(rez.RezervacijaID);

            var view = Assert.IsType<ViewResult>(result);
            Assert.Equal(rez.RezervacijaID, Assert.IsType<Rezervacija>(view.Model).RezervacijaID);
        }

        // ---------- Book ----------

        [Fact]
        public async Task Book_KorisnikNijePrijavljen_VracaUnauthorized()
        {
            BezPrijavljenogKorisnika();

            var result = await _controller.Book(1, 2);

            Assert.IsType<UnauthorizedResult>(result);
        }

        [Fact]
        public async Task Book_TerminNePostoji_VracaBadRequest()
        {
            var result = await _controller.Book(999, 2);

            Assert.IsType<BadRequestObjectResult>(result);
        }

        [Fact]
        public async Task Book_TerminVecZauzet_VracaBadRequest()
        {
            var termin = TestDb.AddTermin(_db, TestDb.AddRoom(_db), DateTime.UtcNow.AddDays(1), dostupnost: false);

            var result = await _controller.Book(termin.TerminID, 2);

            Assert.IsType<BadRequestObjectResult>(result);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(7)]
        [InlineData(-1)]
        public async Task Book_BrojOsobaVanKapaciteta_VracaBadRequest(int brojOsoba)
        {
            var termin = TestDb.AddTermin(_db, TestDb.AddRoom(_db, kapacitet: 6), DateTime.UtcNow.AddDays(1));

            var result = await _controller.Book(termin.TerminID, brojOsoba);

            Assert.IsType<BadRequestObjectResult>(result);
            Assert.Empty(_db.Rezervacije);
        }

        [Fact]
        public async Task Book_TerminUProslosti_VracaBadRequest()
        {
            var termin = TestDb.AddTermin(_db, TestDb.AddRoom(_db), DateTime.UtcNow.AddDays(-1), "10:00");

            var result = await _controller.Book(termin.TerminID, 2);

            Assert.IsType<BadRequestObjectResult>(result);
        }

        [Fact]
        public async Task Book_ValidnaRezervacija_KreiraRezervacijuIZauzimaTermin()
        {
            var termin = TestDb.AddTermin(_db, TestDb.AddRoom(_db), DateTime.UtcNow.AddDays(3));

            var result = await _controller.Book(termin.TerminID, 4);

            var redirect = Assert.IsType<RedirectToActionResult>(result);
            Assert.Equal("MyReservations", redirect.ActionName);
            var rez = Assert.Single(_db.Rezervacije);
            Assert.Equal(4, rez.BrojOsoba);
            Assert.True(rez.Status);
            Assert.False(termin.Dostupnost);
        }

        [Fact]
        public async Task Book_ValidnaRezervacija_SaljeEmailPotvrdeKorisniku()
        {
            var termin = TestDb.AddTermin(_db, TestDb.AddRoom(_db), DateTime.UtcNow.AddDays(3));

            await _controller.Book(termin.TerminID, 2);

            _email.Verify(e => e.SendEmailAsync("korisnik@test.com", "The Last Key - Potvrda rezervacije", It.IsAny<string>()), Times.Once);
        }

        [Fact]
        public async Task Book_KorisnikBezEmaila_NeSaljeEmail()
        {
            _user.Email = null;
            var termin = TestDb.AddTermin(_db, TestDb.AddRoom(_db), DateTime.UtcNow.AddDays(3));

            await _controller.Book(termin.TerminID, 2);

            _email.Verify(e => e.SendEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        }

        // ---------- Cancel ----------

        [Fact]
        public async Task Cancel_KorisnikNijePrijavljen_PreusmjeravaNaLogin()
        {
            BezPrijavljenogKorisnika();

            var result = await _controller.Cancel(1);

            Assert.Equal("Login", Assert.IsType<RedirectToActionResult>(result).ActionName);
        }

        [Fact]
        public async Task Cancel_RezervacijaNePostoji_VracaNotFound()
        {
            var result = await _controller.Cancel(999);

            Assert.IsType<NotFoundResult>(result);
        }

        [Fact]
        public async Task Cancel_VecOtkazanaRezervacija_PostavljaGresku()
        {
            var rez = TestDb.AddRezervacija(_db, _user, TestDb.AddTermin(_db, TestDb.AddRoom(_db), DateTime.UtcNow.AddDays(3)), status: false);

            await _controller.Cancel(rez.RezervacijaID);

            Assert.Equal("Rezervaciju nije moguće otkazati.", _controller.TempData["CancelError"]);
        }

        [Fact]
        public async Task Cancel_ManjeOdDvaSataDoTermina_NeOtkazujeRezervaciju()
        {
            var zaSat = DateTime.UtcNow.AddHours(1);
            var termin = TestDb.AddTermin(_db, TestDb.AddRoom(_db), zaSat.Date, zaSat.ToString("HH:mm"), dostupnost: false);
            var rez = TestDb.AddRezervacija(_db, _user, termin);

            await _controller.Cancel(rez.RezervacijaID);

            Assert.True(rez.Status);
            Assert.Equal("Rezervaciju nije moguće otkazati manje od 2 sata prije termina.", _controller.TempData["CancelError"]);
        }

        [Fact]
        public async Task Cancel_ValidnoOtkazivanje_OtkazujeIOslobadjaTermin()
        {
            var termin = TestDb.AddTermin(_db, TestDb.AddRoom(_db), DateTime.UtcNow.AddDays(2), dostupnost: false);
            var rez = TestDb.AddRezervacija(_db, _user, termin);

            var result = await _controller.Cancel(rez.RezervacijaID);

            Assert.False(rez.Status);
            Assert.True(termin.Dostupnost);
            Assert.Equal("Uspješno ste otkazali rezervaciju.", _controller.TempData["CancelSuccess"]);
            Assert.Equal("Index", Assert.IsType<RedirectToActionResult>(result).ActionName);
        }

        [Fact]
        public async Task Cancel_ValidnoOtkazivanje_SaljeEmailOOtkazivanju()
        {
            var rez = TestDb.AddRezervacija(_db, _user, TestDb.AddTermin(_db, TestDb.AddRoom(_db), DateTime.UtcNow.AddDays(2)));

            await _controller.Cancel(rez.RezervacijaID);

            _email.Verify(e => e.SendEmailAsync("korisnik@test.com", "The Last Key - Rezervacija otkazana", It.IsAny<string>()), Times.Once);
        }

        [Fact]
        public async Task Cancel_KorisnikBezEmaila_NeSaljeEmail()
        {
            var rez = TestDb.AddRezervacija(_db, _user, TestDb.AddTermin(_db, TestDb.AddRoom(_db), DateTime.UtcNow.AddDays(2)));
            _user.Email = "";

            await _controller.Cancel(rez.RezervacijaID);

            _email.Verify(e => e.SendEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        }
    }
}
