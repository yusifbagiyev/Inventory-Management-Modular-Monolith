namespace SharedServices.Extensions
{
    public static class DateTimeExtensions
    {
        private static readonly TimeZoneInfo AzerbaijanTimeZone =
            TimeZoneInfo.FindSystemTimeZoneById("Azerbaijan Standard Time");

        public static DateTime ToAzerbaijanTime(this DateTime utcDateTime)
        {
            if (utcDateTime.Kind != DateTimeKind.Utc)
                utcDateTime = DateTime.SpecifyKind(utcDateTime, DateTimeKind.Utc);

            return TimeZoneInfo.ConvertTimeFromUtc(utcDateTime, AzerbaijanTimeZone);
        }

        public static DateTime ToUtc(this DateTime localDateTime)
        {
            if (localDateTime.Kind == DateTimeKind.Utc)
                return localDateTime;

            return TimeZoneInfo.ConvertTimeToUtc(localDateTime, AzerbaijanTimeZone);
        }

        public static string ToLocalString(this DateTime utcDateTime, string format = "dd/MM/yyyy HH:mm")
        {
            return utcDateTime.ToAzerbaijanTime().ToString(format);
        }
    }
}