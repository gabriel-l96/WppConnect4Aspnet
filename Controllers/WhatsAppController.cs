using WppConnect4Aspnet.Data;
using Microsoft.AspNetCore.Mvc;
using WppConnect4Aspnet.Services;
using WppConnect4Aspnet.Models;

namespace WppConnect4Aspnet.Controllers
{
    [Route("api/[controller]")]
    public class WhatsAppController : ControllerBase
    {
        private readonly IPuppeteerWppService _wppService;
        private readonly ApiDbContext _context;
        public WhatsAppController(IPuppeteerWppService wppService, ApiDbContext context)
        {
            _wppService = wppService;
            _context = context;
        }
        [HttpPost("start/{sessionId}")]
        public async Task<IActionResult> StartSession(string sessionId)
        {
            try
            {
                if (string.IsNullOrEmpty(sessionId))
                    return BadRequest("O ID da sessão não pode ser vazio.");

                _ = _wppService.StartSessionAsync(sessionId);
                return Accepted(new { Message = "Iniciando a sessão. Monitore o estado para obter o QR Code." });
            }
            catch (Exception ex)
            {
                return new BadRequestObjectResult(new { error = ex.Message });
            }
        }
        [HttpGet("status/{sessionId}")]
        public async Task<IActionResult> GetSessionStatus(string sessionId)
        {
            try
            {
                var session = await _context.WhatsappSessions.FindAsync(sessionId);
                if (session == null)
                    return NotFound(new { error = "Sessão não encontrada." });
                return Ok(session);
            }
            catch (Exception ex)
            {
                return new BadRequestObjectResult(new { error = ex.Message });
            }
        }
        [HttpPost("send")]
        public async Task<IActionResult> SendMessage([FromBody] SendMessageRequest request)
        {
            try
            {
                if (string.IsNullOrEmpty(request.SessionId) || string.IsNullOrEmpty(request.To) || string.IsNullOrEmpty(request.Message))
                    return BadRequest("Parâmetros inválidos.");
                var result = await _wppService.SendTextMenssageAsync(request.SessionId, request.To, request.Message);
                return Ok(new { Message = "Comando de envio executado com sucesso!", Data = result });
            }
            catch (Exception ex)
            {
                return BadRequest(new { Message = "Erro ao enviar a mensagem", Error = ex.Message });
                //return new BadRequestObjectResult(new { error = ex.Message });
            }
        }
        [HttpPost("stop/{sessionId}")]
        public async Task<IActionResult> StopSession(string sessionId)
        {
            try
            {
                if (string.IsNullOrEmpty(sessionId))
                    return BadRequest("O ID da sessão não pode ser vazio.");
                await _wppService.StopSessionAsync(sessionId);
                return Ok(new { Message = "Sessão encerrada." });
            }
            catch (Exception ex)
            {
                return new BadRequestObjectResult(new { error = ex.Message });
            }
        }
    }
}
