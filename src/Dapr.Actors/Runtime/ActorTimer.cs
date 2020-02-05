// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
// ------------------------------------------------------------

namespace Dapr.Actors.Runtime
{
    using System;
    using System.IO;
    using System.Text;
    using System.Text.Json;
    using System.Threading.Tasks;

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

        internal async Task<string> SerializeAsync()
        {
            using var stream = new MemoryStream();
            using Utf8JsonWriter writer = new Utf8JsonWriter(stream);

            writer.WriteStartObject();

            if (this.DueTime != null)
            {
                writer.WriteString("dueTime", ConverterUtils.ConvertTimeSpanValueInDaprFormat(this.DueTime));
            }

            if (this.Period != null && this.Period >= TimeSpan.Zero)
            {
                writer.WriteString("period", ConverterUtils.ConvertTimeSpanValueInDaprFormat(this.Period));
            }

            writer.WriteEndObject();
            await writer.FlushAsync();
            return Encoding.UTF8.GetString(stream.ToArray());
        }
    }
}
