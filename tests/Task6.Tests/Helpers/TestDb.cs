using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Task6.Data;
using Task6.Models;

namespace Task6.Tests.Helpers
{
    // Zamjenska baza: svaki test dobija svoju praznu InMemory bazu,
    // pa su testovi međusobno neovisni i ne treba im aktivan PostgreSQL.
    public static class TestDb
    {
        public static EscapeRoomDbContext Create()
        {
            var options = new DbContextOptionsBuilder<EscapeRoomDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
                .Options;

            return new EscapeRoomDbContext(options);
        }

        public static ApplicationUser AddUser(EscapeRoomDbContext db, string email, Uloga uloga = Uloga.Korisnik)
        {
            var user = new ApplicationUser
            {
                Id = Guid.NewGuid().ToString(),
                UserName = email,
                Email = email,
                Ime = "Test",
                Prezime = "Korisnik",
                Uloga = uloga
            };
            db.Users.Add(user);
            db.SaveChanges();
            return user;
        }

        public static EscapeRoom AddRoom(EscapeRoomDbContext db, string naziv = "Soba", int kapacitet = 6, double cijena = 50)
        {
            var room = new EscapeRoom
            {
                Naziv = naziv,
                Opis = "Opis sobe " + naziv,
                Kapacitet = kapacitet,
                Cijena = cijena,
                Tezina = Tezina.Srednja
            };
            db.EscapeRooms.Add(room);
            db.SaveChanges();
            return room;
        }

        public static Termin AddTermin(EscapeRoomDbContext db, EscapeRoom room, DateTime datum, string vrijeme = "18:00", bool dostupnost = true)
        {
            var termin = new Termin
            {
                RoomID = room.RoomID,
                Datum = datum,
                Vrijeme = vrijeme,
                Dostupnost = dostupnost
            };
            db.Termini.Add(termin);
            db.SaveChanges();
            return termin;
        }

        public static Rezervacija AddRezervacija(EscapeRoomDbContext db, ApplicationUser user, Termin termin, int brojOsoba = 2, bool status = true)
        {
            var rezervacija = new Rezervacija
            {
                KorisnikID = user.Id,
                TerminID = termin.TerminID,
                BrojOsoba = brojOsoba,
                DatumKreiranja = DateTime.UtcNow,
                Status = status
            };
            db.Rezervacije.Add(rezervacija);
            db.SaveChanges();
            return rezervacija;
        }
    }
}
