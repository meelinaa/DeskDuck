using System.Threading;
using System.Threading.Tasks;

namespace DeskDuck
{
    public interface IMessagePublisherService
    {
        Task RunAsync(CancellationToken ct);
    }
}
