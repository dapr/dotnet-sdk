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
using System.Text.RegularExpressions;
using Dapr.Actors.Extensions;

namespace Dapr.Actors.Runtime;


internal sealed class TimeConverterUtils
{
    /// <summary>
    /// A regular expression used to evaluate whether a given prefix period embodies an @every statement
    /// </summary>
    private static readonly Regex isEveryExpression = new(@"@every (\d+(m?s|m|h))+$");
    /// <summary>
    /// The various prefixed period values allowed.
    /// </summary>
    private static readonly string[] acceptablePeriodValues = { "yearly", "monthly", "weekly", "daily", "midnight", "hourly" };

    private string ExpressionValue { get; }
    
    public TimeConverterUtils(string expressionValue)
    {
        this.ExpressionValue = expressionValue;
    }
    
    /// <summary>
    /// Reflects that the schedule represents a prefixed period expression.
    /// </summary>
    public bool IsPrefixedPeriodExpression =>
    this.ExpressionValue.StartsWith('@') &&
    (isEveryExpression.IsMatch(ExpressionValue) || 
     ExpressionValue.EndsWithAny(acceptablePeriodValues, StringComparison.InvariantCulture));
        
    /// <summary>
    /// Reflects that the schedule represents a fixed point in time.
    /// </summary>
    public bool IsPointInTimeExpression => DateTimeOffset.TryParse(ExpressionValue, out _);

    /// <summary>
    /// Reflects that the schedule represents a Golang duration expression.
    /// </summary>
    public bool IsDurationExpression => ExpressionValue.IsDurationString();
    
    
}
