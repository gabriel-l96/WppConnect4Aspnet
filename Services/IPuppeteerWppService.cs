namespace WppConnect4Aspnet.Services
{
    public interface IPuppeteerWppService
    {
        Task StartSessionAsync(string sessionId);
        Task<object> SendTextMenssageAsync(string sessionId, string number, string message);
        Task<object> SendStatusImageAsync(string sessionId, string filepath, string caption = "");
        Task<object> SendStatusVideoAsync(string sessionId, string filepath, string caption = "");
        Task<object> SendStatusTextAsync(string sessionId, string text, string? backgroundColor, int? font);
        Task<object?> GetAllStatusAsync(string sessionId);
        [Obsolete("Esse método não está funcional")]
        Task<object> DeleteStatusMessageAsync(string sessionId,string to, string statusMessageId);
        Task StopSessionAsync(string sessionId);
        Task StartSessionsFromDbAsync();
    }
}
