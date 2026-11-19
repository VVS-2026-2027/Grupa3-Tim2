using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Task6.Controllers;
using Task6.Data;
using Task6.Models;
using Task6.Services;
using Task6.Tests.Helpers;

namespace Task6.Tests.Controllers
{
    public class PaymentControllerTests : IDisposable
    {
        private const string IspravnaKartica = "4111 1111 1111 1111";
        private const string IspravnoIme = "Lamija Maglic";
        private const string IspravanDatum = "12/35";
        private const string IspravanCvv = "123";

        private readonly EscapeRoomDbContext _db;
        private readonly Mock<UserManager<ApplicationUser>> _userManager;
        private readonly Mock<IEmailService> _email;
        private readonly PaymentController _controller;
        private readonly ApplicationUser _user;
        private readonly Rezervacija _rezervacija;

        public PaymentControllerTests()
        {
            _db = TestDb.Create();
            _user = TestDb.AddUser(_db, "korisnik@test.com");
            var soba = TestDb.AddRoom(_db, cijena: 30);
            _rezervacija = TestDb.AddRezervacija(_db, _user, TestDb.AddTermin(_db, soba, DateTime.UtcNow.AddDays(2)), brojOsoba: 3);

            _userManager = ControllerSetup.CreateUserManagerMock();
            _userManager.Setup(m => m.GetUserAsync(It.IsAny<ClaimsPrincipal>())).ReturnsAsync(_user);
            _email = new Mock<IEmailService>();
            _controller = new PaymentController(_db, _userManager.Object, _email.Object);
            ControllerSetup.WithUser(_controller, _user.Email, _user.Id);
        }

        public void Dispose() => _db.Dispose();

        public static IEnumerable<object[]> NeispravniPodaciKartice() => TestData.FromCsv("kartice.csv");

        private static string? PrvaGreska(PaymentController controller) =>
            controller.ModelState[string.Empty]?.Errors.FirstOrDefault()?.ErrorMessage;

        // ---------- GET Pay ----------

        [Fact]
        public async Task PayGet_KorisnikNijePrijavljen_PreusmjeravaNaLogin()
        {
            _userManager.Setup(m => m.GetUserAsync(It.IsAny<ClaimsPrincipal>())).ReturnsAsync((ApplicationUser?)null);

            var result = await _controller.Pay(_rezervacija.RezervacijaID);

            Assert.Equal("Login", Assert.IsType<RedirectToActionResult>(result).ActionName);
        }

        [Fact]
        public async Task PayGet_RezervacijaNePostoji_VracaNotFound()
        {
            var result = await _controller.Pay(999);

            Assert.IsType<NotFoundResult>(result);
        }

        [Fact]
        public async Task PayGet_OtkazanaRezervacija_PreusmjeravaSaGreskom()
        {
            _rezervacija.Status = false;
            _db.SaveChanges();

            var result = await _controller.Pay(_rezervacija.RezervacijaID);

            Assert.Equal("Index", Assert.IsType<RedirectToActionResult>(result).ActionName);
            Assert.Equal("Otkazana rezervacija se ne može platiti.", _controller.TempData["PaymentError"]);
        }

        [Fact]
        public async Task PayGet_RezervacijaVecPlacena_PreusmjeravaSaPorukom()
        {
            _db.Placanja.Add(new Placanje { RezervacijaID = _rezervacija.RezervacijaID, Iznos = 90, Datum = DateTime.UtcNow, Status = true });
            _db.SaveChanges();

            await _controller.Pay(_rezervacija.RezervacijaID);

            Assert.Equal("Ova rezervacija je već plaćena.", _controller.TempData["PaymentInfo"]);
        }

        [Fact]
        public async Task PayGet_NeplacenaRezervacija_RacunaIznosPremaBrojuOsoba()
        {
            var result = await _controller.Pay(_rezervacija.RezervacijaID);

            var view = Assert.IsType<ViewResult>(result);
            Assert.Equal(90.0, view.ViewData["Iznos"]);
        }

        // ---------- POST Pay ----------

        [Fact]
        public async Task PayPost_KorisnikNijePrijavljen_PreusmjeravaNaLogin()
        {
            _userManager.Setup(m => m.GetUserAsync(It.IsAny<ClaimsPrincipal>())).ReturnsAsync((ApplicationUser?)null);

            var result = await _controller.Pay(_rezervacija.RezervacijaID, IspravnaKartica, IspravnoIme, IspravanDatum, IspravanCvv);

            Assert.Equal("Login", Assert.IsType<RedirectToActionResult>(result).ActionName);
        }

        [Fact]
        public async Task PayPost_RezervacijaNePostoji_VracaNotFound()
        {
            var result = await _controller.Pay(999, IspravnaKartica, IspravnoIme, IspravanDatum, IspravanCvv);

            Assert.IsType<NotFoundResult>(result);
        }

        [Fact]
        public async Task PayPost_OtkazanaRezervacija_NeKreiraPlacanje()
        {
            _rezervacija.Status = false;
            _db.SaveChanges();

            await _controller.Pay(_rezervacija.RezervacijaID, IspravnaKartica, IspravnoIme, IspravanDatum, IspravanCvv);

            Assert.Empty(_db.Placanja);
            Assert.Equal("Otkazana rezervacija se ne može platiti.", _controller.TempData["PaymentError"]);
        }

        [Theory]
        [MemberData(nameof(NeispravniPodaciKartice))]
        public async Task PayPost_NeispravniPodaciIzCsv_VracaViewSaGreskom(string brojKartice, string ime, string datum, string cvv, string ocekivanaGreska)
        {
            var result = await _controller.Pay(_rezervacija.RezervacijaID, brojKartice, ime, datum, cvv);

            Assert.IsType<ViewResult>(result);
            Assert.Equal(ocekivanaGreska, PrvaGreska(_controller));
            Assert.Empty(_db.Placanja);
        }

        [Fact]
        public async Task PayPost_IspravniPodaci_KreiraPlacanjeSaIspravnimIznosom()
        {
            var result = await _controller.Pay(_rezervacija.RezervacijaID, IspravnaKartica, IspravnoIme, IspravanDatum, IspravanCvv);

            Assert.Equal("Index", Assert.IsType<RedirectToActionResult>(result).ActionName);
            var placanje = Assert.Single(_db.Placanja);
            Assert.Equal(90, placanje.Iznos);
            Assert.True(placanje.Status);
            Assert.Equal("Plaćanje je uspješno izvršeno.", _controller.TempData["PaymentSuccess"]);
        }

        [Fact]
        public async Task PayPost_PostojeceNeuspjesnoPlacanje_AzuriraPostojeceUmjestoNovog()
        {
            _db.Placanja.Add(new Placanje { RezervacijaID = _rezervacija.RezervacijaID, Iznos = 1, Datum = DateTime.UtcNow.AddDays(-1), Status = false });
            _db.SaveChanges();

            await _controller.Pay(_rezervacija.RezervacijaID, IspravnaKartica, IspravnoIme, IspravanDatum, IspravanCvv);

            var placanje = Assert.Single(_db.Placanja);
            Assert.True(placanje.Status);
            Assert.Equal(90, placanje.Iznos);
        }

        [Fact]
        public async Task PayPost_IspravniPodaci_SaljeEmailPotvrdePlacanja()
        {
            await _controller.Pay(_rezervacija.RezervacijaID, IspravnaKartica, IspravnoIme, IspravanDatum, IspravanCvv);

            _email.Verify(e => e.SendEmailAsync("korisnik@test.com", "The Last Key - Potvrda plaćanja", It.Is<string>(s => s.Contains("90"))), Times.Once);
        }

        [Fact]
        public async Task PayPost_KorisnikBezEmaila_NeSaljeEmail()
        {
            _user.Email = null;

            await _controller.Pay(_rezervacija.RezervacijaID, IspravnaKartica, IspravnoIme, IspravanDatum, IspravanCvv);

            _email.Verify(e => e.SendEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        }
    }
}
