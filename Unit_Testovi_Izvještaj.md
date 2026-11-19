# Unit testiranje aplikacije The Last Key

## Obuhvat testiranja

Za Task 2 sam proširila postojeći xUnit projekat. Kod kontrolera sam gledala šta akcija vrati, šta nakon poziva ostane u bazi i da li se, recimo, pošalje obavijest. Za svaki test koji radi s bazom koristila sam novu InMemory bazu, tako da testovi mogu raditi odvojeno i bez pokretanja PostgreSQL-a. Kontrolere sam pozivala direktno, pa kroz ove testove nisam prolazila kroz ASP.NET Core filtere.

## Testiranje podacima iz fajlova

| Format | Fajl | Funkcionalnost i scenariji |
| --- | --- | --- |
| CSV | `kartice.csv` | Validacija unosa plaćanja. Nedostajuća polja, dužina i znakovi broja kartice i CVV-a, ime, format i mjesec isteka te istekla kartica. |
| CSV | `cijene_soba.csv` | Kreiranje sobe kroz administraciju i zaposlenički panel. Cijeli i decimalni iznosi, nula, pogrešan separator, negativan iznos, neispravne cifre i prazan unos. |
| JSON | `popusti_soba.json` | Utvrđivanje aktivnog popusta i izračun cijene. Aktivni, budući i istekli popusti, nedostajući datumi, nulti i stopostotni popust. |
| JSON | `filteri_soba.json` | Pretraga po nazivu i opisu, filtriranje po težini i broju igrača te kombinacije filtera, uključujući pretragu bez rezultata. |

Podatke sam ostavila u `tests/Task6.Tests/TestData`. Na primjer, u `cijene_soba.csv` mogu dodati još jedan red s cijenom i očekivanim ishodom, a xUnit će ga pokrenuti kao još jedan slučaj istog testa.

## Zamjenski objekti

Za plaćanje mi je bilo bitno provjeriti da se uplata stvarno upiše i da se potvrda pošalje na pravu adresu. Uplatu provjeravam u InMemory bazi, a slanje poruke preko zamjenskog servisa. Isti pristup sam koristila za slike, gdje mogu provjeriti šta je kontroler poslao servisu za pohranu bez pravog uploada.

| Objekat | Primjer |
| --- | --- |
| EF Core InMemory | Provjerava se da li ispravna rezervacija, recenzija ili uplata mijenja stanje baze i da li se odbijeni zahtjevi ne spremaju. |
| Moq za `UserManager` i `SignInManager` | Simuliraju se prijava, neprijavljen korisnik i neuspjela operacija nad korisničkim računom. |
| Moq za `IEmailService` i `IStorageService` | Kontrolerski testovi provjeravaju kome se šalju obavijesti i s kojim parametrima se učitava ili briše slika. |
| Moq za `ISmtpClient` | `EmailServiceTests` provjerava HTML sadržaj poruke, STARTTLS i ponašanje kada autentikacija ne uspije. |
| Moq za Supabase Storage API | `SupabaseStorageServiceTests` provjerava upload, formiranje javnog URL-a, slanje relativne putanje pri brisanju i obradu grešaka skladišta. |
| `SpyLogger` i Moq za `IServiceProvider` | Testovi pozadinskih servisa bilježe poruke, simuliraju nedostupnu bazu ili otkazivanje čišćenja i čekaju rezultat bez fiksne pauze. |

## Spisak testova i raspodjela po članovima

Ja sam radila glavninu Taska 2. Najviše sam se bavila testovima kontrolera, podacima iz CSV i JSON fajlova, zamjenskim objektima i izvještajem. U zajedničkom projektu su i Hasanovi raniji TDD testovi za kapacitet rezervacije i rok za otkazivanje.

Za odbranu ću pokazati kako je testni projekat organizovan, nekoliko testova kontrolera i kako sam izmjerila pokrivenost. Ostale teme smo za prezentaciju podijelili ovako.

| Član | Tema za odbranu |
| --- | --- |
| Lamija Maglić | Organizacija testnog projekta, testovi računa, administracije i zaposleničkog panela, CSV i JSON te mjerenje pokrivenosti |
| Nejla Hopić | Pretraga i prikaz soba, popusti i izračun cijene |
| Lejla Gičević | Recenzije, korisnička podrška i primjeri zamjenskih objekata za e-mail i pohranu slika |
| Hasan Hamzagić | Rezervacije, plaćanja i testovi oba validatora |

Za ispis imena pojedinačnih testova koristi se `dotnet test Task6.slnx --list-tests`.

| Testna klasa | Funkcionalnost |
| --- | --- |
| `AccountControllerTests` | Registracija, prijava, odjava i reset lozinke |
| `AdminControllerTests` | Prava pristupa, statistika, sobe, termini, rezervacije, plaćanja, recenzije, popusti, obavijesti, korisnici i podrška |
| `ZaposlenikControllerTests` | Prava pristupa, statistika, sobe, termini, rezervacije, recenzije, popusti, obavijesti i podrška |
| `PaymentControllerTests` | Pristup rezervaciji, validacija kartice, iznos, spremanje i potvrda plaćanja |
| `ReservationControllerTests` | Pregled, kreiranje i otkazivanje rezervacije |
| `PopustiControllerTests` | Pristup popustima, obavijesti i period važenja popusta za sobu |
| `ReviewControllerTests` i `RecenzijeControllerTests` | Pregled, dodavanje i brisanje recenzija, pravo na recenziju i duplikati |
| `SupportControllerTests` i `ProfilControllerTests` | Slanje poruka podršci, pregled profila i korisničkih rezervacija |
| `RoomControllerTests`, `EscapeRoomsControllerTests` i `HomeControllerTests` | Filteri, detalji sobe, početna stranica i greške |
| `EscapeRoomTests` | Aktivnost popusta i cijena nakon popusta |
| `DiscountCleanupServiceTests` i `TerminCleanupServiceTests` | Čišćenje isteklih popusta i termina, pokretanje servisa, greške i otkazivanje |
| `EmailServiceTests` i `SupabaseStorageServiceTests` | Slanje poruke, pohrana i brisanje slika te greške vanjskih servisa |
| `ReservationValidatorTests` i `ReservationCancellationValidatorTests` | Granice broja igrača i roka za otkazivanje |
| `MigrationTests` | Generisanje SQL skripti za migraciju i povratak, kolone za popuste i EF model |
| `DbInitializerTests` | Početne uloge, korisnici, sobe, termini, rezervacija i recenzija te ponovljeno pokretanje inicijalizacije |

## Rezultat i pokrivenost

Kad sam zadnji put pokrenula sve testove, prošlo je **437 od 437**. Pokrivenost linija nakon izuzimanja `Program.cs` i Razor pogleda iznosi **99,38%** (5.132 od 5.164 linija). Bez direktorija `Migrations` pokrivenost je **98,71%** (2.449 od 2.481 linija).

```bash
dotnet test Task6.slnx --collect "XPlat Code Coverage"
```

Coverlet ispisuje putanju do `coverage.cobertura.xml`. U tabeli su linije asinhronih metoda pridružene klasi iz izvornog fajla, a svaka linija se broji jednom. Kod migracija je dizajnerski dio prikazan uz isti naziv klase.

| Klasa | Izvorni fajl | Pokrivene linije | Ukupno linija | Pokrivenost |
| --- | --- | ---: | ---: | ---: |
| `AccountController` | `Controllers/AccountController.cs` | 215 | 215 | 100,00% |
| `AdminController` | `Controllers/AdminController.cs` | 593 | 593 | 100,00% |
| `EscapeRoomsController` | `Controllers/EscapeRoomController.cs` | 35 | 35 | 100,00% |
| `HomeController` | `Controllers/HomeController.cs` | 20 | 20 | 100,00% |
| `PaymentController` | `Controllers/PaymentController.cs` | 135 | 138 | 97,83% |
| `PopustiController` | `Controllers/PopustiController.cs` | 105 | 105 | 100,00% |
| `ProfilController` | `Controllers/ProfilController.cs` | 54 | 54 | 100,00% |
| `RecenzijeController` | `Controllers/RecenzijeController.cs` | 34 | 34 | 100,00% |
| `ReservationController` | `Controllers/ReservationController.cs` | 147 | 147 | 100,00% |
| `ReviewController` | `Controllers/ReviewController.cs` | 119 | 119 | 100,00% |
| `RoomController` | `Controllers/RoomController.cs` | 61 | 61 | 100,00% |
| `SupportController` | `Controllers/SupportController.cs` | 41 | 41 | 100,00% |
| `ZaposlenikController` | `Controllers/ZaposlenikController.cs` | 471 | 471 | 100,00% |
| `DbInitializer` | `Data/DbInitializer.cs` | 147 | 157 | 93,63% |
| `EscapeRoomDbContext` | `Data/EscapeRoomDbContext.cs` | 10 | 10 | 100,00% |
| `ApplicationUser` | `Models/ApplicationUser.cs` | 5 | 5 | 100,00% |
| `ErrorViewModel` | `Models/ErrorViewModel.cs` | 2 | 2 | 100,00% |
| `EscapeRoom` | `Models/EscapeRoom.cs` | 21 | 21 | 100,00% |
| `Obavijest` | `Models/Obavijest.cs` | 6 | 6 | 100,00% |
| `Placanje` | `Models/Placanje.cs` | 7 | 7 | 100,00% |
| `Podrska` | `Models/Podrska.cs` | 10 | 10 | 100,00% |
| `Recenzija` | `Models/Recenzija.cs` | 8 | 8 | 100,00% |
| `Rezervacija` | `Models/Rezervacija.cs` | 9 | 9 | 100,00% |
| `Termin` | `Models/Termin.cs` | 7 | 7 | 100,00% |
| `DiscountCleanupService` | `Services/DiscountCleanupService.cs` | 43 | 44 | 97,73% |
| `EmailService` | `Services/EmailService.cs` | 28 | 31 | 90,32% |
| `ReservationCancellationValidator` | `Services/ReservationCancellationValidator.cs` | 3 | 3 | 100,00% |
| `ReservationValidator` | `Services/ReservationValidator.cs` | 3 | 3 | 100,00% |
| `SupabaseStorageService` | `Services/SupabaseStorageService.cs` | 38 | 48 | 79,17% |
| `TerminCleanupService` | `Services/TerminCleanupService.cs` | 51 | 52 | 98,08% |
| `AdminDashboardViewModel` | `ViewModels/Admin/AdminDashboardViewModel.cs` | 0 | 4 | 0,00% |
| `DiscountViewModel` | `ViewModels/DiscountViewModel.cs` | 5 | 5 | 100,00% |
| `LoginViewModel` | `ViewModels/LoginViewModel.cs` | 3 | 3 | 100,00% |
| `PopustViewModel` | `ViewModels/PopustViewModel.cs` | 3 | 3 | 100,00% |
| `RegisterViewModel` | `ViewModels/RegisterViewModel.cs` | 5 | 5 | 100,00% |
| `RoomDiscountViewModel` | `ViewModels/RoomDiscountViewModel.cs` | 5 | 5 | 100,00% |
| `InitialPostgres` | `Migrations/20260602175312_InitialPostgres.Designer.cs` | 545 | 545 | 100,00% |
| `InitialPostgres` | `Migrations/20260602175312_InitialPostgres.cs` | 384 | 384 | 100,00% |
| `DodajOdgovorZaPodrsku` | `Migrations/20260606011230_DodajOdgovorZaPodrsku.Designer.cs` | 555 | 555 | 100,00% |
| `DodajOdgovorZaPodrsku` | `Migrations/20260606011230_DodajOdgovorZaPodrsku.cs` | 30 | 30 | 100,00% |
| `DodajImageUrl` | `Migrations/20260609105757_DodajImageUrl.Designer.cs` | 559 | 559 | 100,00% |
| `DodajImageUrl` | `Migrations/20260609105757_DodajImageUrl.cs` | 13 | 13 | 100,00% |
| `AddDiscountFields` | `Migrations/20260610000100_AddDiscountFields.cs` | 29 | 29 | 100,00% |
| `EscapeRoomDbContextModelSnapshot` | `Migrations/EscapeRoomDbContextModelSnapshot.cs` | 568 | 568 | 100,00% |

Coverlet ne prikazuje izvršive linije za interfejse `IEmailService` i `IStorageService` i enumeracije `Tezina`, `TipObavijesti` i `Uloga`.

Novi testovi inicijalizacije provjeravaju početne podatke u InMemory bazi. Test migracija generiše PostgreSQL skripte bez izvršavanja nad serverom. `AddDiscountFields` provjeren je direktno kroz njegove operacije. U projektu nedostaju metapodaci za automatsko otkrivanje te migracije, pa generisana skripta iz postojeće konfiguracije ne sadrži taj korak. `AdminDashboardViewModel` nema poziva u trenutnom kodu aplikacije i ostaje nepokriven. Konstrukcija stvarnog Supabase i SMTP klijenta iz produkcijske konfiguracije ostaje izvan izolovanih scenarija.

Pokrivenost sam izmjerila za cijeli projekat. Još treba potvrditi spisak testova i po jedan primjer zamjenskog objekta koji je radio svaki član.
