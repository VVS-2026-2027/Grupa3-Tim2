using Task6.Models;
using Task6.Tests.Helpers;

namespace Task6.Tests.Models
{
    public class EscapeRoomTests
    {
        public static IEnumerable<object[]> PopustiIzJsona() =>
            TestData.FromJson<PopustSlucaj>("popusti_soba.json").Select(s => new object[] { s });

        private static EscapeRoom KreirajSobu(PopustSlucaj slucaj)
        {
            var sada = DateTime.UtcNow;
            return new EscapeRoom
            {
                Cijena = slucaj.Cijena,
                DiscountPercent = slucaj.Popust,
                DiscountStart = slucaj.PocetakDana.HasValue ? sada.AddDays(slucaj.PocetakDana.Value) : null,
                DiscountEnd = slucaj.KrajDana.HasValue ? sada.AddDays(slucaj.KrajDana.Value) : null
            };
        }

        [Theory]
        [MemberData(nameof(PopustiIzJsona))]
        public void IsDiscountActive_PodaciIzJsona_VracaOcekivanoStanje(PopustSlucaj slucaj)
        {
            var soba = KreirajSobu(slucaj);

            var aktivan = soba.IsDiscountActive();

            Assert.Equal(slucaj.Aktivan, aktivan);
        }

        [Theory]
        [MemberData(nameof(PopustiIzJsona))]
        public void CurrentPrice_PodaciIzJsona_VracaCijenuSaPopustomSamoKadJeAktivan(PopustSlucaj slucaj)
        {
            var soba = KreirajSobu(slucaj);

            var cijena = soba.CurrentPrice;

            Assert.Equal(slucaj.OcekivanaCijena, cijena, 2);
        }

        [Fact]
        public void Konstruktor_NovaSoba_ImaPrazneKolekcijeIBezPopusta()
        {
            var soba = new EscapeRoom();

            Assert.Empty(soba.Recenzije);
            Assert.Empty(soba.Termini);
            Assert.Equal(0, soba.DiscountPercent);
            Assert.False(soba.IsDiscountActive());
        }
    }
}
