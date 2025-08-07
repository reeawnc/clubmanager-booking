using System.Collections.Generic;
using System.Threading.Tasks;
using BookingsApi.Services;

namespace BookingsApi.Tools
{
    public class GetSentUserMessagesTool : ITool
    {
        public string Name => "get_sent_user_messages";
        public string Description => "Fetches the user's sent messages from ClubManager";
        public Dictionary<string, object> Parameters => new()
        {
            ["type"] = "object",
            ["properties"] = new Dictionary<string, object>()
        };

        private readonly UserMessagesService _service = new();

        public async Task<string> ExecuteAsync(Dictionary<string, object> parameters)
        {
            return await _service.GetSentUserMessagesAsync();
        }
    }
}


