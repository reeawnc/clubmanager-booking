using System.Data;
using System.Collections.Generic;
using System.Threading.Tasks;
using System;

namespace BookingsApi.Tools
{
    public class CalculateTool : ITool
    {
        public string Name => "calculate";
        
        public string Description => "Perform mathematical calculations";
        
        public Dictionary<string, object> Parameters => new()
        {
            ["type"] = "object",
            ["properties"] = new Dictionary<string, object>
            {
                ["expression"] = new Dictionary<string, object>
                {
                    ["type"] = "string",
                    ["description"] = "Mathematical expression to evaluate"
                }
            },
            ["required"] = new[] { "expression" }
        };
        
        public async Task<string> ExecuteAsync(Dictionary<string, object> parameters)
        {
            if (!parameters.TryGetValue("expression", out var expr) || expr == null)
            {
                return "Error: Expression parameter is required";
            }
            
            try
            {
                var expression = expr.ToString() ?? "";
                var dt = new DataTable();
                var result = dt.Compute(expression, "");
                return await Task.FromResult(result.ToString() ?? "Error");
            }
            catch (Exception ex)
            {
                return await Task.FromResult($"Error evaluating expression: {ex.Message}");
            }
        }
    }
}