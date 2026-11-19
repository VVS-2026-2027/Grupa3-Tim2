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
    public class PopustiControllerTests : IDisposable
    {
        private readonly EscapeRoomDbContext _db;
        private readonly Mock<IEmailService> _email;
        private readonly PopustiController _controller;

        public PopustiControllerTests()
        {
            _db = TestDb.Create();
            _email = new Mock<IEmailService>();
            _controller = new PopustiController(_db, _email.Object);
        }

        public void Dispose() => _db.Dispose();

        private void PrijaviKao(Uloga uloga)
        {
            var user = TestDb.AddUser(_db, $"{uloga}@test.com".ToLower(), uloga);
            ControllerSetup.WithUser(_controller, user.Email);
        }

        private static RoomDiscountViewModel Popust(int roomId, int procenat = 20) => new()
        {
            EscapeRoomId = roomId,
            DiscountPercent = procenat,
            StartDate = DateTime.UtcNow.Date,
            EndDate = DateTime.UtcNow.Date.AddDays(7)
        };

        // ---------- Kontrola pristupa ----------

        [Fact]
        public async Task Index_AnonimniKorisnik_PreusmjeravaNaPocetnu()
        {
            ControllerSetup.WithUser(_controller, null);

            var result = await _controller.Index();

            var redirect = Assert.IsType<RedirectToActionResult>(result);
            Assert.Equal("Index", redirect.ActionName);
            Assert.Equal("Home", redirect.ControllerName);
        }

        [Fact]
        public async Task Index_ObicanKorisnik_PreusmjeravaNaPocetnu()
        {
            PrijaviKao(Uloga.Korisnik);

            var result = await _controller.Index();

            Assert.Equal("Home", Assert.IsType<RedirectToActionResult>(result).ControllerName);
        }

        [Fact]
        public async Task Index_EmailNePostojiUBazi_PreusmjeravaNaPocetnu()
        {
            ControllerSetup.WithUser(_controller, "nepostojeci@test.com");

            var result = await _controller.Index();

            Assert.IsType<RedirectToActionResult>(result);
        }

        [Theory]
        [InlineData(Uloga.Admin)]
        [InlineData(Uloga.Zaposlenik)]
        public async Task Index_AdminIliZaposlenik_VracaSamoObavijestiTipaPopust(Uloga uloga)
        {
            PrijaviKao(uloga);
            _db.Obavijesti.AddRange(
                new Obavijest { Naslov = "Popust", Sadrzaj = "20%", Datum = DateTime.UtcNow, TipObavijesti = TipObavijesti.Popust },
                new Obavijest { Naslov = "Novost", Sadrzaj = "Nova soba", Datum = DateTime.UtcNow, TipObavijesti = TipObavijesti.Novost });
            _db.SaveChanges();

            var result = await _controller.Index();

            var model = Assert.IsAssignableFrom<List<Obavijest>>(Assert.IsType<ViewResult>(result).Model);
            Assert.Single(model);
            Assert.Equal(TipObavijesti.Popust, model[0].TipObavijesti);
        }

        // ---------- Create ----------

        [Fact]
        public async Task CreateGet_ObicanKorisnik_PreusmjeravaNaPocetnu()
        {
            PrijaviKao(Uloga.Korisnik);

            var result = await _controller.Create();

            Assert.IsType<RedirectToActionResult>(result);
        }

        [Fact]
        public async Task CreateGet_Admin_VracaPrazanModel()
        {
            PrijaviKao(Uloga.Admin);

            var result = await _controller.Create();

            Assert.IsType<PopustViewModel>(Assert.IsType<ViewResult>(result).Model);
        }

        [Fact]
        public async Task CreatePost_ObicanKorisnik_NeKreiraObavijest()
        {
            PrijaviKao(Uloga.Korisnik);

            var result = await _controller.Create(new PopustViewModel { Naslov = "X", Sadrzaj = "Y" });

            Assert.IsType<RedirectToActionResult>(result);
            Assert.Empty(_db.Obavijesti);
        }

        [Fact]
        public async Task CreatePost_NevalidanModel_VracaViewBezSpremanja()
        {
            PrijaviKao(Uloga.Admin);
            _controller.ModelState.AddModelError("Naslov", "Obavezno");
            var model = new PopustViewModel();

            var result = await _controller.Create(model);

            Assert.Same(model, Assert.IsType<ViewResult>(result).Model);
            Assert.Empty(_db.Obavijesti);
        }

        [Fact]
        public async Task CreatePost_ValidanModel_KreiraObavijestTipaPopust()
        {
            PrijaviKao(Uloga.Zaposlenik);

            var result = await _controller.Create(new PopustViewModel { Naslov = "Black Friday", Sadrzaj = "30% na sve sobe" });

            Assert.Equal("Index", Assert.IsType<RedirectToActionResult>(result).ActionName);
            var obavijest = Assert.Single(_db.Obavijesti);
            Assert.Equal(TipObavijesti.Popust, obavijest.TipObavijesti);
            Assert.Equal("Popust je kreiran i korisnici su obaviješteni.", _controller.TempData["SuccessMessage"]);
        }

        [Fact]
        public async Task CreatePost_ValidanModel_SaljeEmailSvakomKorisnikuSaEmailom()
        {
            PrijaviKao(Uloga.Admin);
            TestDb.AddUser(_db, "a@test.com");
            TestDb.AddUser(_db, "b@test.com");

            await _controller.Create(new PopustViewModel { Naslov = "Akcija", Sadrzaj = "Popust" });

            _email.Verify(e => e.SendEmailAsync(It.IsAny<string>(), "The Last Key - Akcija", It.IsAny<string>()), Times.Exactly(3));
        }

        // ---------- CreateRoomDiscount ----------

        [Fact]
        public async Task CreateRoomDiscountGet_ObicanKorisnik_PreusmjeravaNaPocetnu()
        {
            PrijaviKao(Uloga.Korisnik);

            var result = await _controller.CreateRoomDiscount(1);

            Assert.IsType<RedirectToActionResult>(result);
        }

        [Fact]
        public async Task CreateRoomDiscountGet_SobaNePostoji_VracaNotFound()
        {
            PrijaviKao(Uloga.Admin);

            var result = await _controller.CreateRoomDiscount(999);

            Assert.IsType<NotFoundResult>(result);
        }

        [Fact]
        public async Task CreateRoomDiscountGet_SobaPostoji_PopunjavaNazivSobe()
        {
            PrijaviKao(Uloga.Admin);
            var soba = TestDb.AddRoom(_db, "Zatvor");

            var result = await _controller.CreateRoomDiscount(soba.RoomID);

            var model = Assert.IsType<RoomDiscountViewModel>(Assert.IsType<ViewResult>(result).Model);
            Assert.Equal("Zatvor", model.EscapeRoomName);
        }

        [Fact]
        public async Task CreateRoomDiscountPost_ObicanKorisnik_PreusmjeravaNaPocetnu()
        {
            PrijaviKao(Uloga.Korisnik);

            var result = await _controller.CreateRoomDiscount(Popust(1));

            Assert.Equal("Home", Assert.IsType<RedirectToActionResult>(result).ControllerName);
        }

        [Fact]
        public async Task CreateRoomDiscountPost_KrajPrijePocetka_VracaViewSaGreskom()
        {
            PrijaviKao(Uloga.Admin);
            var soba = TestDb.AddRoom(_db, "Zatvor");
            var model = Popust(soba.RoomID);
            model.EndDate = model.StartDate!.Value.AddDays(-1);

            var result = await _controller.CreateRoomDiscount(model);

            Assert.IsType<ViewResult>(result);
            Assert.True(_controller.ModelState.ContainsKey("EndDate"));
            Assert.Equal("Zatvor", model.EscapeRoomName);
            Assert.Equal(0, soba.DiscountPercent);
        }

        [Fact]
        public async Task CreateRoomDiscountPost_NevalidanModelBezSobe_VracaViewBezNazivaSobe()
        {
            PrijaviKao(Uloga.Admin);
            _controller.ModelState.AddModelError("DiscountPercent", "Greška");
            var model = Popust(0);

            var result = await _controller.CreateRoomDiscount(model);

            Assert.IsType<ViewResult>(result);
            Assert.Equal(string.Empty, model.EscapeRoomName);
        }

        [Fact]
        public async Task CreateRoomDiscountPost_NevalidanModelNepostojecaSoba_VracaView()
        {
            PrijaviKao(Uloga.Admin);
            _controller.ModelState.AddModelError("DiscountPercent", "Greška");

            var result = await _controller.CreateRoomDiscount(Popust(999));

            Assert.IsType<ViewResult>(result);
        }

        [Fact]
        public async Task CreateRoomDiscountPost_SobaNePostoji_VracaNotFound()
        {
            PrijaviKao(Uloga.Admin);

            var result = await _controller.CreateRoomDiscount(Popust(999));

            Assert.IsType<NotFoundResult>(result);
        }

        [Fact]
        public async Task CreateRoomDiscountPost_Admin_SpremaPopustIVracaNaAdminSobe()
        {
            PrijaviKao(Uloga.Admin);
            var soba = TestDb.AddRoom(_db);

            var result = await _controller.CreateRoomDiscount(Popust(soba.RoomID, 25));

            var redirect = Assert.IsType<RedirectToActionResult>(result);
            Assert.Equal("Rooms", redirect.ActionName);
            Assert.Equal("Admin", redirect.ControllerName);
            Assert.Equal(25, soba.DiscountPercent);
            Assert.Equal(DateTimeKind.Utc, soba.DiscountStart!.Value.Kind);
        }

        [Fact]
        public async Task CreateRoomDiscountPost_Zaposlenik_VracaNaZaposlenikSobe()
        {
            PrijaviKao(Uloga.Zaposlenik);
            var soba = TestDb.AddRoom(_db);

            var result = await _controller.CreateRoomDiscount(Popust(soba.RoomID));

            Assert.Equal("Zaposlenik", Assert.IsType<RedirectToActionResult>(result).ControllerName);
        }
    }
}
