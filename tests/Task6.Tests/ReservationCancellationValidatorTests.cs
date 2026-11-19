
using Task6.Services;

namespace Task6.Tests
{
    public class ReservationCancellationValidatorTests
    {
        [Theory]
        [InlineData("2026-11-20T12:00:00", "2026-11-20T10:00:00", true)]
        [InlineData("2026-11-20T13:00:00", "2026-11-20T10:00:00", true)]
        [InlineData("2026-11-20T11:59:00", "2026-11-20T10:00:00", false)]
        [InlineData("2026-11-20T10:00:00", "2026-11-20T10:00:00", false)]
        [InlineData("2026-11-20T09:00:00", "2026-11-20T10:00:00", false)]
        public void CanCancel_ShouldReturnExpectedResult(
            string termin,
            string trenutnoVrijeme,
            bool expected)
        {
            DateTime datumTermina = DateTime.Parse(termin);
            DateTime trenutno = DateTime.Parse(trenutnoVrijeme);

            bool rezultat = ReservationCancellationValidator.CanCancel(
                datumTermina, trenutno);

            Assert.Equal(expected, rezultat);
        }
    }
}
