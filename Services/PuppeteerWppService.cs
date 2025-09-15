using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using PuppeteerSharp;
using QRCoder;
using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Text;
using System.Text.Json;
using WppConnect4Aspnet.Data;
using WppConnect4Aspnet.Models;

namespace WppConnect4Aspnet.Services
{
    public class PuppeteerWppService : IPuppeteerWppService, IAsyncDisposable
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly IWaJsService _waJsService;
        private readonly ConcurrentDictionary<string, IBrowser> _activeBrowsers = new();
        private readonly ConcurrentDictionary<string, IPage> _activePages = new();
        private static readonly HashSet<string> _aloowedImageExtensions = new HashSet<string>
        {
            ".jpg", ".jpeg",  ".png", ".gif", ".bmp", ".webp", ".tiff", ".tif"
        };
        private static readonly HashSet<string> _aloowedVideoExtensions = new HashSet<string>
        {
            ".mp4", ".mov", ".avi", ".3gp", ".mkv", ".webv"
        };

        private static readonly HashSet<string> _aloowedAudioExtensions = new HashSet<string>
        {
            ".mp3", ".aac", ".ogg", ".oga", ".opus", ".amr", ".wav", ".m4a"
        };

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
            try
            {
                var page = await CreateOrGetSessionsAsync(sessionId);

                page.Console += (sender, e) => Console.WriteLine($"[Browser Console] {e.Message.Type}: {e.Message.Text}");
                page.Error += (sender, e) => Console.WriteLine($"[Browser Page Error] {e.Error}");

                await page.SetUserAgentAsync("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/117.0.0.0 Safari/537.36");
                Console.WriteLine("A navegar para o WhatsApp Web...");
                await page.GoToAsync("https://web.whatsapp.com", new NavigationOptions
                {
                    WaitUntil = new[] { WaitUntilNavigation.DOMContentLoaded },
                    Timeout = 60000
                });
                //await page.WaitForSelectorAsync("#app", new WaitForSelectorOptions { Timeout = 20000 });

                await InjectWaJsAsync(page);

                await ConfigureListernersAsync(sessionId, page);

                Console.WriteLine($"Sessão {sessionId} iniciada com sucesso.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erro ao iniciar a sessão {sessionId}: " + ex.Message);
            }
        }
        public async Task<IPage> CreateOrGetSessionsAsync(string sessionId)
        {
            if (_activePages.TryGetValue(sessionId, out var existingPage))
                return existingPage;

            var userDataDir = Path.Combine(AppContext.BaseDirectory, "sessions", sessionId);
            if (!Directory.Exists(userDataDir))
            {
                Directory.CreateDirectory(userDataDir);
            }
            var browser = await Puppeteer.LaunchAsync(new LaunchOptions
            {
                Headless = true,
                UserDataDir = userDataDir,
                Args = new[] { "--no-sandbox", "--disable-setuid-sandbox", "--disabe-web-security", "--disable-blink-features=AutomationControlled", "--no-first-run" }
            });

            var page = (await browser.PagesAsync()).FirstOrDefault() ?? await browser.NewPageAsync();
            _activeBrowsers[sessionId] = browser;
            _activePages[sessionId] = page;

            return page;
        }
        public async Task InjectWaJsAsync(IPage page)
        {
            await page.WaitForSelectorAsync("#app", new WaitForSelectorOptions { Timeout = 20000 });
            Console.WriteLine("Interface do WhatsApp detectada. A injetar script wa-js...");

            var scriptPath = _waJsService.GetScriptPath();
            var scriptContent = await File.ReadAllTextAsync(scriptPath);

            await page.EvaluateExpressionAsync(scriptContent);
            Console.WriteLine("A aguardar pela inicialização completa do WA-JS...");

            await page.WaitForFunctionAsync("() => window.WPP && window.WPP.isReady", new WaitForFunctionOptions { Timeout = 20000 });

            Console.WriteLine("WA-JS injetado com sucesso e está pronto para uso.");
        }
        public async Task ConfigureListernersAsync(string sessionId, IPage page)
        {
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
            await page.EvaluateExpressionAsync(@"
                    WPP.on('conn.auth_code_change', (data) => {
                        console.log('Evento ""conn.auth_code_change"" detectado.');
                        if (data && data.fullCode) {
                            window.onQrCode(data.fullCode);
                        }
                    });
                    WPP.on('conn.main_ready', () => {
                        console.log('Evento ""conn.main_ready"" detectado.');
                        window.onStatusChange('CONNECTED');
                    });
                    console.log('Listeners do WPP configurados.');");

            Console.WriteLine("Listeners do WA-JS configurados.");
        }
        public async Task StartSessionsFromDbAsync()
        {
            using var scope = _serviceProvider.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<ApiDbContext>();

            var sessions = dbContext.WhatsappSessions.ToList();

            foreach (var session in sessions)
            {
                if (_activeBrowsers.ContainsKey(session.SessionId)) continue;

                if (session.Status == SessionStatus.Connected || session.Status == SessionStatus.WaitingForQrCode)
                {
                    try
                    {
                        Console.WriteLine($"Tentando reiniciar a sessão {session.SessionId} do banco de dados...");
                        await StartSessionAsync(session.SessionId);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Erro ao reiniciar a sessão {session.SessionId}: {ex.Message}");
                        await UpdateSessionStatus(session.SessionId, SessionStatus.Disconnected, "");
                    }
                }
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
return WPP.chat.sendTextMessage(number, decodedMessage, {createChat: true})
            }";
            var result = await page.EvaluateFunctionAsync<JsonElement>(script, numberFormated, messageBase64);
            var get = result.GetType().FullName;
            var msg = JsonSerializer.Deserialize<Message>(result.GetRawText());
            return msg;
            //return await page.EvaluateFunctionAsync(command);
        }
        public async Task<object> SendStatusTextAsync(string sessionId, string message, string? backgroundColor, int? font = 2)
        {
            if (string.IsNullOrWhiteSpace(backgroundColor))
            {
                backgroundColor = "#0275d8";
            }
            var opts = new { backgroundColor, font };
            var messageBase64 = Convert.ToBase64String(Encoding.Default.GetBytes(message));

            if (!_activePages.TryGetValue(sessionId, out var page))
            {
                throw new InvalidOperationException($"A sessão com ID '{sessionId}' não está ativa.");
            }
            string script = @"(base64Text, opts) =>{
                        const binaryString = atob(base64Text);            
                        const len = binaryString.length;
                        const bytes = new Uint8Array(len);
                        for (let i = 0; i < len; i++){
                            bytes[i] = binaryString.charCodeAt(i);
                        }
                        const decodedMessage = new TextDecoder('utf-8').decode(bytes);
            return WPP.status.sendTextStatus(decodedMessage, {opts})
                        }";

            var result = await page.EvaluateFunctionAsync<JsonElement>(script, [messageBase64, opts]);
            var msg = JsonSerializer.Deserialize<Message>(result.GetRawText());
            return msg;
        }
        [Obsolete("Esse método não está funcional")]
        public async Task<object> DeleteStatusMessageAsync(string sessionId,string to ,string messageId)
        {
            if (!_activePages.TryGetValue(sessionId, out var page))
            {
                throw new InvalidOperationException($"A sessão com ID '{sessionId}' não está ativa.");
            }
            var script = @"async (chatId, msgId) => { return await WPP.chat.deleteMessage(chatId, msgId);}";
            //var result = await page.EvaluateFunctionAsync<JsonElement>(script, to, messageId);
            //var options = new JsonSerializerOptions
            //{
            //    // Garante que o deserializador não seja sensível a maiúsculas/minúsculas
            //    PropertyNameCaseInsensitive = true
            //};
            //var msg = JsonSerializer.Deserialize<DeleteMessageResult>(result.GetRawText(), options);
            //return msg;

            var result = await page.EvaluateFunctionAsync<object>(script, new object[] { to, messageId } );

            // Opcional: Log para depuração no C#
            var resultJson = System.Text.Json.JsonSerializer.Serialize(result);
            Console.WriteLine($"[DEBUG C#] Resultado da exclusão: {resultJson}");

            return result;
        }
        public async Task<object> SendStatusImageAsync(string sessionId, string filepath, string caption = "")
        {

            if (string.IsNullOrEmpty(filepath))
                throw new FileNotFoundException("O arquivo especificado não foi encontrado.", filepath);
            if (!_activePages.TryGetValue(sessionId, out var page))
            {
                throw new InvalidOperationException($"A sessão com ID '{sessionId}' não está ativa.");
            }
            var ext = Path.GetExtension(filepath).ToLowerInvariant();
            if (string.IsNullOrEmpty(ext) || !_aloowedImageExtensions.Contains(ext))
            {
                throw new InvalidOperationException("Apenas arquivos de imagem são suportados para status de imagem.");
            }
            byte[] fileBytes = await File.ReadAllBytesAsync(filepath);
            string base64Image = Convert.ToBase64String(fileBytes);
            string minetype = GetMimeType(filepath);
            string dataUrl = $"data:{minetype};base64,{base64Image}";

            return await page.EvaluateFunctionAsync<object>($@"async => WPP.status.sendImageStatus('" + dataUrl + "', {caption: '" + caption + "'})");
        }
        public async Task<object> SendStatusVideoAsync(string sessionId, string filepath, string caption = "")
        {
            if (string.IsNullOrEmpty(filepath) || !File.Exists(filepath))
                throw new FileNotFoundException("O arquivo especificado não foi encontrado.", filepath);
            if (!_activePages.TryGetValue(sessionId, out var page))
            {
                throw new InvalidOperationException($"A sessão com ID '{sessionId}' não está ativa.");
            }
            var ext = Path.GetExtension(filepath).ToLowerInvariant();
            if (string.IsNullOrEmpty(ext) || !_aloowedVideoExtensions.Contains(ext))
            {
                throw new InvalidOperationException("Apenas arquivos de vídeo são suportados para status de vídeos.");
            }
            byte[] fileBytes = await File.ReadAllBytesAsync(filepath);
            string base64Image = Convert.ToBase64String(fileBytes);
            string minetype = GetMimeType(filepath);
            string dataUrl = $"data:{minetype};base64,{base64Image}";

            return await page.EvaluateFunctionAsync<object>($@"async => WPP.status.sendVideoStatus('" + dataUrl + "', {caption: '" + caption + "'})");
        }
        public async Task<object?> GetAllStatusAsync(string sessionId)
        {
            if (!_activePages.TryGetValue(sessionId, out var page))
            {
                throw new InvalidOperationException($"A sessão com ID '{sessionId}' não está ativa.");
            }
            try
            {
                var resultJson = await page.EvaluateFunctionAsync<string>(@"
            async () => {
                const status = await WPP.status.getMyStatus();
                return JSON.stringify(status, null, 2); // O 'null, 2' formata o JSON para facilitar a leitura
            }
        ");
                return resultJson;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("Erro ao obter os status da sessão: " + ex.Message);
            }
        }
        //public async Task<object> DeleteStatusMessageAsync(string sessionId, string statusMessageId)
        //{
        //    if (string.IsNullOrEmpty(statusMessageId))
        //        throw new ArgumentException("O ID da mensagem de status não pode estar vazio.");
        //    if (!_activePages.TryGetValue(sessionId, out var page))
        //    {
        //        throw new InvalidOperationException($"A sessão com ID '{sessionId}' não está ativa.");
        //    }
        //    try
        //    {
        //        //const string statudChatId = "status@broadcast";
        //        return await page.EvaluateFunctionAsync<object>("([msgId]) => {return WPP.status.remove([msgId]);}", [statusMessageId]);
        //    }
        //    catch (Exception ex)
        //    {
        //        throw new InvalidOperationException("Erro ao deletar a mensagem de status: " + ex.Message);
        //    }
        //}
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
            session.LastActivity = DateTime.Now;
            await dbContext.SaveChangesAsync();
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
        public async ValueTask DisposeAsync()
        {
            foreach (var browser in _activeBrowsers.Values)
            {
                await browser.CloseAsync();
            }
        }

        public string GetMimeType(string filePath)
        {
            var extension = Path.GetExtension(filePath).ToLowerInvariant();
            return extension switch
            {
                // IMAGENS
                ".jpg" or ".jpeg" => "image/jpeg",
                ".png" => "image/png",
                ".gif" => "image/gif",
                ".bmp" => "image/bmp",
                ".webp" => "image/webp",
                "tiff" or ".tif" => "image/tiff",
                // VÍDEOS
                ".mp4" => "video/mp4",
                ".mov" => "video/quicktime",
                ".avi" => "video/x-msvideo",
                ".3gp" => "video/3gpp",
                ".mkv" => "video/x-matroska",
                ".webv" => "video/webm",
                // ÁUDIO
                ".mp3" => "audio/mpeg",
                ".aac" => "audio/aac",
                ".ogg" or ".oga" => "audio/ogg",
                ".opus" => "audio/opus",
                ".amr" => "audio/amr",
                ".wav" => "audio/wav",
                ".m4a" => "audio/mp4",
                // DOCUMENTOS
                ".pdf" => "application/pdf",
                ".txt" => "text/plain",
                ".csv" => "text/csv",
                "rtf" => "application/rtf",
                "vcf" or ".vcard" => "text/vcard",
                ".doc" => "application/msword",
                ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
                ".xls" => "application/vnd.ms-excel",
                ".xlsx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                ".ppt" => "application/vnd.ms-powerpoint",
                ".pptx" => "application/vnd.openxmlformats-officedocument.presentationml.presentation",
                ".odt" => "application/vnd.oasis.opendocument.text",
                ".ods" => "application/vnd.oasis.opendocument.spreadsheet",
                ".odp" => "application/vnd.oasis.opendocument.presentation",
                // COMPACTADOS
                ".zip" => "application/zip",
                ".rar" => "application/vnd.rar",
                ".7z" => "application/x-7z-compressed",
                ".tar" => "application/x-tar",
                ".gz" => "application/gzip",
                // Outros tipos comuns
                _ => "application/octet-stream",
            };
        }
    }
}