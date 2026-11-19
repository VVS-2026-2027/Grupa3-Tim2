using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Task6.Controllers;
using Task6.Data;
using Task6.Models;
using Task6.Tests.Helpers;

namespace Task6.Tests.Controllers
{
    public class SupportControllerTests : IDisposable
    {
        private readonly EscapeRoomDbContext _db;
        private readonly Mock<UserManager<ApplicationUser>> _userManager;
        private readonly SupportController _controller;
        private readonly ApplicationUser _user;

        public SupportControllerTests()
        {
            _db = TestDb.Create();
            _user = TestDb.AddUser(_db, "korisnik@test.com");
            _userManager = ControllerSetup.CreateUserManagerMock();
            _userManager.Setup(m => m.GetUserAsync(It.IsAny<ClaimsPrincipal>())).ReturnsAsync(_user);
            _controller = new SupportController(_db, _userManager.Object);
            ControllerSetup.WithUser(_controller, _user.Email, _user.Id);
        }

        public void Dispose() => _db.Dispose();

        [Fact]
        public async Task ContactGet_KorisnikNijePrijavljen_PreusmjeravaNaLogin()
        {
            _userManager.Setup(m => m.GetUserAsync(It.IsAny<ClaimsPrincipal>())).ReturnsAsync((ApplicationUser?)null);

            var result = await _controller.Contact();

            Assert.Equal("Login", Assert.IsType<RedirectToActionResult>(result).ActionName);
        }

        [Fact]
        public async Task ContactGet_PrijavljenKorisnik_PopunjavaEmail()
        {
            var result = await _controller.Contact();

            Assert.Equal("korisnik@test.com", Assert.IsType<ViewResult>(result).ViewData["Email"]);
        }

        [Fact]
        public async Task ContactPost_KorisnikNijePrijavljen_PreusmjeravaNaLogin()
        {
            _userManager.Setup(m => m.GetUserAsync(It.IsAny<ClaimsPrincipal>())).ReturnsAsync((ApplicationUser?)null);

            var result = await _controller.Contact(new Podrska());

            Assert.Equal("Login", Assert.IsType<RedirectToActionResult>(result).ActionName);
        }

        [Fact]
        public async Task ContactPost_ValidnaPoruka_SpremaPorukuSaPodacimaKorisnika()
        {
            var model = new Podrska { NaslovPoruke = "Problem", Sadrzaj = "Ne mogu platiti" };

            var result = await _controller.Contact(model);

            var redirect = Assert.IsType<RedirectToActionResult>(result);
            Assert.Equal("Profil", redirect.ControllerName);
            var poruka = Assert.Single(_db.Podrske);
            Assert.Equal(_user.Id, poruka.KorisnikID);
            Assert.Equal("korisnik@test.com", poruka.Email);
            Assert.False(poruka.Odgovoreno);
        }

        [Fact]
        public async Task ContactPost_KorisnikBezEmaila_SpremaPrazanEmail()
        {
            _user.Email = null;

            await _controller.Contact(new Podrska { NaslovPoruke = "Pitanje", Sadrzaj = "Tekst" });

            Assert.Equal("", Assert.Single(_db.Podrske).Email);
        }

        [Fact]
        public async Task ContactPost_NevalidanModel_VracaFormuBezSpremanja()
        {
            _controller.ModelState.AddModelError("Sadrzaj", "Sadržaj je obavezan.");
            var model = new Podrska { NaslovPoruke = "Pitanje" };

            var result = await _controller.Contact(model);

            Assert.Same(model, Assert.IsType<ViewResult>(result).Model);
            Assert.Empty(_db.Podrske);
        }
    }

    public class ProfilControllerTests : IDisposable
    {
        private readonly EscapeRoomDbContext _db;
        private readonly Mock<UserManager<ApplicationUser>> _userManager;
        private readonly ProfilController _controller;
        private readonly ApplicationUser _user;
        private readonly EscapeRoom _soba;

        public ProfilControllerTests()
        {
            _db = TestDb.Create();
            _user = TestDb.AddUser(_db, "korisnik@test.com");
            _soba = TestDb.AddRoom(_db);
            _userManager = ControllerSetup.CreateUserManagerMock();
            _userManager.Setup(m => m.GetUserAsync(It.IsAny<ClaimsPrincipal>())).ReturnsAsync(_user);
            _controller = new ProfilController(_userManager.Object, _db, NullLogger<ProfilController>.Instance);
            ControllerSetup.WithUser(_controller, _user.Email, _user.Id);
        }

        public void Dispose() => _db.Dispose();

        private void BezPrijavljenogKorisnika() =>
            _userManager.Setup(m => m.GetUserAsync(It.IsAny<ClaimsPrincipal>())).ReturnsAsync((ApplicationUser?)null);

        [Fact]
        public async Task Index_KorisnikNijePrijavljen_PreusmjeravaNaLogin()
        {
            BezPrijavljenogKorisnika();

            var result = await _controller.Index();

            Assert.Equal("Login", Assert.IsType<RedirectToActionResult>(result).ActionName);
        }

        [Fact]
        public async Task Index_PrijavljenKorisnik_VracaNjegoveRezervacijeIPlacene()
        {
            var rez = TestDb.AddRezervacija(_db, _user, TestDb.AddTermin(_db, _soba, DateTime.UtcNow.AddDays(3)));
            TestDb.AddRezervacija(_db, TestDb.AddUser(_db, "drugi@test.com"), TestDb.AddTermin(_db, _soba, DateTime.UtcNow.AddDays(4)));
            _db.Placanja.Add(new Placanje { RezervacijaID = rez.RezervacijaID, Iznos = 50, Datum = DateTime.UtcNow, Status = true });
            _db.SaveChanges();

            var result = await _controller.Index();

            var view = Assert.IsType<ViewResult>(result);
            Assert.Single(Assert.IsAssignableFrom<List<Rezervacija>>(view.Model));
            Assert.Contains(rez.RezervacijaID, Assert.IsType<List<int>>(view.ViewData["PlaceneRezervacije"]));
            Assert.Same(_user, view.ViewData["User"]);
        }

        [Fact]
        public async Task Details_RezervacijaNePostoji_VracaNotFound()
        {
            var result = await _controller.Details(999);

            Assert.IsType<NotFoundResult>(result);
        }

        [Fact]
        public async Task Details_TudjaRezervacija_VracaUnauthorized()
        {
            var rez = TestDb.AddRezervacija(_db, TestDb.AddUser(_db, "drugi@test.com"), TestDb.AddTermin(_db, _soba, DateTime.UtcNow.AddDays(3)));

            var result = await _controller.Details(rez.RezervacijaID);

            Assert.IsType<UnauthorizedResult>(result);
        }

        [Fact]
        public async Task Details_VlastitaRezervacija_VracaView()
        {
            var rez = TestDb.AddRezervacija(_db, _user, TestDb.AddTermin(_db, _soba, DateTime.UtcNow.AddDays(3)));

            var result = await _controller.Details(rez.RezervacijaID);

            Assert.Same(rez, Assert.IsType<ViewResult>(result).Model);
        }

        [Fact]
        public async Task Cancel_RezervacijaNePostoji_VracaNotFound()
        {
            var result = await _controller.Cancel(999);

            Assert.IsType<NotFoundResult>(result);
        }

        [Fact]
        public async Task Cancel_KorisnikNijePrijavljen_VracaUnauthorized()
        {
            var rez = TestDb.AddRezervacija(_db, _user, TestDb.AddTermin(_db, _soba, DateTime.UtcNow.AddDays(3)));
            BezPrijavljenogKorisnika();

            var result = await _controller.Cancel(rez.RezervacijaID);

            Assert.IsType<UnauthorizedResult>(result);
        }

        [Fact]
        public async Task Cancel_ManjeOd24SataDoTermina_VracaBadRequest()
        {
            var rez = TestDb.AddRezervacija(_db, _user, TestDb.AddTermin(_db, _soba, DateTime.UtcNow.AddHours(5)));

            var result = await _controller.Cancel(rez.RezervacijaID);

            Assert.IsType<BadRequestResult>(result);
            Assert.True(rez.Status);
        }

        [Fact]
        public async Task Cancel_ViseOd24SataDoTermina_OtkazujeIOslobadjaTermin()
        {
            var termin = TestDb.AddTermin(_db, _soba, DateTime.UtcNow.AddDays(3), dostupnost: false);
            var rez = TestDb.AddRezervacija(_db, _user, termin);

            var result = await _controller.Cancel(rez.RezervacijaID);

            Assert.Equal("Index", Assert.IsType<RedirectToActionResult>(result).ActionName);
            Assert.False(rez.Status);
            Assert.True(termin.Dostupnost);
        }
    }
}
