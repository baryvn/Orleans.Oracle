namespace Orleans.Reminders.Oracle
{
    public class ReminderModel
    {
        public string? GRAIN_ID { get; set; }
        public string? REMINDER_NAME { get; set; } 
        public string? ETAG { get; set; }
        public uint? GRAIN_HASH { get; set; }
        public string? DATA { get; set; }
    }
}
