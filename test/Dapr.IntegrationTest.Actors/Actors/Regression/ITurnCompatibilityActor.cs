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

namespace Dapr.IntegrationTest.Actors.Regression;

public interface ITurnCompatibilityActor : IPingActor
{
    Task Reenter(ReentrantTurnRequest request, CancellationToken cancellationToken = default);
    Task<int> Increment(CancellationToken cancellationToken = default);
    Task<TurnStateSnapshot> Read(bool reload, CancellationToken cancellationToken = default);
    Task FailAfterChangingState(CancellationToken cancellationToken = default);
    Task StartSchedule(TurnScheduleRequest request, CancellationToken cancellationToken = default);
    Task StopSchedule(bool reminder, CancellationToken cancellationToken = default);
}

public sealed class ReentrantTurnRequest
{
    public string[] ActorIds { get; set; } = [];
    public int Index { get; set; }
    public bool UseRemoting { get; set; }
}

public sealed class TurnScheduleRequest
{
    public bool Reminder { get; set; }
    public string CallbackActorId { get; set; } = "";
    public byte[] Payload { get; set; } = [];
}

public sealed class TurnStateSnapshot
{
    public Guid InstanceId { get; set; }
    public int Value { get; set; }
    public bool SameWithinTurn { get; set; }
    public bool SameAsPreviousRead { get; set; }
    public bool TransientExists { get; set; }
    public bool SentinelExists { get; set; }
    public int CallbackCount { get; set; }
}
