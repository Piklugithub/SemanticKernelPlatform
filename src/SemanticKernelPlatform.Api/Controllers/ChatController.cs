using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SemanticKernelPlatform.Application.Interfaces;
using SemanticKernelPlatform.Application.Models;

namespace SemanticKernelPlatform.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public sealed class ChatController : ControllerBase
    {
        private readonly IChatService _chatService;

        public ChatController(IChatService chatService)
        {
            _chatService = chatService;
        }

        [HttpPost]
        [ProducesResponseType(typeof(ChatResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<ChatResponse>> SendMessage(
            [FromBody] ChatRequest request,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(request.Message))
            {
                return BadRequest(new
                {
                    Error = "Message is required."
                });
            }

            var response = await _chatService.SendMessageAsync(
                request,
                cancellationToken);

            return Ok(response);
        }
    }
}
