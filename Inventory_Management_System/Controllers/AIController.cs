using Microsoft.AspNetCore.Authorization;

namespace Inventory_Management_System.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class AIController : ControllerBase
    {
        private readonly IAIService _aiService;
        private readonly ApplicationDbContext _context;

        public AIController(IAIService aiService, ApplicationDbContext context)
        {
            _aiService = aiService;
            _context = context;
        }

        [HttpPost("chat")]
        public async Task<IActionResult> Chat([FromBody] AIRequest request)
        {
            if (string.IsNullOrWhiteSpace(request?.Message))
            {
                return BadRequest(new
                {
                    message = "Question cannot be empty."
                });
            }

            var trimmedQuery = request.Message.Trim();

            string response;
            try
            {
                response = await _aiService.AskAsync(trimmedQuery);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error during AskAsync: {ex.Message}");
                response = "Sorry, an error occurred while processing your request. Please try again.";
            }

            if (string.IsNullOrWhiteSpace(response))
            {
                response = "No response generated.";
            }

            // Persist the interaction into AIChatLogs via ApplicationDbContext
            var chatLog = new AIChatLog
            {
                UserQuery = trimmedQuery.Length > 1000 ? trimmedQuery.Substring(0, 1000) : trimmedQuery,
                AIResponse = response,
                CreatedAt = DateTime.Now
            };

            _context.AIChatLogs.Add(chatLog);
            await _context.SaveChangesAsync();

            return Ok(new
            {
                id = chatLog.LogID,
                question = trimmedQuery,
                userQuery = trimmedQuery,
                userMessage = trimmedQuery,
                response = response,
                aiResponse = response,
                createdAt = chatLog.CreatedAt
            });
        }

        [HttpGet("history")]
        public async Task<IActionResult> GetHistory([FromQuery] int limit = 50)
        {
            var takeCount = Math.Clamp(limit, 1, 200);

            // Fetch recent chat records and order chronologically (oldest to newest)
            var logs = await _context.AIChatLogs
                .OrderByDescending(c => c.CreatedAt)
                .ThenByDescending(c => c.LogID)
                .Take(takeCount)
                .OrderBy(c => c.CreatedAt)
                .ThenBy(c => c.LogID)
                .Select(c => new
                {
                    id = c.LogID,
                    userQuery = c.UserQuery,
                    aiResponse = c.AIResponse,
                    createdAt = c.CreatedAt,
                    userMessage = c.UserQuery,
                    message = c.UserQuery,
                    response = c.AIResponse,
                    timestamp = c.CreatedAt.ToString("o")
                })
                .ToListAsync();

            return Ok(logs);
        }

        [HttpDelete("history")]
        [HttpPost("clear")]
        [HttpDelete("clear")]
        [HttpPost("history/clear")]
        public async Task<IActionResult> ClearHistory()
        {
            try
            {
                _context.AIChatLogs.RemoveRange(_context.AIChatLogs);
                await _context.SaveChangesAsync();

                return Ok(new
                {
                    success = true,
                    message = "Chat history cleared successfully."
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error clearing AIChatLogs: {ex.Message}");
                return StatusCode(500, new
                {
                    success = false,
                    message = "Failed to clear chat history."
                });
            }
        }
    }

    public class AIRequest
    {
        public string Message { get; set; }
    }
}
