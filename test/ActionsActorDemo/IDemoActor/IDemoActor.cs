using Microsoft.Actions.Actors;
using Newtonsoft.Json.Linq;
using System;
using System.Threading.Tasks;

namespace IDemoActorIneterface
{
    public interface IDemoActor : IActor
    {
        Task<string> ProcessData(MyData data);

        Task<MyData> Echo(MyData data);

        Task<string> ProcessStringData(string data);

        Task<MyData> GetData();

        Task NoReturnTypeNoArg();

        Task ThrowException();

        Task RegisterReminder();

        Task UnregisterReminder();

        Task RegisterTimer();

        Task UnregisterTimer();
    }

    public class MyData
    {
        public string PropertyA { get; set; }
        public string PropertyB { get; set; }

        public override string ToString()
        {
            var propAValue = this.PropertyA == null ? "null" : this.PropertyA;
            var propBValue = this.PropertyB == null ? "null" : this.PropertyB;
            return $"PropertyA: {propAValue}, PropertyB: {propBValue}";
        }
    }
}
