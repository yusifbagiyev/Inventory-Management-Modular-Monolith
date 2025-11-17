namespace NotificationService.Application.DTOs
{
    public record WhatsAppSettings
    {
        public string ApiUrl { get; set; } = string.Empty;
        public string ApiToken { get; set; } = string.Empty;
    }
}