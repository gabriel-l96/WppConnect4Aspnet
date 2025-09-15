using WppConnect4Aspnet.Data;
using Microsoft.AspNetCore.Mvc;
using WppConnect4Aspnet.Models;
using WppConnect4Aspnet.Services;

namespace WppConnect4Aspnet.Controllers
{
    [Route("api/[controller]")]
    public class WppStoriesController : ControllerBase
    {
        private readonly IPuppeteerWppService _wppService;
        private readonly ApiDbContext _context;
        public WppStoriesController(IPuppeteerWppService wppService, ApiDbContext context)
        {
            _wppService = wppService;
            _context = context;
        }
        [HttpPost("{sessionId}/stories/send-text")]
        public async Task<IActionResult> SendStatusText(string sessionId, [FromBody] TextStatusRequest request)
        {
            if (string.IsNullOrEmpty(request.Text))
            {
                return BadRequest("O texto a ser enviado não pode estar vazio.");
            }
            try
            {
                var result = await _wppService.SendStatusTextAsync(sessionId, request.Text, request.backgroundColor, request.font);
                return Ok(new { Message = "Comando de envio de status text executado com sucesso!", Data = result });
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { message = "Erro na requisição: ", error = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Message = "Erro ao enviar a o texto para status.", Error = ex.Message });
                //return new BadRequestObjectResult(new { error = ex.Message });
            }
        }
        [HttpPost("{sessionId}/stories/send-image")]
        [RequestSizeLimit(50 * 1024 * 1024)] // Limite de 50 MB para upload de vídeo
        public async Task<IActionResult> SendStatusImage(string sessionId, [FromBody] ImageStatusRequest request)
        {
            if (string.IsNullOrEmpty(request.FilePath))
            {
                return BadRequest("O caminho do arquivo não pode ser vazio.");
            }
            if (Request.ContentLength > 50 * 1024 * 1024)
            {
                return BadRequest("O tamanho do arquivo excede o limite de 50 MB.");
            }
            try
            {
                var result = await _wppService.SendStatusImageAsync(sessionId, request.FilePath, request.Caption);
                return Ok(new { Message = "Comando de envio de status image executado com sucesso!", Data = result });
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { message = "Erro na requisição: ", error = ex.Message });
            }
            catch (FileNotFoundException ex)
            {
                return NotFound(new { message = "Arquivo não encontrado: ", error = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Message = "Erro ao enviar a imagem para o status.", Error = ex.Message });
                //return new BadRequestObjectResult(new { error = ex.Message });
            }
        }
        [HttpPost("{sessionId}/stories/send-video")]
        [RequestSizeLimit(50 * 1024 * 1024)] // Limite de 50 MB para upload de vídeo
        public async Task<IActionResult> SendStatusVideo(string sessionId, [FromBody] ImageStatusRequest request)
        {
            if (string.IsNullOrEmpty(request.FilePath))
            {
                return BadRequest("O caminho do arquivo não pode ser vazio.");
            }
            if (Request.ContentLength > 50 * 1024 * 1024)
            {
                return BadRequest("O tamanho do arquivo excede o limite de 50 MB.");
            }
            try
            {
                var result = await _wppService.SendStatusVideoAsync(sessionId, request.FilePath, request.Caption);
                return Ok(new { Message = "Comando de envio de status image executado com sucesso!", Data = result });
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { message = "Erro na requisição: ", error = ex.Message });
            }
            catch (FileNotFoundException ex)
            {
                return NotFound(new { message = "Arquivo não encontrado: ", error = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Message = "Erro ao enviar o vídeo para o status.", Error = ex.Message });
                //return new BadRequestObjectResult(new { error = ex.Message });
            }
        }
        [HttpGet("{sessionId}/stories/GetAllStories")]
        public async Task<IActionResult> GetAllStories(string sessionId)
        {
            var session = await _context.WhatsappSessions.FindAsync(sessionId);
            if (session == null || session.Status != SessionStatus.Connected)
                return StatusCode(409, new { message = $"Sessão {sessionId} não encontrada ou desconectada." });
            try
            {
                var stories = await _wppService.GetAllStatusAsync(sessionId);
                return Ok(stories);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { message = "Erro na requisição: ", error = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = $"Erro ao obter a lista de status da sesssão {sessionId}: ", ex.Message });
            }
        }
        [Obsolete("Esse EndPoint não está funcional, possa ser que no futuro possamos implementa-lo novamente")]
        [HttpPost("{sessionId}/stories/{statusMessageId}")] 
        public async Task<IActionResult> DeleteStatusMessage(string sessionId,string to, string statusMessageId)
        {
            var session = await _context.WhatsappSessions.FindAsync(sessionId);
            if (session == null)
                return NotFound(new { error = $"Sessão {sessionId} não encont5rada." });
            try
            {
                var result = await _wppService.DeleteStatusMessageAsync(sessionId, to ,statusMessageId);
                return Ok(new { Message = "Comando de exclusão de status executado com sucesso!", Data = result });
            }
            catch (Exception ex)
            {
                return new BadRequestObjectResult(new { error = $"Erro ao excluir o status {statusMessageId} da sesssão {sessionId}: ", ex.Message });
            }
        }
    }
}
