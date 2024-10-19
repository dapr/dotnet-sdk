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

using System.Reflection;
using Dapr.Common.Data.Attributes;
using Dapr.Common.Data.Operations;
using Microsoft.Extensions.DependencyInjection;

namespace Dapr.Common.Data;

/// <summary>
/// Used to create a data pipeline specific to a given type using the ordered operation services indicated in the
/// <see cref="DataOperationAttribute"/> attribute on that type.
/// </summary>
public sealed class DataPipelineFactory
{
    /// <summary>
    /// The service provider used to pull the registered data operation services.
    /// </summary>
    private readonly IServiceProvider serviceProvider;

    /// <summary>
    /// Used to instantiate a <see cref="DataPipelineFactory"/>.
    /// </summary>
    public DataPipelineFactory(IServiceProvider serviceProvider)
    {
        this.serviceProvider = serviceProvider;
    }

    /// <summary>
    /// Creates a pipeline with the attributes specified for a given type.
    /// </summary>
    /// <typeparam name="T">The type to create the pipeline for.</typeparam>
    /// <returns></returns>
    public DataPipeline CreatePipeline<T>()
    {
        var attribute = typeof(T).GetCustomAttribute<DataOperationAttribute>();
        if (attribute == null)
        {
            return new DataPipeline(new List<IDaprDataOperation<Type, Type>>());
        }

        var operations = attribute.DataOperationTypes
            .Select(type => serviceProvider.GetRequiredService(type))
            .ToList();

        return new DataPipeline(operations);
    }
}
