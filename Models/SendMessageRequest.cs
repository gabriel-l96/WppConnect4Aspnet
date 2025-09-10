namespace WppConnect4Aspnet.Models
{
    public class SendMessageRequest
    {
        public required string SessionId { get; set; }
        public required string To { get; set; }
        public required string Message { get; set; }
    }
}
