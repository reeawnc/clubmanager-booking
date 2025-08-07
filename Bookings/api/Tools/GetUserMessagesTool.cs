using System.Collections.Generic;
using System.Threading.Tasks;
using BookingsApi.Services;

namespace BookingsApi.Tools
{
    public class GetUserMessagesTool : ITool
    {
        public string Name => "get_user_messages";
        public string Description => "Fetches user messages (inbox) from ClubManager. Params: markAsRead (bool), showExpired (bool), showRead (bool)";
        public Dictionary<string, object> Parameters => new()
        {
            ["type"] = "object",
            ["properties"] = new Dictionary<string, object>
            {
                ["markAsRead"] = new Dictionary<string, object>
                {
                    ["type"] = "boolean",
                    ["description"] = "Mark messages as read"
                },
                ["showExpired"] = new Dictionary<string, object>
                {
                    ["type"] = "boolean",
                    ["description"] = "Include expired messages"
                },
                ["showRead"] = new Dictionary<string, object>
                {
                    ["type"] = "boolean",
                    ["description"] = "Include read messages"
                }
            }
        };

        private readonly UserMessagesService _service = new();

        public async Task<string> ExecuteAsync(Dictionary<string, object> parameters)
        {
            var markAsRead = parameters.TryGetValue("markAsRead", out var mar) && mar is bool b1 && b1;
            var showExpired = parameters.TryGetValue("showExpired", out var se) && se is bool b2 && b2;
            var showRead = parameters.TryGetValue("showRead", out var sr) && sr is bool b3 ? b3 : true;
            return await _service.GetUserMessagesAsync(markAsRead, showExpired, showRead);
        }
    }
}


