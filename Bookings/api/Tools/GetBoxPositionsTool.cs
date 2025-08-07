using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using BookingsApi.Services;
using BookingsApi.Models;

namespace BookingsApi.Tools
{
    public class GetBoxPositionsTool : ITool
    {
        public string Name => "get_box_positions";
        public string Description => "Fetches box league positions. Provide either groupId (string/number) or group (BoxGroupType name: Club, SummerFriendlies).";
        public Dictionary<string, object> Parameters => new()
        {
            ["type"] = "object",
            ["properties"] = new Dictionary<string, object>
            {
                ["groupId"] = new Dictionary<string, object>
                {
                    ["type"] = "string",
                    ["description"] = "Group ID (e.g., '216')"
                },
                ["group"] = new Dictionary<string, object>
                {
                    ["type"] = "string",
                    ["description"] = "Group name (BoxGroupType): Club or SummerFriendlies"
                }
            }
        };

        private readonly BoxPositionsService _service = new();

        public async Task<string> ExecuteAsync(Dictionary<string, object> parameters)
        {
            // Prefer explicit groupId; otherwise allow enum name via 'group'
            string? groupId = null;
            if (parameters.TryGetValue("groupId", out var g))
            {
                groupId = g?.ToString();
            }

            if (string.IsNullOrWhiteSpace(groupId) && parameters.TryGetValue("group", out var gn) && gn != null)
            {
                var groupName = gn.ToString();
                if (!string.IsNullOrWhiteSpace(groupName) && Enum.TryParse<BoxGroupType>(groupName, true, out var groupEnum))
                {
                    groupId = ((int)groupEnum).ToString();
                }
            }

            if (string.IsNullOrWhiteSpace(groupId))
            {
                throw new ArgumentException("Provide either 'groupId' or 'group' (Club, SummerFriendlies)");
            }

            return await _service.GetBoxPositionsAsync(groupId);
        }
    }
}


