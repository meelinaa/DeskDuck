using System.Threading;
using System.Threading.Tasks;

namespace DeskDuck.Services
{
    public interface IMessagePublisherService
    {
        Task RunAsync(CancellationToken ct);
    }
}
