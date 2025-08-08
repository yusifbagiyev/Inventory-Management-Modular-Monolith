namespace SharedServices.Converters
{
	public class DateTimeJsonConverter : JsonConverter<DateTime>
	{
		private readonly ITimeZoneService _timeZoneService;

		public DateTimeJsonConverter(ITimeZoneService timeZoneService)
		{
			_timeZoneService = timeZoneService;
		}

		public override DateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
		{
			var dateTime = reader.GetDateTime();
			return DateTime.SpecifyKind(dateTime, DateTimeKind.Utc);
		}

		public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options)
		{
			// Convert UTC to local time before serializing
			var localTime = _timeZoneService.ConvertToLocalTime(value);
			writer.WriteStringValue(localTime.ToString("yyyy-MM-ddTHH:mm:ss"));
		}
	}
}