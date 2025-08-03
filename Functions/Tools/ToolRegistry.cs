using System.Reflection;
using System.Collections.Generic;

namespace clubmanager_booking.Functions.Tools
{
    public class ToolRegistry
    {
        private readonly Dictionary<string, ITool> _tools = new();
        
        public ToolRegistry()
        {
            RegisterDefaultTools();
        }
        
        public void RegisterTool(ITool tool)
        {
            _tools[tool.Name] = tool;
        }
        
        public ITool? GetTool(string name)
        {
            return _tools.TryGetValue(name, out var tool) ? tool : null;
        }
        
        public IEnumerable<ITool> GetAllTools()
        {
            return _tools.Values;
        }
        
        private void RegisterDefaultTools()
        {
            // Register default tools here
            RegisterTool(new GetCurrentTimeTool());
            RegisterTool(new CalculateTool());
            RegisterTool(new GetCourtAvailabilityTool());
        }
    }
} 