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

using Dapr.Common.Data.Operations;

namespace Dapr.Common.Data.Attributes;

/// <summary>
/// Attribute-based approach for indicating which data operations should be performed on a type and in what order.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = false, Inherited = false)]
public sealed class DataOperationAttribute : Attribute
{
    /// <summary>
    /// Contains the various data operation types available and the order in which to apply them.
    /// </summary>
    public readonly IReadOnlyList<Type> DataOperationTypes;
    
    /// <summary>
    /// Initializes a new <see cref="DataOperationAttribute"/>.
    /// </summary>
    /// <param name="dataOperationTypes"></param>
    /// <exception cref="DaprException"></exception>
    public DataOperationAttribute(params Type[] dataOperationTypes)
    {
        var registeredTypes = new List<Type>();
        
        foreach (var type in dataOperationTypes)
        {
            if (!typeof(IDaprDataOperation).IsAssignableFrom(type))
                throw new DaprException($"Unable to register data preparation operation as {nameof(type)} does not implement `IDataOperation`");

            registeredTypes.Add(type);
        }

        DataOperationTypes  = registeredTypes;
    }
}
