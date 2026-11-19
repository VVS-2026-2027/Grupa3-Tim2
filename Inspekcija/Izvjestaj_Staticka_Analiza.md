# Izvještaj o statičkoj analizi koda

Grupa 3 – Tim 2, Verifikacija i validacija softvera, Projektni zadatak I dio (Task 1.2)

Analiziran je isti kod kao u inspekciji, aplikacija Cvjećara Latica (grana `inspekcija`).

## 1. Alati

Koristili smo SonarAnalyzer.CSharp (verzija 10.4). To su ista pravila koja koristi SonarLint ekstenzija za Visual Studio. Pored njega su uključeni i ugrađeni .NET analizatori (pravila CA), a C# kompajler je prijavio i upozorenja za nullable reference (CS86xx). Za provjeru biblioteka korišten je NuGet audit.

Analizatori se pokreću pri buildu projekta. Uključili smo ih preko `Directory.Build.props` (paket `SonarAnalyzer.CSharp`, `AnalysisLevel=latest`, `AnalysisMode=Recommended`) i pokrenuli:

```
dotnet build Cvjecara_Latica.csproj -t:Rebuild
dotnet list Cvjecara_Latica.csproj package --vulnerable --include-transitive
```

Migracije (`Data/Migrations/`) smo izbacili iz rezultata jer ih generiše Entity Framework. Sva upozorenja su u fajlu [staticka_analiza_rezultati.csv](staticka_analiza_rezultati.csv).

## 2. Rezultati

Dobili smo 178 upozorenja i nijednu grešku kompajliranja. NuGet audit je pronašao 7 paketa sa poznatim ranjivostima.

| Alat | Broj upozorenja |
|---|---:|
| SonarAnalyzer.CSharp | 87 |
| C# kompajler (nullable) | 61 |
| .NET analizatori (CA) | 30 |
| Ukupno | 178 |

| Dio aplikacije | Broj upozorenja |
|---|---:|
| Controllers/ | 91 |
| Models/ | 56 |
| Services/ | 12 |
| Data/ | 10 |
| Views/ | 8 |
| Program.cs | 1 |

Najviše upozorenja imaju `HomeController.cs` (29) i `CartsController.cs` (24). U `CartsController.cs` smo i u inspekciji našli najviše defekata.

Pravila koja su se najčešće pojavljivala:

| Pravilo | Broj | Šta znači | Stavka liste provjere |
|---|---:|---|---|
| CS8618 | 39 | Non-nullable svojstvo nije inicijalizovano (modeli) | A-V2 |
| S6967 | 37 | Akcija ne provjerava `ModelState.IsValid` | B-V1, A-DP2 |
| S6964 | 24 | Parametar akcije tipa int/double/bool treba biti nullable ili `[Required]` | B-V1 |
| CS8602, CS8604, CS8600, CS8601, CS8605 | 22 | Moguć pristup null vrijednosti | A-DP1 |
| CA1304, CA1305, CA1311, S6580 | 16 | Parsiranje i `ToUpper/ToLower` bez navedene kulture | A-DP2 |
| S6966 | 8 | Sinhroni poziv (`ToList`, `SaveChanges`) unutar async metode | A-S3 |
| S4144 | 7 | Metoda `Delete` je ista kao `Details` u 7 kontrolera | A-S7 |
| CA2016 | 4 | `CancellationToken` se ne prosljeđuje u pozadinskom servisu | A-DP6 |
| S125 | 2 | Zakomentarisan kod | A-S5 |
| S1144, S4487 | 2 | Nekorištena metoda `CartExists` i polje `_logger` | A-S4, A-V3 |
| S1244 | 1 | Poređenje `double` sa `==` (HomeController.cs:351) | A-AR1 |

Paketi sa ranjivostima:

| Paket | Verzija | Ozbiljnost | Oznaka |
|---|---|---|---|
| MailKit | 4.12.1 | Moderate | GHSA-9j88-vvj5-vhgr |
| MimeKit | 4.12.0 | Moderate | GHSA-g7hc-96xr-gvvx |
| System.Text.Json (tranzitivno) | 7.0.3 | High | GHSA-hh2w-p6rv-4g7w |
| System.Security.Cryptography.Pkcs (tranzitivno) | 8.0.1 | High | GHSA-846f-f764-76mx |
| Microsoft.Build (tranzitivno) | 17.8.3 | High | GHSA-w3q9-fxm7-j8fq |
| NuGet.Packaging, NuGet.Protocol (tranzitivno) | 6.11.0 | Low | GHSA-g4vj-cjjj-v7hg |

## 3. Poređenje sa inspekcijom

Neke stvari koje smo našli u inspekciji pronašao je i alat:

- nekorištena metoda `CartExists` i zakomentarisan kod u CartsController i OrdersController (S1144, S125)
- prazna metoda u `BestSellers.cshtml.cs` (S1186)
- moguća null vrijednost u `ConfirmOrder.cshtml` (CS8605)
- akcije koje ne provjeravaju ulazne podatke (S6967, S6964)
- ranjive verzije MailKit i MimeKit

Alat je pronašao i neke greške koje mi nismo primijetili u inspekciji:

- U HomeController.cs:351 cijena se poredi sa `==` (S1244). Proizvod čija cijena nije cijeli broj nikad se neće pronaći pretragom po cijeni.
- U nekim async akcijama se koriste sinhroni pozivi prema bazi (S6966), npr. `SaveChanges()` u CartsController.cs:283.
- Metode `Delete` i `Details` su iste u sedam kontrolera (S4144).
- `DateTime.Parse` (HomeController.cs:218) i `int.Parse` se koriste bez kulture (CA1305, S6580), pa rezultat zavisi od regionalnih postavki servera.
- `UnclaimedOrderChecker` ne prosljeđuje `CancellationToken` (CA2016), pa se servis ne zaustavlja kako treba kad se aplikacija gasi.
- Ranjive tranzitivne biblioteke (System.Text.Json, Pkcs), koje se ne vide kad se gleda samo `.csproj`.

Najozbiljnije nalaze iz inspekcije alat nije pronašao:

- iznos plaćanja koji dolazi iz forme i pristup tuđim narudžbama (IDOR), jer alat ne zna koji podaci smiju doći od korisnika ni kome pripada neki zapis
- `[AllowAnonymous]` na detaljima narudžbe i akcije bez `[ValidateAntiForgeryToken]`, jer je kod sintaksno ispravan
- lozinke u `appsettings.json`, jer analizatori ne provjeravaju JSON fajlove
- broj kartice spremljen kao običan tekst
- pogrešne zagrade u uslovu u CartsController.cs:122, jer je izraz ispravan C# kod
- nezatvoren input u `PlaceOrder.cshtml`, jer alat ne provjerava HTML u pogledima

Neka upozorenja nisu mnogo korisna. CS8618 se javlja 39 puta jer Entity Framework i model binding postavljaju vrijednosti tek nakon konstruktora, pa je dosta tih upozorenja šum. Ipak, isti uzrok smo u inspekciji prijavili kao stvarnu grešku (issue #23 i #25), pa ih ne treba samo ignorisati. S6967 se javlja i na GET akcijama koje primaju samo `id`, gdje provjera `ModelState` nema smisla. CA1707 (podvlaka u imenu `Cvjecara_Latica`) je samo stilsko pravilo.

## 4. Zaključak

Statička analiza brzo pronalazi greške u strukturi koda, null reference, nekorišten kod i ranjive biblioteke, i tu je pouzdanija od ručnog pregleda. Pronašla je i nekoliko stvarnih grešaka koje mi nismo vidjeli. S druge strane, nije pronašla nijedan od 19 kritičnih i visokih nalaza iz inspekcije, jer oni zavise od poslovne logike i od toga šta je programer htio postići. Zato alat ne može zamijeniti inspekciju, ali je dobro da se koristi uz nju. Kad alat pronađe strukturne greške, inspekcija se može više baviti kontrolom pristupa, validacijom i logikom, gdje je prema Pareto analizi najviše ozbiljnih defekata.
