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

using System.Threading.Tasks;
using Dapr.Actors;

namespace Dapr.IntegrationTest.Actors.WeaklyTyped;

/// <summary>
/// Base response type used to test polymorphic actor responses.
/// </summary>
public class ResponseBase
{
    /// <summary>
    /// Gets or sets a property defined on the base response type.
    /// </summary>
    public string? BaseProperty { get; set; }
}

/// <summary>
/// Derived response type used to exercise polymorphic deserialization.
/// </summary>
public class DerivedResponse : ResponseBase
{
    /// <summary>
    /// Gets or sets a property defined only on the derived type.
    /// </summary>
    public string? DerivedProperty { get; set; }
}

/// <summary>
/// Actor interface that returns polymorphic response objects via weakly-typed invocation.
/// </summary>
public interface IWeaklyTypedTestingActor : IPingActor, IActor
{
    /// <summary>
    /// Returns a <see cref="DerivedResponse"/> instance to test polymorphic deserialization.
    /// </summary>
    Task<ResponseBase> GetPolymorphicResponse();

    /// <summary>
    /// Returns <see langword="null"/> to verify that null responses are handled correctly.
    /// </summary>
    Task<ResponseBase?> GetNullResponse();
}
