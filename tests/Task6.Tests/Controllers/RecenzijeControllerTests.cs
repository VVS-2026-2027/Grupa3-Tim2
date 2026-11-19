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
    public class RecenzijeControllerTests : IDisposable
    {
        private readonly EscapeRoomDbContext _db;
        private readonly Mock<UserManager<ApplicationUser>> _userManager;
        private readonly RecenzijeController _controller;
        private readonly ApplicationUser _user;
        private readonly EscapeRoom _soba;

        public RecenzijeControllerTests()
        {
            _db = TestDb.Create();
            _user = TestDb.AddUser(_db, "korisnik@test.com");
            _soba = TestDb.AddRoom(_db);
            _userManager = ControllerSetup.CreateUserManagerMock();
            _userManager.Setup(m => m.GetUserAsync(It.IsAny<ClaimsPrincipal>())).ReturnsAsync(_user);
            _controller = new RecenzijeController(_db, _userManager.Object);
            ControllerSetup.WithUser(_controller, _user.Email, _user.Id);
        }

        public void Dispose() => _db.Dispose();

        [Fact]
        public void Index_PostojeRecenzije_VracaSveRecenzije()
        {
            _db.Recenzije.Add(new Recenzija { KorisnikID = _user.Id, RoomID = _soba.RoomID, Ocjena = 4, Komentar = "Dobro" });
            _db.SaveChanges();

            var result = _controller.Index();

            Assert.Single(Assert.IsAssignableFrom<List<Recenzija>>(Assert.IsType<ViewResult>(result).Model));
        }

        [Fact]
        public void CreateGet_PozivAkcije_VracaView()
        {
            var result = _controller.Create();

            Assert.IsType<ViewResult>(result);
        }

        [Fact]
        public async Task CreatePost_KorisnikNijePrijavljen_VracaUnauthorized()
        {
            _userManager.Setup(m => m.GetUserAsync(It.IsAny<ClaimsPrincipal>())).ReturnsAsync((ApplicationUser?)null);

            var result = await _controller.Create(_soba.RoomID, 5, "Super");

            Assert.IsType<UnauthorizedResult>(result);
        }

        [Fact]
        public async Task CreatePost_BezZavrseneRezervacije_VracaBadRequest()
        {
            TestDb.AddRezervacija(_db, _user, TestDb.AddTermin(_db, _soba, DateTime.Now.AddDays(3)));

            var result = await _controller.Create(_soba.RoomID, 5, "Super");

            Assert.IsType<BadRequestObjectResult>(result);
            Assert.Empty(_db.Recenzije);
        }

        [Fact]
        public async Task CreatePost_ZavrsenaRezervacija_DodajeRecenzijuIVracaOk()
        {
            TestDb.AddRezervacija(_db, _user, TestDb.AddTermin(_db, _soba, DateTime.Now.AddDays(-3)));

            var result = await _controller.Create(_soba.RoomID, 4.5, "Odlična soba");

            Assert.IsType<OkObjectResult>(result);
            var recenzija = Assert.Single(_db.Recenzije);
            Assert.Equal(4.5, recenzija.Ocjena);
        }

        [Fact]
        public async Task Delete_RecenzijaNePostoji_VracaNotFound()
        {
            var result = await _controller.Delete(999);

            Assert.IsType<NotFoundResult>(result);
        }

        [Fact]
        public async Task Delete_RecenzijaPostoji_BriseIVracaOk()
        {
            var recenzija = new Recenzija { KorisnikID = _user.Id, RoomID = _soba.RoomID, Ocjena = 2, Komentar = "Loše" };
            _db.Recenzije.Add(recenzija);
            _db.SaveChanges();

            var result = await _controller.Delete(recenzija.RecenzijaID);

            Assert.IsType<OkObjectResult>(result);
            Assert.Empty(_db.Recenzije);
        }
    }
}
