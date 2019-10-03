// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Microsoft.Dapr.Actors.Runtime
{    
    using System;
    using System.IO;
    using System.Reflection;
    using System.Threading;
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
                if (this.DueTime != null)
                {
                    writer.WriteProperty((TimeSpan?)this.DueTime, "dueTime", JsonWriterExtensions.WriteTimeSpanValueDaprFormat);
                }

                if (this.Period != null)
                {
                    writer.WriteProperty((TimeSpan?)this.Period, "period", JsonWriterExtensions.WriteTimeSpanValueDaprFormat);
                }

                // Do not serialize state and call back, it will be kept with actor instance.
                writer.WriteEndObject();
                content = sw.ToString();
            }

            return content;
        }
    }
}
