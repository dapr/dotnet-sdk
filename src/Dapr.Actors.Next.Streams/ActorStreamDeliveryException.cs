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

namespace Dapr.Actors.Next.Streams;

/// <summary>
/// Base exception for explicit stream delivery classification.
/// </summary>
public abstract class ActorStreamDeliveryException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ActorStreamDeliveryException"/> class.
    /// </summary>
    protected ActorStreamDeliveryException(string message)
        : base(message)
    {
    }
}

/// <summary>
/// Exception that marks a stream delivery failure as transient.
/// </summary>
public sealed class ActorStreamTransientException(string message) : ActorStreamDeliveryException(message);

/// <summary>
/// Exception that marks a stream delivery failure as poison.
/// </summary>
public sealed class ActorStreamPoisonException(string message) : ActorStreamDeliveryException(message);
