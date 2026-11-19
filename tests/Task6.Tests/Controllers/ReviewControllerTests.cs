using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Task6.Controllers;
using Task6.Data;
using Task6.Models;
using Task6.Tests.Helpers;

namespace Task6.Tests.Controllers
{
    public class ReviewControllerTests : IDisposable
    {
        private readonly EscapeRoomDbContext _db;
        private readonly Mock<UserManager<ApplicationUser>> _userManager;
        private readonly ReviewController _controller;
        private readonly ApplicationUser _user;
        private readonly EscapeRoom _soba;

        public ReviewControllerTests()
        {
            _db = TestDb.Create();
            _user = TestDb.AddUser(_db, "korisnik@test.com");
            _soba = TestDb.AddRoom(_db);
            _userManager = ControllerSetup.CreateUserManagerMock();
            _userManager.Setup(m => m.GetUserAsync(It.IsAny<ClaimsPrincipal>())).ReturnsAsync(_user);
            _controller = new ReviewController(_db, _userManager.Object);
            ControllerSetup.WithUser(_controller, _user.Email, _user.Id);
        }

        public void Dispose() => _db.Dispose();

        private void BezPrijavljenogKorisnika() =>
            _userManager.Setup(m => m.GetUserAsync(It.IsAny<ClaimsPrincipal>())).ReturnsAsync((ApplicationUser?)null);

        private void DodajZavrsenuRezervaciju() =>
            TestDb.AddRezervacija(_db, _user, TestDb.AddTermin(_db, _soba, DateTime.UtcNow.AddDays(-3)));

        private Recenzija DodajRecenziju(ApplicationUser autor, double ocjena = 4)
        {
            var recenzija = new Recenzija { KorisnikID = autor.Id, RoomID = _soba.RoomID, Ocjena = ocjena, Komentar = "Super", Datum = DateTime.UtcNow };
            _db.Recenzije.Add(recenzija);
            _db.SaveChanges();
            return recenzija;
        }

        // ---------- List ----------

        [Fact]
        public async Task List_SobaNePostoji_VracaNotFound()
        {
            var result = await _controller.List(999);

            Assert.IsType<NotFoundResult>(result);
        }

        [Fact]
        public async Task List_SobaBezRecenzija_ProsjecnaOcjenaJeNula()
        {
            var result = await _controller.List(_soba.RoomID);

            var view = Assert.IsType<ViewResult>(result);
            Assert.Equal(0.0, Convert.ToDouble(view.ViewData["AverageRating"]));
        }

        [Fact]
        public async Task List_SobaSaRecenzijama_RacunaProsjecnuOcjenu()
        {
            DodajRecenziju(_user, 5);
            DodajRecenziju(TestDb.AddUser(_db, "drugi@test.com"), 3);

            var result = await _controller.List(_soba.RoomID);

            var view = Assert.IsType<ViewResult>(result);
            Assert.Equal(4.0, Convert.ToDouble(view.ViewData["AverageRating"]));
            Assert.Equal(2, Assert.IsAssignableFrom<List<Recenzija>>(view.Model).Count);
        }

        // ---------- GET Create ----------

        [Fact]
        public async Task CreateGet_SobaNePostoji_VracaNotFound()
        {
            var result = await _controller.Create(999);

            Assert.IsType<NotFoundResult>(result);
        }

        [Fact]
        public async Task CreateGet_KorisnikNijePrijavljen_PreusmjeravaNaLogin()
        {
            BezPrijavljenogKorisnika();

            var result = await _controller.Create(_soba.RoomID);

            Assert.Equal("Login", Assert.IsType<RedirectToActionResult>(result).ActionName);
        }

        [Fact]
        public async Task CreateGet_VecPostojiRecenzija_PreusmjeravaNaListuSaPorukom()
        {
            DodajRecenziju(_user);

            var result = await _controller.Create(_soba.RoomID);

            Assert.Equal("List", Assert.IsType<RedirectToActionResult>(result).ActionName);
            Assert.Equal("Već ste dali recenziju za ovu sobu!", _controller.TempData["Message"]);
        }

        [Fact]
        public async Task CreateGet_BezZavrseneRezervacije_PreusmjeravaSaGreskom()
        {
            var result = await _controller.Create(_soba.RoomID);

            Assert.IsType<RedirectToActionResult>(result);
            Assert.NotNull(_controller.TempData["Error"]);
        }

        [Fact]
        public async Task CreateGet_ZavrsenaRezervacija_VracaFormu()
        {
            DodajZavrsenuRezervaciju();

            var result = await _controller.Create(_soba.RoomID);

            var view = Assert.IsType<ViewResult>(result);
            Assert.Same(_soba, view.ViewData["Room"]);
        }

        // ---------- POST Create ----------

        [Fact]
        public async Task CreatePost_KorisnikNijePrijavljen_PreusmjeravaNaLogin()
        {
            BezPrijavljenogKorisnika();

            var result = await _controller.Create(_soba.RoomID, new Recenzija());

            Assert.Equal("Login", Assert.IsType<RedirectToActionResult>(result).ActionName);
        }

        [Fact]
        public async Task CreatePost_SobaNePostoji_VracaNotFound()
        {
            var result = await _controller.Create(999, new Recenzija());

            Assert.IsType<NotFoundResult>(result);
        }

        [Fact]
        public async Task CreatePost_BezZavrseneRezervacije_NeSpremaRecenziju()
        {
            var result = await _controller.Create(_soba.RoomID, new Recenzija { Ocjena = 5, Komentar = "Odlično" });

            Assert.IsType<RedirectToActionResult>(result);
            Assert.Empty(_db.Recenzije);
        }

        [Fact]
        public async Task CreatePost_DuplaRecenzija_NeSpremaNovu()
        {
            DodajZavrsenuRezervaciju();
            DodajRecenziju(_user);

            await _controller.Create(_soba.RoomID, new Recenzija { Ocjena = 5, Komentar = "Opet" });

            Assert.Single(_db.Recenzije);
            Assert.Equal("Već ste ostavili recenziju za ovu sobu.", _controller.TempData["Error"]);
        }

        [Fact]
        public async Task CreatePost_ValidnaRecenzija_SpremaSaPodacimaKorisnikaISobe()
        {
            DodajZavrsenuRezervaciju();

            var result = await _controller.Create(_soba.RoomID, new Recenzija { Ocjena = 5, Komentar = "Odlično" });

            Assert.Equal("List", Assert.IsType<RedirectToActionResult>(result).ActionName);
            var recenzija = Assert.Single(_db.Recenzije);
            Assert.Equal(_user.Id, recenzija.KorisnikID);
            Assert.Equal(_soba.RoomID, recenzija.RoomID);
        }

        [Fact]
        public async Task CreatePost_NevalidanModel_VracaFormuBezSpremanja()
        {
            DodajZavrsenuRezervaciju();
            _controller.ModelState.AddModelError("Komentar", "Komentar je obavezan.");
            var model = new Recenzija { Ocjena = 5 };

            var result = await _controller.Create(_soba.RoomID, model);

            Assert.Same(model, Assert.IsType<ViewResult>(result).Model);
            Assert.Empty(_db.Recenzije);
        }

        // ---------- Delete ----------

        [Fact]
        public async Task Delete_RecenzijaNePostoji_VracaNotFound()
        {
            var result = await _controller.Delete(999);

            Assert.IsType<NotFoundResult>(result);
        }

        [Fact]
        public async Task Delete_TudjaRecenzijaObicanKorisnik_VracaForbid()
        {
            var recenzija = DodajRecenziju(TestDb.AddUser(_db, "drugi@test.com"));

            var result = await _controller.Delete(recenzija.RecenzijaID);

            Assert.IsType<ForbidResult>(result);
            Assert.Single(_db.Recenzije);
        }

        [Fact]
        public async Task Delete_VlastitaRecenzija_BriseRecenziju()
        {
            var recenzija = DodajRecenziju(_user);

            var result = await _controller.Delete(recenzija.RecenzijaID);

            Assert.Equal("List", Assert.IsType<RedirectToActionResult>(result).ActionName);
            Assert.Empty(_db.Recenzije);
        }

        [Fact]
        public async Task Delete_AdminBriseTudjuRecenziju_BriseRecenziju()
        {
            var recenzija = DodajRecenziju(TestDb.AddUser(_db, "drugi@test.com"));
            _controller.ControllerContext.HttpContext.User = new ClaimsPrincipal(
                new ClaimsIdentity(new[] { new Claim(ClaimTypes.Role, "Admin") }, "TestAuth"));

            await _controller.Delete(recenzija.RecenzijaID);

            Assert.Empty(_db.Recenzije);
        }

        // ---------- MyReviews ----------

        [Fact]
        public async Task MyReviews_KorisnikNijePrijavljen_PreusmjeravaNaLogin()
        {
            BezPrijavljenogKorisnika();

            var result = await _controller.MyReviews();

            Assert.Equal("Login", Assert.IsType<RedirectToActionResult>(result).ActionName);
        }

        [Fact]
        public async Task MyReviews_PrijavljenKorisnik_VracaSamoNjegoveRecenzije()
        {
            DodajRecenziju(_user);
            DodajRecenziju(TestDb.AddUser(_db, "drugi@test.com"));

            var result = await _controller.MyReviews();

            var model = Assert.IsAssignableFrom<List<Recenzija>>(Assert.IsType<ViewResult>(result).Model);
            Assert.All(model, r => Assert.Equal(_user.Id, r.KorisnikID));
            Assert.Single(model);
        }
    }
}
