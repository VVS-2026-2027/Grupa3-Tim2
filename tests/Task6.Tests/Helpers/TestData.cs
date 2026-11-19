using System.Text.Json;

namespace Task6.Tests.Helpers
{
    // Čita testne podatke iz fajlova u folderu TestData (CSV i JSON),
    // za data driven testove preko [MemberData].
    public static class TestData
    {
        private static string PathOf(string fileName) =>
            Path.Combine(AppContext.BaseDirectory, "TestData", fileName);

        // Svaki red CSV fajla (bez zaglavlja) postaje jedan poziv testa.
        public static IEnumerable<object[]> FromCsv(string fileName)
        {
            return File.ReadAllLines(PathOf(fileName))
                .Skip(1)
                .Where(line => !string.IsNullOrWhiteSpace(line))
                .Select(line => line.Split(';').Cast<object>().ToArray());
        }

        public static List<T> FromJson<T>(string fileName)
        {
            var json = File.ReadAllText(PathOf(fileName));
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            return JsonSerializer.Deserialize<List<T>>(json, options)!;
        }
    }

    public record PopustSlucaj(string Opis, double Cijena, int Popust, int? PocetakDana, int? KrajDana, double OcekivanaCijena, bool Aktivan)
    {
        public override string ToString() => Opis;
    }

    public record FilterSlucaj(string Opis, string? SearchTerm, string? FilterTezina, int? BrojIgraca, int OcekivaniBroj)
    {
        public override string ToString() => Opis;
    }
}
