using System.Collections.Generic;
using System.Threading.Tasks;
using BookingsApi.Tools;

namespace Bookings.Tests.MockTools
{
    public class MockBoxPositionsTool : ITool
    {
        public string Name => "get_box_positions";
        public string Description => "Mocked tool for box positions";
        public Dictionary<string, object> Parameters => new()
        {
            ["type"] = "object"
        };

        private readonly string _json;
        public MockBoxPositionsTool(string json)
        {
            _json = json;
        }

        public Task<string> ExecuteAsync(Dictionary<string, object> parameters)
        {
            return Task.FromResult(_json);
        }
    }
}


