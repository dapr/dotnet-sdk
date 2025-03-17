// ------------------------------------------------------------------------
//  Copyright 2025 The Dapr Authors
//  Licensed under the Apache License, Version 2.0 (the "License");
//  you may not use this file except in compliance with the License.
//  You may obtain a copy of the License at
//      http://www.apache.org/licenses/LICENSE-2.0
//  Unless required by applicable law or agreed to in writing, software
//  distributed under the License is distributed on an "AS IS" BASIS,
//  WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//  See the License for the specific language governing permissions and
//  limitations under the License.
//  ------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace Dapr.Actors.Extensions;

internal static class StringExtensions
{
    /// <summary>
    /// Extension method that validates a string against a list of possible matches.
    /// </summary>
    /// <param name="value">The string value to evaluate.</param>
    /// <param name="possibleValues">The possible values to look for a match within.</param>
    /// <param name="comparisonType">The type of string comparison to perform.</param>
    /// <returns>True if the value ends with any of the possible values; otherwise false.</returns>
    public static bool EndsWithAny(this string value, IReadOnlyList<string> possibleValues, StringComparison comparisonType )
     => possibleValues.Any(val => value.EndsWith(val, comparisonType));
}
