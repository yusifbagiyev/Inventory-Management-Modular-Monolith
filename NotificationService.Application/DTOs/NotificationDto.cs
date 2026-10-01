namespace NotificationService.Application.DTOs
{
    public record NotificationDto
    {
        public int Id { get; set; }
        public string Type { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public string? Data { get; set; }
        public bool IsRead { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? ReadAt { get; set; }
    }

    /// <summary>One page of a user's notifications for the Notifications page, with the tab counts.</summary>
    public record NotificationPageDto
    {
        public List<NotificationDto> Items { get; init; } = [];
        /// <summary>How many match the current tab and type (for the pages).</summary>
        public int TotalCount { get; init; }
        public int AllCount { get; init; }
        public int UnreadCount { get; init; }
        public List<string> Types { get; init; } = [];
    }

    public record MarkAsReadDto
    {
        public int NotificationId { get; set; }
    }
}