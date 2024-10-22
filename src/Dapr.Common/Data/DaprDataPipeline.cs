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

using System.Text;
using Dapr.Common.Data.Extensions;
using Dapr.Common.Data.Operations;

namespace Dapr.Common.Data;

/// <summary>
/// Processes the data using the provided <see cref="IDaprDataOperation{TInput,TOutput}"/> providers.
/// </summary>
internal sealed class DaprDataPipeline<TInput>
{
    /// <summary>
    /// The metadata key containing the operations.
    /// </summary>
    private const string OperationKey = "ops";
    
    private readonly StringBuilder operationNameBuilder = new();
    private readonly IDaprTStringTransitionOperation<TInput>? genericToStringOp;
    private readonly List<IDaprStringBasedOperation> stringOps = new();
    private readonly IDaprStringByteTransitionOperation? stringToByteOp;
    private readonly List<IDaprByteBasedOperation> byteOps = new();
    
    /// <summary>
    /// Used to initialize a new <see cref="DaprDataPipeline{TInput}"/>.
    /// </summary>
    public DaprDataPipeline(IEnumerable<IDaprDataOperation> operations)
    {
        foreach (var op in operations)
        {
            switch (op)
            {
                case IDaprTStringTransitionOperation<TInput> genToStrOp when genericToStringOp is not null:
                    throw new DaprException(
                        $"Multiple types are declared for the conversion of the data to a string in the data pipeline for {typeof(TInput)}  - only one is allowed");
                case IDaprTStringTransitionOperation<TInput> genToStrOp:
                    genericToStringOp = genToStrOp;
                    break;
                case IDaprStringBasedOperation strOp:
                    stringOps.Add(strOp);
                    break;
                case IDaprStringByteTransitionOperation strToByte when stringToByteOp is not null:
                    throw new DaprException(
                        $"Multiple types are declared for the pipeline conversion from a string to a byte array for {typeof(TInput)} - only one is allowed");
                case IDaprStringByteTransitionOperation strToByte:
                    stringToByteOp = strToByte;
                    break;
                case IDaprByteBasedOperation byteOp:
                    byteOps.Add(byteOp);
                    break;
            }
        }
        
        if (genericToStringOp is null)
        {
            throw new DaprException(
                $"A pipeline operation must be specified to convert a {typeof(TInput)} into a serializable string");
        }

        if (stringToByteOp is null)
        {
            throw new DaprException(
                $"A pipeline operation must be specified to convert a {typeof(TInput)} into a byte array");
        }
    }
    
    /// <summary>
    /// Processes the data in the order of the provided list of <see cref="IDaprDataOperation{TInput,TOutput}"/>.
    /// </summary>
    /// <param name="input">The data to evaluate.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The evaluated data.</returns>
    public async Task<DaprOperationPayload<ReadOnlyMemory<byte>>> ProcessAsync(TInput input, CancellationToken cancellationToken = default)
    {
        //Combines the metadata across each operation to be returned with the result 
        var combinedMetadata = new Dictionary<string, string>();
        
        //Start by serializing the input to a string
        var serializationPayload = await genericToStringOp!.ExecuteAsync(input, cancellationToken);
        combinedMetadata.MergeFrom(serializationPayload.Metadata);
        AppendOperationName(genericToStringOp.Name);
        
        //Run through any provided string-based operations
        var stringPayload = new DaprOperationPayload<string?>(serializationPayload.Payload);
        foreach (var strOp in stringOps)
        {
            stringPayload = await strOp.ExecuteAsync(stringPayload.Payload, cancellationToken);
            combinedMetadata.MergeFrom(stringPayload.Metadata);
            AppendOperationName(strOp.Name);
        }
        
        //Encode the string payload to a byte array
        var encodedPayload = await stringToByteOp!.ExecuteAsync(stringPayload.Payload, cancellationToken);
        combinedMetadata.MergeFrom(encodedPayload.Metadata);
        AppendOperationName(stringToByteOp.Name);
        
        //Run through any provided byte-based operations
        var bytePayload = new DaprOperationPayload<ReadOnlyMemory<byte>>(encodedPayload.Payload);
        foreach (var byteOp in byteOps)
        {
            bytePayload = await byteOp.ExecuteAsync(bytePayload.Payload, cancellationToken);
            combinedMetadata.MergeFrom(bytePayload.Metadata);
            AppendOperationName(byteOp.Name);
        }
        
        //Persist the op names to the metadata
        combinedMetadata[OperationKey] = operationNameBuilder.ToString();
        
        //Create a payload that combines the payload and metadata
        var resultPayload = new DaprOperationPayload<ReadOnlyMemory<byte>>(bytePayload.Payload)
        {
            Metadata = combinedMetadata
        };
        return resultPayload;
    }
    
    /// <summary>
    /// Processes the reverse of the data in the order of the provided list of <see cref="IDaprDataOperation{TInput,TOutput}"/>.
    /// </summary>
    /// <param name="payload">The data to process in reverse.</param>
    /// <param name="metadata">The metadata providing the mechanism(s) used to encode the data.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The evaluated data.</returns>
    public async Task<DaprOperationPayload<TInput?>> ReverseProcessAsync<TOutput>(ReadOnlyMemory<byte> payload, Dictionary<string,string> metadata, CancellationToken cancellationToken = default)
    {
        //First, perform byte-based operations
        var inboundPayload = new DaprOperationPayload<ReadOnlyMemory<byte>>(payload) { Metadata = metadata };
        var byteBasedResult = await ReverseByteOperationsAsync(inboundPayload, cancellationToken);
        
        //Convert this back to a string from a byte array
        var stringResult = await stringToByteOp!.ReverseAsync(byteBasedResult, cancellationToken);
        
        //Perform the string-based operations
        var stringBasedResult = await ReverseStringOperationsAsync(stringResult, cancellationToken);
        
        //Convert from a string back into its generic type
        var genericResult = await genericToStringOp!.ReverseAsync(stringBasedResult, cancellationToken);

        return genericResult;
    }

    /// <summary>
    /// Performs a reversal operation for the string-based operations.
    /// </summary>
    /// <param name="payload">The payload to run the reverse operation against.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns></returns>
    private async Task<DaprOperationPayload<string?>> ReverseStringOperationsAsync(DaprOperationPayload<string?> payload, 
            CancellationToken cancellationToken)
    {
        stringOps.Reverse();
        foreach (var op in stringOps)
        {
            payload = await op.ReverseAsync(payload, cancellationToken);
        }

        return payload;
    }

    /// <summary>
    /// Performs a reversal operation for the byte-based operations.
    /// </summary>
    /// <param name="payload">The current state of the payload.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The most up-to-date payload.</returns>
    private async Task<DaprOperationPayload<ReadOnlyMemory<byte>>>
        ReverseByteOperationsAsync(DaprOperationPayload<ReadOnlyMemory<byte>> payload,
            CancellationToken cancellationToken)
    {
        byteOps.Reverse();
        foreach (var op in byteOps)
        {
            payload = await op.ReverseAsync(payload, cancellationToken);
        }

        return payload;
    }

    /// <summary>
    /// Appends the operation name to the string.
    /// </summary>
    /// <param name="name">The name of the operation to append.</param>
    private void AppendOperationName(string name)
    {
        if (operationNameBuilder.Length > 0)
            operationNameBuilder.Append(',');
        operationNameBuilder.Append(name);
    }
}
