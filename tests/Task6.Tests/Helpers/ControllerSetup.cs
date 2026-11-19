using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.Primitives;
using Moq;
using Task6.Models;

namespace Task6.Tests.Helpers
{
    public static class ControllerSetup
    {
        // Postavlja HttpContext sa prijavljenim korisnikom (ili anonimnim ako je email null) i TempData.
        public static void WithUser(Controller controller, string? email, string? userId = null)
        {
            var identity = email == null
                ? new ClaimsIdentity()
                : new ClaimsIdentity(new[]
                {
                    new Claim(ClaimTypes.Name, email),
                    new Claim(ClaimTypes.NameIdentifier, userId ?? email)
                }, "TestAuth");

            var httpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) };

            controller.ControllerContext = new ControllerContext { HttpContext = httpContext };
            controller.TempData = new TempDataDictionary(httpContext, Mock.Of<ITempDataProvider>());
        }

        // Za akcije koje čitaju vrijednost direktno iz Request.Form (npr. Cijena u CreateRoom/EditRoom).
        public static void WithForm(Controller controller, Dictionary<string, string> values)
        {
            var form = values.ToDictionary(v => v.Key, v => new StringValues(v.Value));
            controller.HttpContext.Request.Form = new FormCollection(form);
        }

        public static Mock<UserManager<ApplicationUser>> CreateUserManagerMock()
        {
            var store = new Mock<IUserStore<ApplicationUser>>();
            return new Mock<UserManager<ApplicationUser>>(
                store.Object, null!, null!, null!, null!, null!, null!, null!, null!);
        }

        public static IFormFile CreateFile(string fileName, string contentType, long length)
        {
            var stream = new MemoryStream(new byte[Math.Min(length, 16)]);
            return new FormFile(stream, 0, length, "imageFile", fileName)
            {
                Headers = new HeaderDictionary(),
                ContentType = contentType
            };
        }
    }
}
