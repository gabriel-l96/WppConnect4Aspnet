using System.ComponentModel.DataAnnotations;

namespace WppConnect4Aspnet.Models
{
    public class WhatsappSession
    {
        [Key]
        [Required]
        public string SessionId { get; set; }
        [Required]
        public SessionStatus Status { get; set; }
        public string? QrCodeBase64 { get; set; }
        public DateTime LastActivity { get; set; } 

    }
}
