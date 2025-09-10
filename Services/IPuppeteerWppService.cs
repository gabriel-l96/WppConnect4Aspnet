namespace WppConnect4Aspnet.Services
{
    public interface IPuppeteerWppService
    {
        Task StartSessionAsync(string sessionId);
        Task <object> SendTextMenssageAsync(string sessionId, string number, string message);
        Task StopSessionAsync (string sessionId);
    }
}
