using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;
using BookingsApi.Tools;

namespace Bookings.Tests.MockTools
{
    // Minimal mock tool that returns fixed JSON payloads for deterministic tests
    public class MockCourtAvailabilityTool : ITool
    {
        private readonly string _payload;
        private readonly Dictionary<string, string>? _dateToPayload;

        public MockCourtAvailabilityTool(string payload)
        {
            _payload = payload;
        }

        // Overload: provide date->payload mapping for multi-day tests
        public MockCourtAvailabilityTool(Dictionary<string, string> dateToPayload)
        {
            _payload = string.Empty;
            _dateToPayload = dateToPayload;
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
            if (_dateToPayload != null)
            {
                var date = parameters != null && parameters.TryGetValue("date", out var d) ? d?.ToString() ?? string.Empty : string.Empty;
                if (!string.IsNullOrEmpty(date))
                {
                    // Normalize incoming date to dd MMM yy to match keys
                    if (DateTime.TryParse(date, out var dt))
                    {
                        var normalized = dt.ToString("dd MMM yy", CultureInfo.InvariantCulture);
                        if (_dateToPayload.TryGetValue(normalized, out var normPayload))
                        {
                            return Task.FromResult(normPayload);
                        }
                    }
                    if (_dateToPayload.TryGetValue(date, out var payload))
                    {
                        return Task.FromResult(payload);
                    }
                }
                // Fallback to the first mapped payload to keep tests deterministic if exact date string differs by locale
                foreach (var kv in _dateToPayload)
                {
                    return Task.FromResult(kv.Value);
                }
                return Task.FromResult("{\"Date\":\"\",\"Courts\":[]}");
            }
            return Task.FromResult(_payload);
        }
    }
}


