// ------------------------------------------------------------------------
// Copyright 2025 The Dapr Authors
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

using System;
using System.Threading.Tasks;
using Dapr.Actors.Runtime;

namespace Dapr.IntegrationTest.Actors.ExceptionTesting;

/// <summary>
/// Implementation of <see cref="IExceptionActor"/> that unconditionally throws to
/// validate that the Dapr runtime correctly propagates remote exceptions to callers.
/// </summary>
public class ExceptionActor : Actor, IExceptionActor
{
    /// <summary>
    /// Initializes a new instance of <see cref="ExceptionActor"/>.
    /// </summary>
    /// <param name="host">The actor host provided by the Dapr runtime.</param>
    public ExceptionActor(ActorHost host) : base(host)
    {
    }

    /// <inheritdoc />
    public Task Ping() => Task.CompletedTask;

    /// <inheritdoc />
    public Task ExceptionExample() =>
        throw new InvalidOperationException("This exception is intentional.");
}
