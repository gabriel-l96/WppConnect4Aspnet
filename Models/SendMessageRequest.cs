namespace WppConnect4Aspnet.Models
{
    public class SendMessageRequest
    {
        public string SessionId { get; set; }
        public string To { get; set; }
        public string Message { get; set; }
    }
}
