
namespace Task6.Services
{
    public class ReservationCancellationValidator
    {
        public static bool CanCancel(DateTime datumTermina, DateTime trenutnoVrijeme)
        {
            return datumTermina - trenutnoVrijeme >= TimeSpan.FromHours(2);
        }
    }
}
