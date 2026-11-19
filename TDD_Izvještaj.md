TDD razvoj novih funkcionalnosti aplikacije The Last Key

## 1. Uvod

U okviru ovog zadatka radili smo na razvoju dvije nove funkcionalnosti aplikacije **The Last Key**, koristeći TDD (Test-Driven Development) pristup.

TDD podrazumijeva da se prvo napišu testovi kojima se definiše kako određena funkcionalnost treba raditi, a tek nakon toga se piše kod koji treba zadovoljiti te testove.

Tokom rada pratili smo dva osnovna koraka:

- **RED** – prvo pišemo testove i provjeravamo da li padaju, jer funkcionalnost još nije implementirana.
- **GREEN** – nakon toga pišemo implementaciju i ponovo pokrećemo testove kako bismo provjerili da li sada prolaze.

Za pisanje i pokretanje testova koristili smo xUnit.

## 2. Prva funkcionalnost – Provjera kapaciteta rezervacije

### Šta smo željeli postići?

Željeli smo osigurati da korisnik prilikom rezervacije ne može odabrati neispravan broj osoba. Broj osoba mora biti najmanje 1 i ne smije biti veći od kapaciteta odabrane Escape Room sobe.

### RED faza

Prvo smo napisali testove za metodu `IsValidPartySize`, prije nego što smo napravili njenu implementaciju.

Provjerili smo nekoliko različitih situacija:

- Rezervacija za jednu osobu u sobi kapaciteta 6.
- Rezervacija za tri osobe u sobi kapaciteta 6.
- Rezervacija za šest osoba u sobi kapaciteta 6.
- Rezervacija za sedam osoba u sobi kapaciteta 6.
- Rezervacija sa nula osoba.
- Rezervacija sa negativnim brojem osoba.
- Slučaj u kojem je kapacitet sobe 0.

Na ovaj način smo unaprijed definisali šta metoda treba prihvatiti, a šta odbiti.

### GREEN faza

Nakon pisanja testova napravili smo klasu `ReservationValidator` u folderu `Services`.

U njoj smo implementirali metodu `IsValidPartySize`, koja provjerava da li je broj osoba u dozvoljenim granicama.

Zatim smo ovu provjeru povezali sa metodom `Book` u klasi `ReservationController`, tako da aplikacija odbije rezervaciju ako broj osoba nije odgovarajući.

**Rezultat:** Svih 7 testova za ovu funkcionalnost uspješno je prošlo.

## 3. Druga funkcionalnost – Vremensko ograničenje otkazivanja rezervacije

### Šta smo željeli postići?

Druga funkcionalnost odnosi se na otkazivanje rezervacija. Cilj je bio spriječiti korisnike da otkazuju rezervacije neposredno prije početka termina.

Zbog toga smo definisali pravilo da se rezervacija može otkazati samo ako su do početka termina ostala najmanje dva sata.

### RED faza

Prije implementacije napisali smo testove za metodu `Cancel`.

Testirali smo sljedeće situacije:

- Do početka termina ostala su tačno dva sata.
- Do početka termina ostala su tri sata.
- Do početka termina ostao je jedan sat i 59 minuta.
- Termin počinje upravo sada.
- Termin je već prošao.

Time smo provjerili i granični slučaj od tačno dva sata, jer je prema našem pravilu i tada otkazivanje dozvoljeno.

### GREEN faza

Nakon što smo definisali testove, napravili smo klasu `ReservationCancellationValidator` u folderu `Services`.

Implementirali smo metodu `CanCancel`, koja računa vremensku razliku između početka termina i trenutnog vremena. Ako je do termina ostalo najmanje dva sata, otkazivanje je dozvoljeno.

Na kraju smo ovu provjeru povezali sa metodom `Cancel` u klasi `ReservationController`.

**Rezultat:** Svih 5 testova za ovu funkcionalnost uspješno je prošlo.

## 4. Rezultati testiranja

Nakon završetka obje funkcionalnosti ponovo smo pokrenuli sve testove pomoću naredbe:

`dotnet test .\tests\Task6.Tests\Task6.Tests.csproj`

Dobijeni su sljedeći rezultati:

| Testovi                           |         Rezultat |
| --------------------------------- | ---------------: |
| Provjera kapaciteta rezervacije   |      7 uspješnih |
| Vremensko ograničenje otkazivanja |      5 uspješnih |
| **Ukupno**                        | **12 uspješnih** |
| Neuspješni testovi                |            **0** |

Dakle, svih 12 testova je prošlo bez ijednog neuspješnog testa.

## 5. Git commitovi i praćenje TDD procesa

Tokom rada koristili smo Git kako bismo zabilježili pojedinačne korake razvoja. Posebno smo vodili računa da se iz commitova može vidjeti redoslijed pisanja testova i implementacije.

Napravili smo sljedeće commitove:

| Commit    | Opis                                                            |
| --------- | --------------------------------------------------------------- |
| `2c6614e` | test: dodani testovi za provjeru kapaciteta rezervacije (RED)   |
| `1239713` | feat: implementirana provjera kapaciteta rezervacije (GREEN)    |
| `c1f3bc0` | test: dodani testovi za vremensko ogranicenje otkazivanja (RED) |
| `7e458f9` | feat: implementirano vremensko ogranicenje otkazivanja (GREEN)  |

Iz commit historije se jasno vidi da smo za svaku funkcionalnost prvo napisali testove, a zatim implementirali odgovarajuće rješenje.

## 6. Zaključak

Kroz ovaj zadatak smo praktično primijenili TDD pristup na dvije nove funkcionalnosti aplikacije The Last Key.

Prva funkcionalnost omogućava provjeru broja osoba prilikom rezervacije, dok druga uvodi vremensko ograničenje za otkazivanje rezervacija.

Pisanje testova prije implementacije pomoglo nam je da unaprijed definišemo očekivano ponašanje aplikacije i provjerimo da li naše rješenje ispravno radi.

Na kraju smo uspješno pokrenuli svih 12 testova, pri čemu nije bilo neuspješnih testova.

