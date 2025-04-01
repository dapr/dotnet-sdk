// ------------------------------------------------------------------------
// Copyright 2024 The Dapr Authors
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

namespace Dapr.Common.Exceptions;

/// <summary>
/// Dapr implementation of the richer error model.
/// </summary>
/// <param name="Code">A status code.</param>
/// <param name="Message">A message.</param>
public sealed record DaprExtendedErrorInfo(int Code, string Message)
{
    /// <summary>
    /// A collection of details that provide more information on the error.
    /// </summary>
    public DaprExtendedErrorDetail[] Details { get; init; } = Array.Empty<DaprExtendedErrorDetail>();
}