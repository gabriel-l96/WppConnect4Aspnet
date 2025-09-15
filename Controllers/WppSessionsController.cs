using WppConnect4Aspnet.Data;
using Microsoft.AspNetCore.Mvc;
using WppConnect4Aspnet.Services;
using WppConnect4Aspnet.Models;

namespace WppConnect4Aspnet.Controllers
{
    [Route("api/[controller]")]
    public class WppSessionsController : ControllerBase
    {
        private readonly IPuppeteerWppService _wppService;
        private readonly ApiDbContext _context;
        public WppSessionsController(IPuppeteerWppService wppService, ApiDbContext context)
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

                await _wppService.StartSessionAsync(sessionId);
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
