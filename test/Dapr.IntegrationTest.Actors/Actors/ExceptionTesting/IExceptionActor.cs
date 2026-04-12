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

using System.Threading.Tasks;
using Dapr.Actors;

namespace Dapr.IntegrationTest.Actors.ExceptionTesting;

/// <summary>
/// Actor interface that deliberately throws an exception to exercise error propagation.
/// </summary>
public interface IExceptionActor : IPingActor, IActor
{
    /// <summary>Always throws an <see cref="System.Exception"/> to validate remote exception handling.</summary>
    Task ExceptionExample();
}
