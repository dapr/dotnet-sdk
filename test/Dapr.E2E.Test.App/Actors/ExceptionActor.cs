using Dapr.Actors.Runtime;
using System.Threading.Tasks;

namespace Dapr.E2E.Test.Actors.ExceptionTesting
{
    public class ExceptionActor : Actor, IExceptionActor
    {
        public ExceptionActor(ActorHost host) 
            : base(host)
        {
        }

        public Task Ping()
        {
            return Task.CompletedTask;
        }

        public Task ExceptionExample()
        {
            throw new System.Exception();
        }
    }
}