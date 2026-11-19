namespace Task6.Services
{
    public class ReservationValidator
    {
        public static bool IsValidPartySize(int brojOsoba, int kapacitet)
        {
            return brojOsoba >= 1 && brojOsoba <= kapacitet;
        }
    }
}