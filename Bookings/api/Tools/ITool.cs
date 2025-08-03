using System.Collections.Generic;
using System.Threading.Tasks;

namespace BookingsApi.Tools
{
    public interface ITool
    {
        string Name { get; }
        string Description { get; }
        Dictionary<string, object> Parameters { get; }
        Task<string> ExecuteAsync(Dictionary<string, object> parameters);
    }
}