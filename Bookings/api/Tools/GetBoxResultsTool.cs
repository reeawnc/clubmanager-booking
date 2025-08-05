using System.Collections.Generic;
using System.Threading.Tasks;
using System;
using System.Linq;
using Newtonsoft.Json;
using BookingsApi.Services;
using BookingsApi.Models;

namespace BookingsApi.Tools
{
    public class GetBoxResultsTool : ITool
    {
        public string Name => "get_box_results";
        
        public string Description => "Get box league results and match information. Use this when users ask about box league results, match outcomes, or player performance in box leagues.";
        
        public Dictionary<string, object> Parameters => new()
        {
            ["type"] = "object",
            ["properties"] = new Dictionary<string, object>
            {
                ["leagueId"] = new Dictionary<string, object>
                {
                    ["type"] = "integer",
                    ["description"] = "League ID to get results for (optional - will use default based on group type if not provided)"
                },
                ["groupType"] = new Dictionary<string, object>
                {
                    ["type"] = "string",
                    ["description"] = "Group type to get results for (default: 'Club'). Options: 'Club' or 'SummerFriendlies'"
                }
            }
        };
        
        public async Task<string> ExecuteAsync(Dictionary<string, object> parameters)
        {
            try
            {
                // Get parameters with defaults
                object leagueId = null;
                if (parameters.TryGetValue("leagueId", out var leagueIdValue))
                {
                    leagueId = Convert.ToInt32(leagueIdValue);
                }
                
                var groupType = BoxGroupType.SummerFriendlies; // default
                if (parameters.TryGetValue("groupType", out var groupTypeValue))
                {
                    if (Enum.TryParse<BoxGroupType>(groupTypeValue?.ToString(), true, out var parsedGroupType))
                    {
                        groupType = parsedGroupType;
                    }
                }
                
                // Use the shared box results service
                var boxResultsService = new BoxResultsService();
                var boxResults = await boxResultsService.GetBoxResultsAsync(groupType, leagueId);
                
                return JsonConvert.SerializeObject(boxResults);
            }
            catch (Exception ex)
            {
                return $"Error getting box results: {ex.Message}";
            }
        }
    }
} 