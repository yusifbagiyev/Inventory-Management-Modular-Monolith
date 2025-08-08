namespace SharedServices.Services
{
    public interface ITimeZoneService
    {
        DateTime ConvertToLocalTime(DateTime utcDateTime);
        DateTime ConvertToUtc(DateTime localDateTime);
        TimeZoneInfo GetLocalTimeZone();
    }
    public class TimeZoneService : ITimeZoneService
    {
        private readonly TimeZoneInfo _localTimeZone;

        public TimeZoneService()
        {
            // Azerbaijan Standard Time (GMT+4)
            _localTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Azerbaijan Standard Time");
        }

        public DateTime ConvertToLocalTime(DateTime utcDateTime)
        {
            // Ensure the DateTime is specified as UTC
            var utcTime = DateTime.SpecifyKind(utcDateTime, DateTimeKind.Utc);
            return TimeZoneInfo.ConvertTimeFromUtc(utcTime, _localTimeZone);
        }

        public DateTime ConvertToUtc(DateTime localDateTime)
        {
            // Convert local time to UTC
            return TimeZoneInfo.ConvertTimeToUtc(localDateTime, _localTimeZone);
        }

        public TimeZoneInfo GetLocalTimeZone()
        {
            return _localTimeZone;
        }
    }
}