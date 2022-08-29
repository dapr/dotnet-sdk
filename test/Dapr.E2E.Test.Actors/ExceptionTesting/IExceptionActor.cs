using Dapr.Actors;
using System.Threading.Tasks;

namespace Dapr.E2E.Test.Actors.ExceptionTesting
{
    public interface IExceptionActor : IPingActor, IActor
    {
        Task ExceptionExample();
    }
}