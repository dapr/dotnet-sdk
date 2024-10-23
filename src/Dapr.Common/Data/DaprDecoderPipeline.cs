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

namespace Dapr.Common.Data;

/// <summary>
/// Processes the data using the provided <see cref="IDaprDataOperation{TInput,TOutput}"/> providers.
/// </summary>
internal sealed class DaprDecoderPipeline<TOutput>
{
    private readonly Stack<string> prefixKeys;
    private readonly IDaprTStringTransitionOperation<TOutput>? genericToStringOp;
    private readonly List<IDaprStringBasedOperation> stringOps = new();
    private readonly IDaprStringByteTransitionOperation? stringToByteOp;
    private readonly List<IDaprByteBasedOperation> byteOps = new();

    /// <summary>
    /// Used to initialize a new <see cref="DaprDecoderPipeline{TInput}"/>.
    /// </summary>
    public DaprDecoderPipeline(IEnumerable<IDaprDataOperation> operations, Stack<string> prefixKeys)
    {
        this.prefixKeys = prefixKeys;
        
        foreach (var op in operations)
        {
            switch (op)
            {
                case IDaprTStringTransitionOperation<TOutput> genToStrOp when genericToStringOp is not null:
                    throw new DaprException(
                        $"Multiple types are declared for the conversion of the data to a string in the data pipeline for {typeof(TOutput)}  - only one is allowed");
                case IDaprTStringTransitionOperation<TOutput> genToStrOp:
                    genericToStringOp = genToStrOp;
                    break;
                case IDaprStringBasedOperation strOp:
                    stringOps.Add(strOp);
                    break;
                case IDaprStringByteTransitionOperation strToByte when stringToByteOp is not null:
                    throw new DaprException(
                        $"Multiple types are declared for the pipeline conversion from a string to a byte array for {typeof(TOutput)} - only one is allowed");
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
                $"A pipeline operation must be specified to convert a {typeof(TOutput)} into a serializable string");
        }

        if (stringToByteOp is null)
        {
            throw new DaprException(
                $"A pipeline operation must be specified to convert a {typeof(TOutput)} into a byte array");
        }
    }

    /// <summary>
    /// Processes the reverse of the data in the order of the provided list of <see cref="IDaprDataOperation{TInput,TOutput}"/>.
    /// </summary>
    /// <param name="payload">The data to process in reverse.</param>
    /// <param name="metadata">The metadata providing the mechanism(s) used to encode the data.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The evaluated data.</returns>
    public async Task<DaprOperationPayload<TOutput?>> ReverseProcessAsync(ReadOnlyMemory<byte> payload,
        Dictionary<string, string> metadata, CancellationToken cancellationToken = default)
    {
        var metadataPrefixes = new Stack<string>(prefixKeys);
        
        //First, perform byte-based operations
        var inboundPayload = new DaprOperationPayload<ReadOnlyMemory<byte>>(payload) { Metadata = metadata };
        var byteBasedResult = await ReverseByteOperationsAsync(inboundPayload, metadataPrefixes, cancellationToken);

        //Convert this back to a string from a byte array
        var currentPrefix = metadataPrefixes.Pop();
        var stringResult = await stringToByteOp!.ReverseAsync(byteBasedResult, currentPrefix, cancellationToken);

        //Perform the string-based operations
        var stringBasedResult = await ReverseStringOperationsAsync(stringResult, metadataPrefixes, cancellationToken);

        //Convert from a string back into its generic type
        currentPrefix = metadataPrefixes.Pop();
        var genericResult = await genericToStringOp!.ReverseAsync(stringBasedResult, currentPrefix, cancellationToken);

        return genericResult;
    }

    /// <summary>
    /// Performs a reversal operation for the string-based operations.
    /// </summary>
    /// <param name="payload">The payload to run the reverse operation against.</param>
    /// <param name="metadataPrefixes">The prefix values for retrieving data from the metadata for this operation.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns></returns>
    private async Task<DaprOperationPayload<string?>> ReverseStringOperationsAsync(
        DaprOperationPayload<string?> payload,
        Stack<string> metadataPrefixes, CancellationToken cancellationToken)
    {
        stringOps.Reverse();
        foreach (var op in stringOps)
        {
            var currentPrefix = metadataPrefixes.Pop();
            payload = await op.ReverseAsync(payload, currentPrefix, cancellationToken);
        }

        return payload;
    }

    /// <summary>
    /// Performs a reversal operation for the byte-based operations.
    /// </summary>
    /// <param name="payload">The current state of the payload.</param>
    /// <param name="metadataPrefixes">The prefix values for retrieving data from the metadata for this operation.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The most up-to-date payload.</returns>
    private async Task<DaprOperationPayload<ReadOnlyMemory<byte>>>
        ReverseByteOperationsAsync(DaprOperationPayload<ReadOnlyMemory<byte>> payload, Stack<string> metadataPrefixes,
            CancellationToken cancellationToken)
    {
        byteOps.Reverse();
        foreach (var op in byteOps)
        {
            var currentPrefix = metadataPrefixes.Pop();
            payload = await op.ReverseAsync(payload, currentPrefix, cancellationToken);
        }

        return payload;
    }
}
