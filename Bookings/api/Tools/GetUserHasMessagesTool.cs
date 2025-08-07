using System.Collections.Generic;
using System.Threading.Tasks;
using BookingsApi.Services;

namespace BookingsApi.Tools
{
    public class GetUserHasMessagesTool : ITool
    {
        public string Name => "get_user_has_messages";
        public string Description => "Checks if the user has messages in ClubManager";
        public Dictionary<string, object> Parameters => new() { };

        private readonly UserMessagesService _service = new();

        public async Task<string> ExecuteAsync(Dictionary<string, object> parameters)
        {
            return await _service.GetUserHasMessagesAsync();
        }
    }
}


