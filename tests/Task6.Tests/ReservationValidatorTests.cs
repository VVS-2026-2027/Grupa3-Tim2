using Task6.Services;

namespace Task6.Tests
{
    public class ReservationValidatorTests
    {
        [Theory]
        [InlineData(1, 6, true)]
        [InlineData(3, 6, true)]
        [InlineData(6, 6, true)]
        [InlineData(7, 6, false)]
        [InlineData(0, 6, false)]
        [InlineData(-1, 6, false)]
        [InlineData(1, 0, false)]
        public void IsValidPartySize_ShouldReturnExpectedResult(
            int brojOsoba,
            int kapacitet,
            bool expected)
        {
            bool rezultat = ReservationValidator.IsValidPartySize(
                brojOsoba, kapacitet);

            Assert.Equal(expected, rezultat);
        }
    }
}