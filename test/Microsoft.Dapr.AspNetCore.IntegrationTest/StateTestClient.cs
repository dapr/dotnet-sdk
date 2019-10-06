// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Microsoft.Dapr
{
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;

    public class StateTestClient : StateClient
    {
        public Dictionary<string, object> State { get; } = new Dictionary<string, object>();

        public override ValueTask<TValue> GetStateAsync<TValue>(string key, CancellationToken cancellationToken = default)
        {
            if (State.TryGetValue(key, out var obj))
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
                State.Remove(key);
            }
            else
            {
                State[key] = value;
            }

            return new ValueTask(Task.CompletedTask);
        }
    }
}