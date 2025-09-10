using System.Text.Json;

namespace WppConnect4Aspnet.Services
{
    public class WaJsService : IWaJsService
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly string _scriptsDirectory;
        private readonly string _scriptPath;
        private readonly string _verionFilePath;

        public WaJsService(IHttpClientFactory httpClientFactory)
        {
            _httpClientFactory = httpClientFactory;
            _scriptsDirectory = Path.Combine(AppContext.BaseDirectory, "Scripts");
            _scriptPath = Path.Combine(_scriptsDirectory, "wppconnect-wa.js");
            _verionFilePath = Path.Combine(_scriptsDirectory, "version.txt");
            if (!Directory.Exists(_scriptsDirectory))
            {
                Directory.CreateDirectory(_scriptsDirectory);
            }
        }
        public string GetScriptPath()
        {
            if (!File.Exists(_scriptPath))
            {
                throw new FileNotFoundException("O script wppconnect-wa.js não existe... Execute o método InitializeAsync a partir da inicialização.", _scriptPath);
            }
            return _scriptPath;
        }

        public async Task InitializeAsync()
        {
            Console.WriteLine("Iniciando verificação de versão do wppconnect-wa.js...");
            var client = _httpClientFactory.CreateClient();
            client.DefaultRequestHeaders.Add("User-Agent", "WppConnect4Aspnet");
            try
            {
                var response = await client.GetAsync("https://api.github.com/repos/wppconnect-team/wa-js/releases/latest");
                response.EnsureSuccessStatusCode();

                var json = await response.Content.ReadAsStringAsync();
                using var document = JsonDocument.Parse(json);
                var root = document.RootElement;
                var latestVersion = root.GetProperty("tag_name").GetString();
                string downloadUrl = root.GetProperty("assets").EnumerateArray()
                    .FirstOrDefault(asset => asset.GetProperty("name").GetString() == "wppconnect-wa.js")
                    .GetProperty("browser_download_url").GetString();

                if (string.IsNullOrEmpty(latestVersion) || string.IsNullOrEmpty(downloadUrl))
                {
                    Console.WriteLine("Não foi possível obter a versão mais recente ou a URL de download.");
                    return;
                }

                string localVersion = File.Exists(_verionFilePath) ? await File.ReadAllTextAsync(_verionFilePath) : null;

                if (latestVersion.Equals(localVersion, StringComparison.OrdinalIgnoreCase))
                {
                    Console.WriteLine($"A versão local do wppconnect-wa.js '{localVersion}' está atualizada.");
                    return;
                }
                Console.WriteLine($"Nova versão detectada: {latestVersion}. Baixando...");
                var scriptResponse = await client.GetAsync(downloadUrl);
                var scriptBytes = await client.GetByteArrayAsync(downloadUrl);
                await File.WriteAllBytesAsync(_scriptPath, scriptBytes);
                await File.WriteAllTextAsync(_verionFilePath, latestVersion);
                Console.WriteLine($"wppconnect-wa.js atualizado para a versão '{latestVersion}' .");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erro ao verificar ou baixar a versão mais recente do wppconnect-wa.js: {ex.Message}");
                if (!File.Exists(_scriptPath))
                {
                    throw new InvalidOperationException("Falha ao vaixar o wppconnect-wa.js, nenhuma versão local encontrada...");
                }
                Console.WriteLine("Usando a versão local existente do wppconnect-wa.js.");
            }
        }
    }
}
