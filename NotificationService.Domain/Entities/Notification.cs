namespace NotificationService.Domain.Entities
{
    public class Notification
    {
        public int Id { get; private set; }
        public int UserId { get; private set; }
        public string Type { get; private set; }=string.Empty;
        public string Title { get; private set; }=string.Empty;
        public string Message { get; private set; } = string.Empty;
        public string? Data { get; private set; }
        public bool IsRead {  get; private set; }
        public DateTime CreatedAt {  get; private set; }
        public DateTime? ReadAt { get; private set; }

        protected Notification() { }
        public Notification(int userId,string type,string title,string message,string? data = null)
        {
            UserId= userId;
            Type= type;
            Title= title;
            Message= message;
            Data= data;
            IsRead = false;
            CreatedAt = DateTime.Now;
        }
        public void MarkAsRead()
        {
            if (!IsRead)
            {
                IsRead = true;
                ReadAt= DateTime.Now;
            }
        }
    }
}