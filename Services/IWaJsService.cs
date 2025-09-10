namespace WppConnect4Aspnet.Services
{
    public interface IWaJsService
    {
        Task InitializeAsync();
        string GetScriptPath();
    }
}
