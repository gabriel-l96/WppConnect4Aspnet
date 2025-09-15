using WppConnect4Aspnet.Data;
using Microsoft.AspNetCore.Mvc;
using WppConnect4Aspnet.Models;
using WppConnect4Aspnet.Services;

namespace WppConnect4Aspnet.Controllers
{
    [Route("api/[controller]")]
    public class WppChatController : ControllerBase
    {
        private readonly IPuppeteerWppService _wppService;
        private readonly ApiDbContext _context;
        public WppChatController(IPuppeteerWppService wppService, ApiDbContext context)
        {
            _wppService = wppService;
            _context = context;
        }
        [HttpPost("send/{sessionId}/textmessage")]
        public async Task<IActionResult> SendMessage(string sessionId, [FromBody] SendMessageRequest request)
        {
            try
            {
                if (string.IsNullOrEmpty(sessionId) || string.IsNullOrEmpty(request.To) || string.IsNullOrEmpty(request.Message))
                    return BadRequest("Parâmetros inválidos.");
                var result = await _wppService.SendTextMenssageAsync(sessionId, request.To, request.Message);
                return StatusCode(200,new { Message = "Comando de envio executado com sucesso!", Data = result });
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { message = "Erro na requisição.", error = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Message = "Erro ao enviar a mensagem", Error = ex.Message });
            }
        }
    }
}
