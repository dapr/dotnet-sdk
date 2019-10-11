// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
// ------------------------------------------------------------

namespace Dapr
{
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;

    public class StateTestClient : StateClient
    {
        public Dictionary<string, object> State { get; } = new Dictionary<string, object>();

        public override ValueTask<TValue> GetStateAsync<TValue>(string key, CancellationToken cancellationToken = default)
        {
            if (this.State.TryGetValue(key, out var obj))
            {
                return new ValueTask<TValue>((TValue)obj);
            }
            else
            {
                return new ValueTask<TValue>(default(TValue));
            }
        }

        public override ValueTask SaveStateAsync<TValue>(string key, TValue value, CancellationToken cancellationToken = default)
        {
            if (value == null)
            {
                this.State.Remove(key);
            }
            else
            {
                this.State[key] = value;
            }

            return new ValueTask(Task.CompletedTask);
        }
    }
}