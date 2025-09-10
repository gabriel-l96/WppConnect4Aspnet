namespace WppConnect4Aspnet.Models
{
    public enum SessionStatus
    {
        Creating,
        WaitingForQrCode,
        Connected,
        Disconnected,
        Error
    }
}
