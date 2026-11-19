using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Task6.Controllers;
using Task6.Models;
using Task6.Services;
using Task6.Tests.Helpers;
using Task6.ViewModels;
using SignInResult = Microsoft.AspNetCore.Identity.SignInResult;

namespace Task6.Tests.Controllers
{
    public class AccountControllerTests
    {
        private readonly Mock<UserManager<ApplicationUser>> _userManager;
        private readonly Mock<SignInManager<ApplicationUser>> _signInManager;
        private readonly Mock<IEmailService> _email;
        private readonly Mock<IUrlHelper> _url;
        private readonly AccountController _controller;
        private readonly ApplicationUser _user;

        public AccountControllerTests()
        {
            _user = new ApplicationUser { Id = "u1", UserName = "korisnik@test.com", Email = "korisnik@test.com", Ime = "Lamija", Prezime = "Test" };
            _userManager = ControllerSetup.CreateUserManagerMock();
            _signInManager = new Mock<SignInManager<ApplicationUser>>(
                _userManager.Object,
                Mock.Of<IHttpContextAccessor>(),
                Mock.Of<IUserClaimsPrincipalFactory<ApplicationUser>>(),
                null!, null!, null!, null!);
            _email = new Mock<IEmailService>();
            _url = new Mock<IUrlHelper>();
            _controller = new AccountController(_signInManager.Object, _userManager.Object, NullLogger<AccountController>.Instance, _email.Object)
            {
                Url = _url.Object
            };
            ControllerSetup.WithUser(_controller, null);
        }

        private static string? PrvaGreska(Controller controller) =>
            controller.ModelState[string.Empty]?.Errors.FirstOrDefault()?.ErrorMessage;

        private static RegisterViewModel Registracija() => new()
        {
            Email = "novi@test.com",
            Ime = "Novi",
            Prezime = "Korisnik",
            Password = "Lozinka1!",
            ConfirmPassword = "Lozinka1!"
        };

        private static LoginViewModel Prijava() => new() { Email = "korisnik@test.com", Password = "Lozinka1!" };

        // ---------- Register ----------

        [Fact]
        public void RegisterGet_VecPrijavljen_PreusmjeravaNaPocetnu()
        {
            ControllerSetup.WithUser(_controller, "korisnik@test.com");

            var result = _controller.Register();

            Assert.Equal("Home", Assert.IsType<RedirectToActionResult>(result).ControllerName);
        }

        [Fact]
        public void RegisterGet_AnonimniKorisnik_VracaFormu()
        {
            var result = _controller.Register();

            Assert.IsType<ViewResult>(result);
        }

        [Fact]
        public async Task RegisterPost_NevalidanModel_NeKreiraKorisnika()
        {
            _controller.ModelState.AddModelError("Email", "Email je obavezan");

            var result = await _controller.Register(new RegisterViewModel());

            Assert.IsType<ViewResult>(result);
            _userManager.Verify(m => m.CreateAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task RegisterPost_UspjesnaRegistracija_DodajeUloguKorisnikIPrijavljuje()
        {
            _userManager.Setup(m => m.CreateAsync(It.IsAny<ApplicationUser>(), "Lozinka1!")).ReturnsAsync(IdentityResult.Success);

            var result = await _controller.Register(Registracija());

            Assert.Equal("Home", Assert.IsType<RedirectToActionResult>(result).ControllerName);
            _userManager.Verify(m => m.AddToRoleAsync(It.Is<ApplicationUser>(u => u.Email == "novi@test.com" && u.Uloga == Uloga.Korisnik), "Korisnik"), Times.Once);
            _signInManager.Verify(s => s.SignInAsync(It.IsAny<ApplicationUser>(), false, null), Times.Once);
        }

        [Fact]
        public async Task RegisterPost_IdentityVratiGresku_PrikazujeGreskuUFormi()
        {
            _userManager.Setup(m => m.CreateAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>()))
                .ReturnsAsync(IdentityResult.Failed(new IdentityError { Description = "Email je zauzet." }));

            var result = await _controller.Register(Registracija());

            Assert.IsType<ViewResult>(result);
            Assert.Equal("Email je zauzet.", PrvaGreska(_controller));
        }

        [Fact]
        public async Task RegisterPost_BazaBaciIzuzetak_PrikazujePorukuOGresciBaze()
        {
            _userManager.Setup(m => m.CreateAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>()))
                .ThrowsAsync(new InvalidOperationException("Baza nije dostupna"));

            var result = await _controller.Register(Registracija());

            Assert.IsType<ViewResult>(result);
            Assert.StartsWith("Došlo je do problema sa bazom podataka", PrvaGreska(_controller));
        }

        // ---------- Login ----------

        [Fact]
        public void LoginGet_VecPrijavljen_PreusmjeravaNaPocetnu()
        {
            ControllerSetup.WithUser(_controller, "korisnik@test.com");

            var result = _controller.Login("/Room");

            Assert.IsType<RedirectToActionResult>(result);
        }

        [Fact]
        public void LoginGet_AnonimniKorisnik_PamtiReturnUrl()
        {
            var result = _controller.Login("/Room");

            Assert.Equal("/Room", Assert.IsType<ViewResult>(result).ViewData["ReturnUrl"]);
        }

        [Fact]
        public async Task LoginPost_NevalidanModel_VracaFormu()
        {
            _controller.ModelState.AddModelError("Email", "Email je obavezan");

            var result = await _controller.Login(new LoginViewModel());

            Assert.IsType<ViewResult>(result);
            _userManager.Verify(m => m.FindByEmailAsync(It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task LoginPost_KorisnikNePostoji_PrikazujeGreskuPrijave()
        {
            _userManager.Setup(m => m.FindByEmailAsync("korisnik@test.com")).ReturnsAsync((ApplicationUser?)null);

            await _controller.Login(Prijava());

            Assert.Equal("Pogrešna kombinacija korisničkog imena i lozinke.", PrvaGreska(_controller));
        }

        [Fact]
        public async Task LoginPost_BazaBaciIzuzetak_PrikazujePorukuOGresciBaze()
        {
            _userManager.Setup(m => m.FindByEmailAsync(It.IsAny<string>())).ThrowsAsync(new InvalidOperationException());

            await _controller.Login(Prijava());

            Assert.StartsWith("Došlo je do problema sa bazom podataka", PrvaGreska(_controller));
        }

        [Fact]
        public async Task LoginPost_UspjesnaPrijavaSaLokalnimUrlom_PreusmjeravaNaUrl()
        {
            _userManager.Setup(m => m.FindByEmailAsync("korisnik@test.com")).ReturnsAsync(_user);
            _signInManager.Setup(s => s.PasswordSignInAsync("korisnik@test.com", "Lozinka1!", false, false)).ReturnsAsync(SignInResult.Success);
            _url.Setup(u => u.IsLocalUrl("/Room")).Returns(true);

            var result = await _controller.Login(Prijava(), "/Room");

            Assert.Equal("/Room", Assert.IsType<RedirectResult>(result).Url);
        }

        [Fact]
        public async Task LoginPost_UspjesnaPrijavaSaVanjskimUrlom_PreusmjeravaNaPocetnu()
        {
            _userManager.Setup(m => m.FindByEmailAsync("korisnik@test.com")).ReturnsAsync(_user);
            _signInManager.Setup(s => s.PasswordSignInAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>(), false)).ReturnsAsync(SignInResult.Success);
            _url.Setup(u => u.IsLocalUrl("https://napad.com")).Returns(false);

            var result = await _controller.Login(Prijava(), "https://napad.com");

            Assert.Equal("Home", Assert.IsType<RedirectToActionResult>(result).ControllerName);
        }

        public static IEnumerable<object[]> NeuspjesnePrijave() => new[]
        {
            new object[] { SignInResult.LockedOut, "Račun je zaključan." },
            new object[] { SignInResult.TwoFactorRequired, "Za prijavu je potrebna dvostepena provjera." },
            new object[] { SignInResult.Failed, "Pogrešna kombinacija korisničkog imena i lozinke." }
        };

        [Theory]
        [MemberData(nameof(NeuspjesnePrijave))]
        public async Task LoginPost_NeuspjesnaPrijava_PrikazujeOdgovarajucuPoruku(SignInResult rezultat, string ocekivanaPoruka)
        {
            _userManager.Setup(m => m.FindByEmailAsync(It.IsAny<string>())).ReturnsAsync(_user);
            _signInManager.Setup(s => s.PasswordSignInAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>(), false)).ReturnsAsync(rezultat);

            var result = await _controller.Login(Prijava());

            Assert.IsType<ViewResult>(result);
            Assert.Equal(ocekivanaPoruka, PrvaGreska(_controller));
        }

        [Fact]
        public async Task Logout_PrijavljenKorisnik_OdjavljujeIPreusmjerava()
        {
            var result = await _controller.Logout();

            _signInManager.Verify(s => s.SignOutAsync(), Times.Once);
            Assert.Equal("Home", Assert.IsType<RedirectToActionResult>(result).ControllerName);
        }

        // ---------- ForgotPassword ----------

        [Fact]
        public void ForgotPasswordGet_SaEmailom_PopunjavaEmail()
        {
            var result = _controller.ForgotPasswordGet("korisnik@test.com");

            var view = Assert.IsType<ViewResult>(result);
            Assert.Equal("ForgotPassword", view.ViewName);
            Assert.Equal("korisnik@test.com", view.ViewData["Email"]);
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        public async Task ForgotPasswordPost_PrazanEmail_PrikazujeGresku(string email)
        {
            await _controller.ForgotPassword(email);

            Assert.Equal("Email je obavezan.", PrvaGreska(_controller));
        }

        [Fact]
        public async Task ForgotPasswordPost_BazaBaciIzuzetak_PrikazujePorukuOGresciBaze()
        {
            _userManager.Setup(m => m.FindByEmailAsync(It.IsAny<string>())).ThrowsAsync(new InvalidOperationException());

            await _controller.ForgotPassword("korisnik@test.com");

            Assert.StartsWith("Došlo je do problema sa bazom podataka", PrvaGreska(_controller));
        }

        [Fact]
        public async Task ForgotPasswordPost_KorisnikNePostoji_PrikazujeGresku()
        {
            await _controller.ForgotPassword("nepostojeci@test.com");

            Assert.Equal("Korisnički račun sa ovim emailom ne postoji.", PrvaGreska(_controller));
        }

        [Fact]
        public async Task ForgotPasswordPost_PostojeciKorisnik_GeneriseKodISaljeEmail()
        {
            _userManager.Setup(m => m.FindByEmailAsync("korisnik@test.com")).ReturnsAsync(_user);

            var result = await _controller.ForgotPassword("korisnik@test.com");

            Assert.Equal("VerifyResetCode", Assert.IsType<RedirectToActionResult>(result).ActionName);
            Assert.Matches("^[0-9]{6}$", _user.ResetCode);
            Assert.True(_user.ResetCodeExpiry > DateTime.UtcNow);
            _email.Verify(e => e.SendEmailAsync("korisnik@test.com", "The Last Key - Kod za promjenu lozinke", It.Is<string>(s => s.Contains(_user.ResetCode!))), Times.Once);
        }

        [Fact]
        public async Task ForgotPasswordPost_SlanjeEmailaNeuspjesno_PrikazujePorukuOGresci()
        {
            _userManager.Setup(m => m.FindByEmailAsync(It.IsAny<string>())).ReturnsAsync(_user);
            _email.Setup(e => e.SendEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                .ThrowsAsync(new InvalidOperationException("SMTP nije dostupan"));

            var result = await _controller.ForgotPassword("korisnik@test.com");

            Assert.IsType<ViewResult>(result);
            Assert.StartsWith("Došlo je do problema sa bazom podataka", PrvaGreska(_controller));
        }

        // ---------- VerifyResetCode ----------

        [Fact]
        public void VerifyResetCodeGet_EmailUTempData_PrikazujeEmail()
        {
            _controller.TempData["ResetEmail"] = "korisnik@test.com";

            var result = _controller.VerifyResetCode();

            Assert.Equal("korisnik@test.com", Assert.IsType<ViewResult>(result).ViewData["Email"]);
        }

        [Theory]
        [InlineData("", "123456")]
        [InlineData("korisnik@test.com", "")]
        public async Task VerifyResetCodePost_PrazanEmailIliKod_PrikazujeGresku(string email, string kod)
        {
            await _controller.VerifyResetCode(email, kod);

            Assert.Equal("Email i kod su obavezni.", PrvaGreska(_controller));
        }

        [Fact]
        public async Task VerifyResetCodePost_BazaBaciIzuzetak_PrikazujePorukuOGresciBaze()
        {
            _userManager.Setup(m => m.FindByEmailAsync(It.IsAny<string>())).ThrowsAsync(new InvalidOperationException());

            await _controller.VerifyResetCode("korisnik@test.com", "123456");

            Assert.StartsWith("Došlo je do problema sa bazom podataka", PrvaGreska(_controller));
        }

        [Theory]
        [InlineData("654321", 10)]
        [InlineData("123456", -1)]
        public async Task VerifyResetCodePost_PogresanIliIstekaoKod_PrikazujeGresku(string uneseniKod, int minutaDoIsteka)
        {
            _user.ResetCode = "123456";
            _user.ResetCodeExpiry = DateTime.UtcNow.AddMinutes(minutaDoIsteka);
            _userManager.Setup(m => m.FindByEmailAsync(It.IsAny<string>())).ReturnsAsync(_user);

            await _controller.VerifyResetCode("korisnik@test.com", uneseniKod);

            Assert.Equal("Neispravan ili istekao kod.", PrvaGreska(_controller));
        }

        [Fact]
        public async Task VerifyResetCodePost_IspravanKod_PreusmjeravaNaNovuLozinku()
        {
            _user.ResetCode = "123456";
            _user.ResetCodeExpiry = DateTime.UtcNow.AddMinutes(10);
            _userManager.Setup(m => m.FindByEmailAsync(It.IsAny<string>())).ReturnsAsync(_user);

            var result = await _controller.VerifyResetCode("korisnik@test.com", "123456");

            Assert.Equal("ResetNewPassword", Assert.IsType<RedirectToActionResult>(result).ActionName);
            Assert.Equal("123456", _controller.TempData["ResetValidatedCode"]);
        }

        // ---------- ResetNewPassword ----------

        [Fact]
        public void ResetNewPasswordGet_KodNijeProvjeren_PreusmjeravaNaZaboravljenuLozinku()
        {
            var result = _controller.ResetNewPassword();

            Assert.Equal("ForgotPassword", Assert.IsType<RedirectToActionResult>(result).ActionName);
        }

        [Fact]
        public void ResetNewPasswordGet_KodProvjeren_VracaFormu()
        {
            _controller.TempData["ResetValidatedCode"] = "123456";

            var result = _controller.ResetNewPassword();

            Assert.IsType<ViewResult>(result);
        }

        private void PostaviProvjerenKod()
        {
            _controller.TempData["ResetEmail"] = "korisnik@test.com";
            _controller.TempData["ResetValidatedCode"] = "123456";
            _user.ResetCode = "123456";
        }

        [Fact]
        public async Task ResetNewPasswordPost_NemaProvjerenogKoda_PreusmjeravaNaZaboravljenuLozinku()
        {
            var result = await _controller.ResetNewPassword("Nova1!", "Nova1!");

            Assert.Equal("ForgotPassword", Assert.IsType<RedirectToActionResult>(result).ActionName);
        }

        [Fact]
        public async Task ResetNewPasswordPost_LozinkeSeNePoklapaju_PrikazujeGresku()
        {
            PostaviProvjerenKod();

            await _controller.ResetNewPassword("Nova1!", "Druga1!");

            Assert.Equal("Lozinke se ne poklapaju.", PrvaGreska(_controller));
        }

        [Fact]
        public async Task ResetNewPasswordPost_KodSeNePoklapa_PreusmjeravaNaZaboravljenuLozinku()
        {
            PostaviProvjerenKod();
            _user.ResetCode = "999999";
            _userManager.Setup(m => m.FindByEmailAsync(It.IsAny<string>())).ReturnsAsync(_user);

            var result = await _controller.ResetNewPassword("Nova1!", "Nova1!");

            Assert.Equal("ForgotPassword", Assert.IsType<RedirectToActionResult>(result).ActionName);
        }

        [Fact]
        public async Task ResetNewPasswordPost_UspjesnaPromjena_BriseKodIPreusmjeravaNaLogin()
        {
            PostaviProvjerenKod();
            _userManager.Setup(m => m.FindByEmailAsync(It.IsAny<string>())).ReturnsAsync(_user);
            _userManager.Setup(m => m.GeneratePasswordResetTokenAsync(_user)).ReturnsAsync("token");
            _userManager.Setup(m => m.ResetPasswordAsync(_user, "token", "Nova1!")).ReturnsAsync(IdentityResult.Success);

            var result = await _controller.ResetNewPassword("Nova1!", "Nova1!");

            Assert.Equal("Login", Assert.IsType<RedirectToActionResult>(result).ActionName);
            Assert.Null(_user.ResetCode);
            Assert.Null(_user.ResetCodeExpiry);
            _userManager.Verify(m => m.UpdateAsync(_user), Times.Once);
        }

        [Fact]
        public async Task ResetNewPasswordPost_SlabaLozinka_PrikazujeGreskeIdentityja()
        {
            PostaviProvjerenKod();
            _userManager.Setup(m => m.FindByEmailAsync(It.IsAny<string>())).ReturnsAsync(_user);
            _userManager.Setup(m => m.GeneratePasswordResetTokenAsync(_user)).ReturnsAsync("token");
            _userManager.Setup(m => m.ResetPasswordAsync(_user, "token", "123")).ReturnsAsync(
                IdentityResult.Failed(new IdentityError { Description = "Lozinka je prekratka." }));

            var result = await _controller.ResetNewPassword("123", "123");

            Assert.IsType<ViewResult>(result);
            Assert.Equal("Lozinka je prekratka.", PrvaGreska(_controller));
        }

        [Fact]
        public async Task ResetNewPasswordPost_BazaBaciIzuzetak_PrikazujePorukuOGresciBaze()
        {
            PostaviProvjerenKod();
            _userManager.Setup(m => m.FindByEmailAsync(It.IsAny<string>())).ThrowsAsync(new InvalidOperationException());

            await _controller.ResetNewPassword("Nova1!", "Nova1!");

            Assert.StartsWith("Došlo je do problema sa bazom podataka", PrvaGreska(_controller));
        }
    }
}
