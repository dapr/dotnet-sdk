// ------------------------------------------------------------------------
// Copyright 2026 The Dapr Authors
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//     http://www.apache.org/licenses/LICENSE-2.0
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
// ------------------------------------------------------------------------

namespace Dapr.Actors.Next.Core.Runtime;

internal sealed class ActorTurnExecution(object key, IReadOnlyDictionary<string, string> headers)
{
    private static readonly AsyncLocal<ActorTurnExecution?> CurrentExecution = new();

    public static ActorTurnExecution? Current => CurrentExecution.Value;

    public object Key { get; } = key;

    public IReadOnlyDictionary<string, string> Headers { get; } = headers;

    public int ReentrantDepth { get; set; }

    public static IDisposable Push(ActorTurnExecution execution)
    {
        var prior = CurrentExecution.Value;
        CurrentExecution.Value = execution;
        return new PopWhenDisposed(prior);
    }

    private sealed class PopWhenDisposed(ActorTurnExecution? prior) : IDisposable
    {
        public void Dispose()
        {
            CurrentExecution.Value = prior;
        }
    }
}
