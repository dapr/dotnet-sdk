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

using System.Text;
using Dapr.DurableTask.Protobuf;
using Google.Protobuf;

namespace Dapr.Workflow.Benchmarks;

/// <summary>One delivered orchestration work item, including any separately fetched history.</summary>
internal sealed class TurnMeasurement
{
    public int Number { get; init; }
    public bool CachedHistory { get; init; }
    public long ReceivedProtobufBytes { get; set; }
    public long DeliveredInputUtf8Bytes { get; set; }
    public int DocumentDeserializations { get; set; }
    public long MaterializedDocumentUtf8Bytes { get; set; }
    public long BinaryDownloadBytes { get; set; }
}

internal sealed class Measurements
{
    private readonly object gate = new();
    private TurnMeasurement? currentTurn;

    public long WorkflowSentProtobufBytes { get; private set; }
    public long WorkflowReceivedProtobufBytes { get; private set; }
    public long BinaryUploadBytes { get; private set; }
    public long BinaryDownloadBytes { get; private set; }
    public int BinaryWrites { get; private set; }
    public int BinaryReads { get; private set; }
    public int DocumentDeserializations { get; private set; }
    public long MaterializedDocumentUtf8Bytes { get; private set; }
    public int ActivityExecutions { get; private set; }
    public List<TurnMeasurement> Turns { get; } = [];

    internal void ObserveSent(object message)
    {
        lock (gate)
        {
            if (message is CreateInstanceRequest or WorkflowResponse or ActivityResponse or GetInstanceHistoryRequest)
                WorkflowSentProtobufBytes += ((IMessage)message).CalculateSize();
            if (message is WorkflowResponse)
                currentTurn = null;
        }
    }

    internal void ObserveReceived(object message)
    {
        lock (gate)
        {
            // Health pings and metadata queries are not execution traffic.
            if (message is WorkItem { WorkflowRequest: not null } work)
            {
                var request = work.WorkflowRequest;
                currentTurn = new TurnMeasurement
                {
                    Number = Turns.Count + 1,
                    CachedHistory = request.CachedHistory is not null,
                    ReceivedProtobufBytes = work.CalculateSize(),
                    DeliveredInputUtf8Bytes = InputBytes(request.PastEvents.Concat(request.NewEvents))
                };
                Turns.Add(currentTurn);
                WorkflowReceivedProtobufBytes += work.CalculateSize();
            }
            else if (message is WorkItem { ActivityRequest: not null } activity)
            {
                currentTurn = null;
                ActivityExecutions++;
                WorkflowReceivedProtobufBytes += activity.CalculateSize();
            }
            else if (message is GetInstanceHistoryResponse history)
            {
                WorkflowReceivedProtobufBytes += history.CalculateSize();
                if (currentTurn is not null)
                {
                    currentTurn.ReceivedProtobufBytes += history.CalculateSize();
                    currentTurn.DeliveredInputUtf8Bytes += InputBytes(history.Events);
                }
            }
            else if (message is CreateInstanceResponse or CompleteTaskResponse)
            {
                WorkflowReceivedProtobufBytes += ((IMessage)message).CalculateSize();
            }
        }
    }

    internal void DocumentRead(long bytes)
    {
        lock (gate)
        {
            DocumentDeserializations++;
            MaterializedDocumentUtf8Bytes += bytes;
            if (currentTurn is not null)
            {
                currentTurn.DocumentDeserializations++;
                currentTurn.MaterializedDocumentUtf8Bytes += bytes;
            }
        }
    }

    internal void BinaryReadStarted() { lock (gate) BinaryReads++; }
    internal void BinaryWriteStarted() { lock (gate) BinaryWrites++; }
    internal void Uploaded(int count) { lock (gate) BinaryUploadBytes += count; }
    internal void Downloaded(int count)
    {
        lock (gate)
        {
            BinaryDownloadBytes += count;
            if (currentTurn is not null)
                currentTurn.BinaryDownloadBytes += count;
        }
    }

    private static long InputBytes(IEnumerable<HistoryEvent> events)
        => events.Sum(e => (long)Encoding.UTF8.GetByteCount(e.ExecutionStarted?.Input ?? ""));
}

/// <summary>
/// Only one workflow runs at a time. A measurement window excludes connection warmup,
/// result inspection, and cleanup; completing the workflow closes the window before those operations.
/// </summary>
internal sealed class MeasurementWindow
{
    private Measurements? active;
    internal Measurements? Active => Volatile.Read(ref active);
    internal void Start(Measurements measurements) => Volatile.Write(ref active, measurements);
    internal void Stop() => Volatile.Write(ref active, null);
}
