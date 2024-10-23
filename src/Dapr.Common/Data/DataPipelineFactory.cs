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
/// <see cref="DataPipelineAttribute"/> attribute on that type.
/// </summary>
internal sealed class DataPipelineFactory
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
    /// Creates a pipeline used to serialize a given type.
    /// </summary>
    /// <typeparam name="T">The type to create the pipeline for.</typeparam>
    /// <returns></returns>
    public DaprDataPipeline<T> CreateEncodingPipeline<T>()
    {
        var attribute = typeof(T).GetCustomAttribute<DataPipelineAttribute>();
        if (attribute == null)
        {
            return new DaprDataPipeline<T>(new List<IDaprDataOperation<Type, Type>>());
        }

        var allRegisteredOperations = serviceProvider.GetServices<IDaprDataOperation>().ToList();
        var operations = attribute.DataOperationTypes
            .SelectMany(type => allRegisteredOperations.Where(op => op.GetType() == type))
            .ToList();
        
        return new DaprDataPipeline<T>(operations);
    }

    /// <summary>
    /// Creates a pipeline used to reverse a previously applied pipeline operation using the provided
    /// operation names from the metadata.
    /// </summary>
    /// <param name="metadata">The metadata payload used to determine the order of operations.</param>
    /// <typeparam name="T">The type to deserialize to.</typeparam>
    /// <returns>A pipeline configured for reverse processing.</returns>
    /// <exception cref="DaprException"></exception>
    public DaprDataPipeline<T> CreateDecodingPipeline<T>(Dictionary<string,string> metadata)
    {
        const string operationKey = "ops";
        if (!metadata.TryGetValue(operationKey, out var opNames))
        {
            throw new DaprException(
                $"Unable to decode payload as its metadata is missing the key (\"${operationKey}\") containing the operation order");
        }

        //Run through the names backwards in the order of the operations as named in the metadata
        var operations = new List<string>(opNames.Split(',').Reverse());

        var matchingDataOperations = serviceProvider.GetServices<IDaprDataOperation>()
            .Where(op => operations.Contains(op.Name))
            .ToList();

        if (matchingDataOperations.Count != operations.Count)
        {
            //Identify which names are missing
            foreach (var op in matchingDataOperations)
            {
                operations.Remove(op.Name);
            }

            throw new DaprException(
                $"Registered services were not located for the following operation names present in the metadata: {String.Join(',', operations)}");
        }

        return new DaprDataPipeline<T>(matchingDataOperations);
    }
}
