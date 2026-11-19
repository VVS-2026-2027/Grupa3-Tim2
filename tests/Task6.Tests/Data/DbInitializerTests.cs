using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Task6.Data;
using Task6.Models;

namespace Task6.Tests.Data
{
    public class DbInitializerTests
    {
        private static ServiceProvider CreateProvider()
        {
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddDbContext<EscapeRoomDbContext>(options => options.UseInMemoryDatabase(Guid.NewGuid().ToString()));
            services.AddIdentityCore<ApplicationUser>()
                .AddRoles<IdentityRole>()
                .AddEntityFrameworkStores<EscapeRoomDbContext>();
            return services.BuildServiceProvider();
        }

        [Fact]
        public async Task SeedAsync_PraznaBaza_DodajePocetnePodatke()
        {
            await using var provider = CreateProvider();
            await using var scope = provider.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<EscapeRoomDbContext>();
            var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

            await DbInitializer.SeedAsync(db, roles, users);

            Assert.Equal(3, await roles.Roles.CountAsync());
            Assert.Single(await users.GetUsersInRoleAsync("Admin"));
            Assert.Single(await users.GetUsersInRoleAsync("Zaposlenik"));
            Assert.Equal(2, (await users.GetUsersInRoleAsync("Korisnik")).Count);
            Assert.Equal(3, await db.EscapeRooms.CountAsync());
            var termini = await db.Termini.ToListAsync();
            Assert.NotEmpty(termini);
            Assert.All(termini, t => Assert.NotEqual(DayOfWeek.Sunday, t.Datum.DayOfWeek));
            Assert.Single(await db.Rezervacije.ToListAsync());
            Assert.Single(await db.Recenzije.ToListAsync());
        }

        [Fact]
        public async Task SeedAsync_PonovljenoPokretanje_NeDupliraPocetnePodatke()
        {
            await using var provider = CreateProvider();
            await using var scope = provider.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<EscapeRoomDbContext>();
            var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            await DbInitializer.SeedAsync(db, roles, users);
            var brojTermina = await db.Termini.CountAsync();

            await DbInitializer.SeedAsync(db, roles, users);

            Assert.Equal(3, await roles.Roles.CountAsync());
            Assert.Equal(4, await users.Users.CountAsync());
            Assert.Equal(3, await db.EscapeRooms.CountAsync());
            Assert.Equal(brojTermina, await db.Termini.CountAsync());
            Assert.Single(await db.Rezervacije.ToListAsync());
            Assert.Single(await db.Recenzije.ToListAsync());
        }
    }
}
