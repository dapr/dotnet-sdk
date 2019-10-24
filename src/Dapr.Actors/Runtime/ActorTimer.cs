// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
// ------------------------------------------------------------

namespace Dapr.Actors.Runtime
{
    using System;
    using System.IO;
    using System.Threading.Tasks;
    using Newtonsoft.Json;

    internal class ActorTimer : IActorTimer
    {
        private readonly Actor owner;

        public ActorTimer(
            Actor owner,
            string timerName,
            Func<object, Task> asyncCallback,
            object state,
            TimeSpan dueTime,
            TimeSpan period)
        {
            this.owner = owner;
            this.Name = timerName;
            this.AsyncCallback = asyncCallback;
            this.State = state;
            this.Period = period;
            this.DueTime = dueTime;
        }

        public string Name { get; }

        public TimeSpan DueTime { get; }

        public TimeSpan Period { get; }

        public object State { get; }

        public Func<object, Task> AsyncCallback { get; }

        internal string SerializeToJson()
        {
            string content;
            using (var sw = new StringWriter())
            {
                using var writer = new JsonTextWriter(sw);
                writer.WriteStartObject();

                writer.WriteProperty((TimeSpan?)this.DueTime, "dueTime", JsonWriterExtensions.WriteTimeSpanValueDaprFormat);
                writer.WriteProperty((TimeSpan?)this.Period, "period", JsonWriterExtensions.WriteTimeSpanValueDaprFormat);

                // Do not serialize state and call back, it will be kept with actor instance.
                writer.WriteEndObject();
                content = sw.ToString();
            }

            return content;
        }
    }
}
