using System.Collections.Generic;
using System.Threading.Tasks;
using System;
using System.Linq;
using Newtonsoft.Json;
using BookingsApi.Services;

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
                    ["description"] = "League ID to get results for (default: 4076)"
                },
                ["groupId"] = new Dictionary<string, object>
                {
                    ["type"] = "string",
                    ["description"] = "Group ID to get results for (default: '216')"
                }
            }
        };
        
        public async Task<string> ExecuteAsync(Dictionary<string, object> parameters)
        {
            try
            {
                // Get parameters with defaults
                var leagueId = parameters.TryGetValue("leagueId", out var leagueIdValue) 
                    ? Convert.ToInt32(leagueIdValue) 
                    : 4076;
                
                var groupId = parameters.TryGetValue("groupId", out var groupIdValue) 
                    ? groupIdValue?.ToString() 
                    : "216";
                
                // Use the shared box results service
                var boxResultsService = new BoxResultsService();
                var boxResults = await boxResultsService.GetBoxResultsAsync(leagueId, groupId);
                
                return JsonConvert.SerializeObject(boxResults);
            }
            catch (Exception ex)
            {
                return $"Error getting box results: {ex.Message}";
            }
        }
    }
} 