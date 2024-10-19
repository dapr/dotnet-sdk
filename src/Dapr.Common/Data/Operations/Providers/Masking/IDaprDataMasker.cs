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

using System.Text.RegularExpressions;

namespace Dapr.Common.Data.Operations.Providers.Masking;

/// <summary>
/// Identifies an operation that provides a data masking capability.
/// </summary>
public interface IDaprDataMasker : IDaprDataOperation<string, string>
{
    /// <summary>
    /// Registers a pattern to match against.
    /// </summary>
    /// <param name="pattern">The regular expression to match to.</param>
    /// <param name="replacement">The string to place the matching value with.</param>
    void RegisterMatch(Regex pattern, string replacement);
}
