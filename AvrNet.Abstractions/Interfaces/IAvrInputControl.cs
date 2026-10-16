using System.Threading;
using System.Threading.Tasks;

namespace AvrNet.Abstractions.Interfaces
{
    public interface IAvrInputControl
    {
        Task<string> GetInputIdAsync(CancellationToken cancellationToken = default(CancellationToken));
        Task SetInputIdAsync(string inputId, CancellationToken cancellationToken = default(CancellationToken));
    }
}
