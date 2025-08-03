using System.Collections.Generic;
using System.Threading.Tasks;

namespace clubmanager_booking.Functions.Tools
{
    public interface ITool
    {
        string Name { get; }
        string Description { get; }
        Dictionary<string, object> Parameters { get; }
        Task<string> ExecuteAsync(Dictionary<string, object> parameters);
    }
} 