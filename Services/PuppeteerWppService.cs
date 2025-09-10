using QRCoder;
using PuppeteerSharp;
using WppConnect4Aspnet.Data;
using WppConnect4Aspnet.Models;
using System.Collections.Concurrent;
using Microsoft.AspNetCore.Diagnostics;
using System.Text;


namespace WppConnect4Aspnet.Services
{
    public class PuppeteerWppService : IPuppeteerWppService, IAsyncDisposable
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly IWaJsService _waJsService;
        private readonly ConcurrentDictionary<string, IBrowser> _activeBrowsers = new();
        private readonly ConcurrentDictionary<string, IPage> _activePages = new();

        public PuppeteerWppService(IServiceProvider serviceProvider, IWaJsService waJsService)
        {
            _serviceProvider = serviceProvider;
            _waJsService = waJsService;

            Console.WriteLine("Baixando o Chromium, se necessário...");
            _ = new BrowserFetcher().DownloadAsync().GetAwaiter().GetResult();
            Console.WriteLine("Serviço PuppeteerWppService inicializado.");
        }

        public async Task StartSessionAsync(string sessionId)
        {
            if (_activeBrowsers.ContainsKey(sessionId)) return;

            await UpdateSessionStatus(sessionId, SessionStatus.Creating);

            var userDataDir = Path.Combine(AppContext.BaseDirectory, "sessions", sessionId);
            Directory.CreateDirectory(userDataDir);

            var browser = await Puppeteer.LaunchAsync(new LaunchOptions
            {
                Headless = true,
                UserDataDir = userDataDir,
                Args = new[] { "--no-sandbox", "--disable-setuid-sandbox", "--disabe-web-security", "--disable-blink-features=AutomationControlled", "--no-first-run" }
            });
            _activeBrowsers.TryAdd(sessionId, browser);

            var page = (await browser.PagesAsync()).FirstOrDefault() ?? await browser.NewPageAsync();
            _activePages.TryAdd(sessionId, page);

            // Listener melhorado para erros da página
            page.Console += (sender, e) => Console.WriteLine($"[Browser Console] {e.Message.Type}: {e.Message.Text}");
            page.Error += (sender, e) => Console.WriteLine($"[Browser Page Error] {e.Error}");

            await page.ExposeFunctionAsync("onQrCode", async (string qrCode) =>
            {
                Console.WriteLine($"Código QR recebido para {sessionId}. A gerar no console...");
                QRCodeGenerator qrGenerator = new QRCodeGenerator();
                QRCodeData qrCodeData = qrGenerator.CreateQrCode(qrCode, QRCodeGenerator.ECCLevel.Q);

                using (AsciiQRCode qrCodeAscii = new AsciiQRCode(qrCodeData))
                {
                    string asciiQr = qrCodeAscii.GetGraphic(1);
                    Console.WriteLine(asciiQr);
                }
                await UpdateSessionStatus(sessionId, SessionStatus.WaitingForQrCode, qrCode);
            });

            await page.ExposeFunctionAsync("onStatusChange", async (string status) =>
            {
                Console.WriteLine($"Estado da sessão {sessionId} alterado para: {status}");
                var newStatus = status == "CONNECTED" ? SessionStatus.Connected : SessionStatus.Disconnected;
                var qrCodeToClear = newStatus == SessionStatus.Connected ? "" : null;
                await UpdateSessionStatus(sessionId, newStatus, qrCodeToClear);
            });

            try
            {
                await page.SetUserAgentAsync("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/117.0.0.0 Safari/537.36");

                Console.WriteLine("A navegar para o WhatsApp Web...");
                await page.GoToAsync("https://web.whatsapp.com", new NavigationOptions
                {
                    WaitUntil = new[] { WaitUntilNavigation.DOMContentLoaded },
                    Timeout = 60000
                });

                Console.WriteLine("Navegação concluída. A aguardar pela renderização da interface do WhatsApp...");

                await page.WaitForSelectorAsync("#app", new WaitForSelectorOptions { Timeout = 10000 });
                Console.WriteLine("Interface do WhatsApp detetada. A injetar script wa-js...");

                var scriptPath = _waJsService.GetScriptPath();
                var scriptContent = await File.ReadAllTextAsync(scriptPath);

                await page.EvaluateExpressionAsync(scriptContent);

                Console.WriteLine("A aguardar pela inicialização completa do WPP...");

                await page.WaitForFunctionAsync("() => window.WPP && window.WPP.isReady", new WaitForFunctionOptions { Timeout = 10000 });

                Console.WriteLine("WPP está pronto. A configurar listeners...");

                await page.EvaluateExpressionAsync(@"
                    WPP.on('conn.auth_code_change', (data) => {
                        console.log('Evento ""conn.auth_code_change"" detetado.');
                        if (data && data.fullCode) {
                            window.onQrCode(data.fullCode);
                        }
                    });
                    WPP.on('conn.main_ready', () => {
                        console.log('Evento ""conn.main_ready"" detetado.');
                        window.onStatusChange('CONNECTED');
                    });
                    console.log('Listeners do WPP configurados.');
                ");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ERRO CRÍTICO durante a navegação ou injeção: {ex.Message}");
                await StopSessionAsync(sessionId);
            }
        }
        public async Task<object> SendTextMenssageAsync(string sessionId, string to, string message)
        {
            string numberFormated = $"{to}@c.us";
            var messageBase64 = Convert.ToBase64String(Encoding.Default.GetBytes(message));
            if (!_activePages.TryGetValue(sessionId, out var page))
            {
                throw new InvalidOperationException($"A sessão com ID '{sessionId}' não está ativa.");
            }
            //string command = "async => WPP.chat.sendTextMessage('"+ numberFormated + "','"+ messageBase64 + "', { createChat: true })";
            string script = @"(number, base64Text) =>{
            const binaryString = atob(base64Text);            
            const len = binaryString.length;
            const bytes = new Uint8Array(len);
            for (let i = 0; i < len; i++){
                bytes[i] = binaryString.charCodeAt(i);
            }
            const decodedMessage = new TextDecoder('utf-8').decode(bytes);
WPP.chat.sendTextMessage(number, decodedMessage, { createChat: true })
            }";
            return await page.EvaluateFunctionAsync<object>(script, numberFormated, messageBase64);
            //return await page.EvaluateFunctionAsync(command);
        }

        public async Task StopSessionAsync(string sessionId)
        {
            if (_activeBrowsers.TryRemove(sessionId, out var browser))
            {
                await browser.CloseAsync();
                browser.Dispose();
            }
            if (_activePages.TryRemove(sessionId, out var page))
            {
                await page.CloseAsync();
                page.Dispose();
            }
            var useDataDir = Path.Combine(AppContext.BaseDirectory, "sessions", sessionId);
            if (Directory.Exists(useDataDir))
            {
                try
                {
                    Directory.Delete(useDataDir, true);
                }
                catch (IOException ex)
                {
                    Console.WriteLine($"Erro ao deletar o diretório de sessão: {ex.Message}");
                }
            }
            await UpdateSessionStatus(sessionId, SessionStatus.Disconnected, "");
        }

        public async Task UpdateSessionStatus(string sessionId, SessionStatus status, string qrCode = null)
        {
            using var scope = _serviceProvider.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<ApiDbContext>();
            var session = await dbContext.WhatsappSessions.FindAsync(sessionId);
            if (session == null)
            {
                session = new WhatsappSession { SessionId = sessionId };
                dbContext.WhatsappSessions.Add(session);
            }
            session.Status = status;
            if (qrCode != null)
            {
                session.QrCodeBase64 = string.IsNullOrEmpty(qrCode) ? null : qrCode;
            }
            session.LastActivity = DateTime.UtcNow;
            await dbContext.SaveChangesAsync();
        }

        public async ValueTask DisposeAsync()
        {
            foreach (var browser in _activeBrowsers.Values)
            {
                await browser.CloseAsync();
            }
        }
    }
}