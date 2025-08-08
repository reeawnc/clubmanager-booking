using System.Collections.Generic;
using System.Threading.Tasks;
using BookingsApi.Tools;

namespace Bookings.Tests.MockTools
{
    // Minimal mock tool that returns fixed JSON payloads for deterministic tests
    public class MockCourtAvailabilityTool : ITool
    {
        private readonly string _payload;

        public MockCourtAvailabilityTool(string payload)
        {
            _payload = payload;
        }

        public string Name => "get_court_availability";
        public string Description => "Mocked court availability";
        public Dictionary<string, object> Parameters => new()
        {
            ["type"] = "object",
            ["properties"] = new Dictionary<string, object>()
        };

        public Task<string> ExecuteAsync(Dictionary<string, object> parameters)
        {
            return Task.FromResult(_payload);
        }
    }
}


