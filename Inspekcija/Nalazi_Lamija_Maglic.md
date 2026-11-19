# Nalazi inspekcije – Views/, Program.cs, Cvjecara_Latica.csproj

Inspektor: Lamija Maglić
Liste provjere: Lista A – opšta (Predavanje 2, Wiegers), Lista B – sigurnosna (Predavanje 3)

Pregledano je 60 pogleda u folderu `Views/` (2939 linija), `Program.cs` (62 linije), `Cvjecara_Latica.csproj` i konfiguracijski fajlovi projekta (`.gitignore`, `*.user`, `Properties/`), u dva prolaza: funkcionalne greške i kvalitet koda (Lista A), te sigurnosni propusti (Lista B).
Pronađeno je 17 defekata: 1 visoke, 4 srednje, 7 niske i 5 minorne ozbiljnosti. Na još 4 mjesta u pogledima se vidi propust koji je već prijavljen u kontrolerima, pa je tu ostavljen samo kratak komentar sa brojem issue-a.

| Kategorija | Broj defekata |
|---|---|
| Struktura (A-S1, A-S3, A-S4, A-S5, A-S7) | 8 |
| Upravljanje tajnama / vanjske komponente (B-T2, B-Z1) | 3 |
| Logika / validacija (A-PG1, A-DP1, A-DP2) | 4 |
| Kontrola pristupa (B-A1) | 1 |
| Kriptografija i integritet (CWE-353) | 1 |

Najozbiljniji nalaz je u `Views/Carts/PlaceOrder.cshtml`, gdje se polje za promo kod ne prikazuje jer input za telefon nije zatvoren, pa kupac ne može iskoristiti popust.

---

## Komentari na linijama

### 1. Views/Carts/PlaceOrder.cshtml : 38–46

**Ozbiljnost:** 4 – Visoko
**Lista provjere:** A-S1 – Da li kod potpuno i ispravno implementira dizajn?; A-S3 – Struktura i formatiranje
**CWE / OWASP:** –
**Opis:**
Input za telefon nije ispravno zatvoren (l. 41: `title="..." /` bez `>`), a odmah iza je HTML komentar i novi `<input>` za promo kod (l. 42–46).
Browser sve to parsira kao jedan element: `<!--`, `<input` i ostalo postaju atributi input-a za telefon, a duplikati `name`, `class` i `pattern` se ignorišu.
Polje `discountCode` se zato nikada ne prikazuje niti šalje, pa kupac ne može unijeti promo kod, a cijela funkcionalnost popusta (CartsController, l. 264–288) je nedostupna.
Input za telefon treba zatvoriti sa `/>` i komentar izmjestiti van taga.

**Issue:** `[S4][A-S1] Polje za promo kod se ne prikazuje zbog nezatvorenog input taga – PlaceOrder.cshtml:38–46` (labele: severity-4, lista-A, logika)

---

### 2. Program.cs : 29

**Ozbiljnost:** 3 – Srednje
**Lista provjere:** B-A1 – Autorizacija i kontrola pristupa
**CWE / OWASP:** CWE-352 / A01:2025 – Broken Access Control
**Opis:**
MVC je registrovan bez globalnog filtera `AutoValidateAntiforgeryTokenAttribute`, pa zaštita od CSRF-a zavisi od toga da li je programer ručno dodao `[ValidateAntiForgeryToken]` na svaku POST akciju.
To je zajednički uzrok CSRF nalaza u CartsController, HomeController i OrdersController (issue #5, #10, #17, #18, #20): pogledi šalju token, ali ga server na tim akcijama ne provjerava.
Treba dodati `options.Filters.Add(new AutoValidateAntiforgeryTokenAttribute())` u `AddControllersWithViews`.

**Issue:** `[S3][B-A1] Nedostaje globalna CSRF zaštita – Program.cs:29` (labele: severity-3, lista-B, kontrola-pristupa)

---

### 3. Cvjecara_Latica.csproj : 11, 18

**Ozbiljnost:** 3 – Srednje
**Lista provjere:** B-Z1 – Zastarjele biblioteke s poznatim ranjivostima
**CWE / OWASP:** CWE-1395 / A03:2025 – Software Supply Chain Failures
**Opis:**
`dotnet list package --vulnerable --include-transitive` prijavljuje ranjive pakete: MailKit 4.12.1 (GHSA-9j88-vvj5-vhgr) i MimeKit 4.12.0 (GHSA-g7hc-96xr-gvvx), oba srednje ozbiljnosti, te tranzitivne System.Text.Json 7.0.3, System.Security.Cryptography.Pkcs 8.0.1 i Microsoft.Build 17.8.3 visoke ozbiljnosti.
MailKit i MimeKit se koriste u EmailService za slanje emailova kupcima.
Pakete treba ažurirati na verzije bez poznatih ranjivosti i provjeru uključiti u build.

**Issue:** `[S3][B-Z1] Ranjive NuGet biblioteke (MailKit, MimeKit, System.Text.Json) – Cvjecara_Latica.csproj:11–18` (labele: severity-3, lista-B, struktura)

---

### 4. Properties/PublishProfiles/IISProfile.pubxml.user : 6

**Ozbiljnost:** 3 – Srednje
**Lista provjere:** B-T2 – Jesu li tajni podaci izmješteni u konfiguraciju i u .gitignore?
**CWE / OWASP:** CWE-540 / A02:2025 – Security Misconfiguration
**Opis:**
U repozitorij je commitan lični fajl publish profila koji sadrži šifrovanu lozinku za deploy (`EncryptedPassword`), zajedno s `Cvjecara_Latica.csproj.user` i `serviceDependencies.local.json.user`, koji otkrivaju lokalne putanje autora.
`.gitignore` ima samo 9 linija i ne isključuje `*.user` ni `*.pubxml`, pa će svaki novi takav fajl također završiti na GitHubu.
Fajlove treba ukloniti iz repozitorija i koristiti standardni Visual Studio `.gitignore`.

**Issue:** `[S3][B-T2] Commitan publish profil s lozinkom i *.user fajlovi – IISProfile.pubxml.user:6` (labele: severity-3, lista-B, tajne)

---

### 5. Views/Carts/OrderSuccess.cshtml : 28

**Ozbiljnost:** 3 – Srednje
**Lista provjere:** A-DP2 – Provjeravaju li se ulazni podaci na validnost i potpunost?
**CWE / OWASP:** –
**Opis:**
Ocjena se upisuje u skriveno polje `rating` tek klikom na zvjezdicu, a forma nema nikakvu provjeru da je ocjena odabrana.
Ako korisnik klikne "Submit Rating" bez odabira, šalje se prazna vrijednost, `SubmitRating` je odbija i preusmjerava na nepostojeću akciju `OrderSuccess` (CartsController, l. 444), pa korisnik dobija 404.
Dugme treba onemogućiti dok ocjena nije odabrana ili dodati `required` provjeru.

**Issue:** `[S3][A-DP2] Ocjena se može poslati bez odabira zvjezdice – OrderSuccess.cshtml:28` (labele: severity-3, lista-A, validacija)

---

### 6. Views/Shared/_Layout.cshtml : 10

**Ozbiljnost:** 2 – Nisko
**Lista provjere:** B-Z1 – Sigurnost vanjskih komponenti
**CWE / OWASP:** CWE-353 / A08:2025 – Software or Data Integrity Failures
**Opis:**
Font Awesome se učitava s vanjskog CDN-a bez atributa `integrity` i `crossorigin` (Subresource Integrity).
Ako CDN bude kompromitovan, izmijenjeni sadržaj se učitava na svakoj stranici aplikacije bez ikakve provjere.
Treba dodati SRI hash ili biblioteku servirati lokalno iz `wwwroot/lib`.

---

### 7. Views/Shared/_Layout.cshtml : 12–14

**Ozbiljnost:** 2 – Nisko
**Lista provjere:** A-S3 – Je li kod dobro strukturiran?; A-S4 – Nepotrebni ili nedostižni dijelovi koda
**CWE / OWASP:** –
**Opis:**
Unutar `<head>` layouta definisana je sekcija `@section Scripts`, ali layout nema svoj layout koji bi je renderovao, pa se `_ValidationScriptsPartial` odatle nikada ne učitava.
Klijentska validacija zato radi samo na pogledima koji sami definišu sekciju Scripts, dok ostale forme šalju nevalidne podatke serveru.
Blok treba ukloniti, a validacijske skripte uključiti u poglede koji ih trebaju.

---

### 8. Views/Products/Index.cshtml : 72–77

**Ozbiljnost:** 2 – Nisko
**Lista provjere:** A-PG1 – Jesu li grananja potpuna, ispravna i pravilno ugniježđena?
**CWE / OWASP:** –
**Opis:**
Link `Delete` je izvan bloka `if (User.IsInRole("Administrator"))`, pa ga vide svi posjetioci, dok je javno dostupan `Details` sakriven unutar bloka.
Uslov je obrnut u odnosu na prava pristupa u ProductsController (Details javan, Delete samo za administratora): kupac dobija link koji vodi na odbijen pristup, a ne vidi link koji smije koristiti.
`Delete` treba premjestiti u admin blok, a `Details` iz njega izvaditi.

---

### 9. Views/Carts/ConfirmOrder.cshtml : 43

**Ozbiljnost:** 2 – Nisko
**Lista provjere:** A-DP1 – Provjeravaju li se pokazivači na null?
**CWE / OWASP:** –
**Opis:**
`ViewBag.DeliveryDate.ToString(...)` se poziva bez provjere na null, iako se ista vrijednost u l. 17 koristi s `?.`.
GET akcija `ConfirmOrder` (CartsController, l. 311–328) uopće ne postavlja `ViewBag.DeliveryDate`, pa prikaz stranice na toj ruti uvijek baca `RuntimeBinderException` i korisnik dobija grešku umjesto potvrde narudžbe.
Treba koristiti `?.` kao u l. 17.

---

### 10. Views/Carts/PlaceOrder.cshtml : 48–51

**Ozbiljnost:** 2 – Nisko
**Lista provjere:** A-S7 – Ponovljeni blokovi koda; A-S9 – Imenovane konstante
**CWE / OWASP:** –
**Opis:**
Pravilo isporuke (najranije za 2 dana, između 09:00 i 17:00) hardkodirano je u pogledu, a isti brojevi postoje i u CartsController (l. 249–251).
Promjena pravila na jednom mjestu daje različito ponašanje forme i servera. Pogled koristi i `DateTime.Now` servera, pa granice ne odgovaraju vremenskoj zoni korisnika.
Vrijednosti treba izdvojiti u konstante ili konfiguraciju i proslijediti pogledu iz kontrolera.

---

### 11. Views/Carts/Index.cshtml : 5

**Ozbiljnost:** 2 – Nisko
**Lista provjere:** A-S7 – Ponovljeni blokovi koda
**CWE / OWASP:** –
**Opis:**
Ukupna cijena korpe računa se u pogledu, nezavisno od računanja u CartsController (l. 228, 245), uz drugačije pravilo za null količinu.
Pogled i kontroler mogu prikazati različite iznose za istu korpu. Poslovna logika ne pripada pogledu.
Total treba izračunati jednom u kontroleru i proslijediti ga pogledu.

---

### 12. Cvjecara_Latica.csproj : 19–20

**Ozbiljnost:** 2 – Nisko
**Lista provjere:** A-S4 – Nepotrebne procedure; B-Z1 – Vanjske komponente
**CWE / OWASP:** CWE-1395 / A03:2025 – Software Supply Chain Failures
**Opis:**
Paketi `QRCoder` i `ZXing.Net.Bindings.SkiaSharp` (s nativnom SkiaSharp bibliotekom) referencirani su u projektu, ali se nigdje u kodu ne koriste.
Nepotrebne zavisnosti povećavaju veličinu aplikacije i površinu napada.
Neiskorištene pakete treba ukloniti.

---

### 13. Views/Home/Login.cshtml : 1–26

**Ozbiljnost:** 1 – Minorno
**Lista provjere:** A-S5 – Zaostali stubovi i testni kod
**CWE / OWASP:** –
**Opis:**
Stara verzija forme ostavljena je kao HTML komentar (`<!-- ... -->`), koji se, za razliku od Razor komentara, šalje u browser i vidljiv je u izvornom kodu stranice.
Isto u `Register.cshtml` (l. 10–40, označeno "staro"), `Carts/Index.cshtml` (l. 9–29) i `Search.cshtml` (l. 65–70).
Zakomentarisan kod treba ukloniti.

---

### 14. Views/Home/Register.cshtml : 7, 43

**Ozbiljnost:** 1 – Minorno
**Lista provjere:** A-S3 – Konzistentnost
**CWE / OWASP:** –
**Opis:**
Stranica za registraciju ima naslov i zaglavlje "Sign in", što je naziv za prijavu, dok je dugme "Register".
Treba koristiti "Sign up" / "Register".

---

### 15. Views/Home/Search.cshtml : 72–78

**Ozbiljnost:** 1 – Minorno
**Lista provjere:** A-S4 – Nepotrebni dijelovi koda
**CWE / OWASP:** –
**Opis:**
Skripta ručno poziva `form.submit()` na Enter u polju za pretragu, što browser već radi sam za formu sa submit dugmetom.
Kod je suvišan i treba ga ukloniti.

---

### 16. Views/Home/BestSellers.cshtml.cs : 1–12

**Ozbiljnost:** 1 – Minorno
**Lista provjere:** A-S4 – Nepozvane ili nepotrebne procedure
**CWE / OWASP:** –
**Opis:**
U folderu MVC pogleda nalazi se prazan Razor Pages model (`BestSellersModel : PageModel`), koji se nigdje ne koristi jer stranicu prikazuje `HomeController.BestSellers` (l. 363).
Fajl treba ukloniti.

---

### 17. Program.cs : 45

**Ozbiljnost:** 1 – Minorno
**Lista provjere:** A-S5 – Zaostali stubovi
**CWE / OWASP:** –
**Opis:**
Ostao je generički komentar iz šablona projekta s vanjskim linkom, koji ne opisuje stvarnu konfiguraciju.
Treba ga ukloniti.

---

## Kratki komentari (isti propust već prijavljen)

| Fajl : linija | Komentar |
|---|---|
| Views/Carts/ConfirmOrder.cshtml : 44 | `Ozbiljnost: 5 \| B-V1 \| CWE-472 – isti propust, vidi issue #6` (iznos u hidden polju) |
| Views/Carts/OrderSuccess.cshtml : 21 | `Ozbiljnost: 4 \| B-A1 \| CWE-639 – isti propust, vidi issue #9` (orderId u hidden polju) |
| Views/Payments/Index.cshtml : 49 | `Ozbiljnost: 4 \| B-K1 \| CWE-312 – isti propust, vidi issue #21` (puni broj kartice u tabeli) |
| Program.cs : 16–19 | `Ozbiljnost: 4 \| B-A1 \| CWE-307 – isti propust, vidi issue #19` (prijava bez zaključavanja naloga) |
