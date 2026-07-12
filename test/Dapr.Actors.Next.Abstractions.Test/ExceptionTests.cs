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

using Dapr.Actors.Next.Abstractions.Exceptions;

namespace Dapr.Actors.Next.Abstractions.Test;

public sealed class ExceptionTests
{
    [MinimumDaprRuntimeFact("1.18")]
    public void BaseException_ConstructorsSetExpectedProperties()
    {
        var inner = new InvalidOperationException("inner");

        Assert.IsType<DaprActorException>(new DaprActorException());
        Assert.Equal("message", new DaprActorException("message").Message);
        Assert.Same(inner, new DaprActorException("message", inner).InnerException);
    }

    [MinimumDaprRuntimeFact("1.18")]
    public void DerivedException_ConstructorsSetExpectedProperties()
    {
        var inner = new InvalidOperationException("inner");

        AssertDerived(new ActorActivationException(), new ActorActivationException("message"), new ActorActivationException("message", inner), inner);
        AssertDerived(new ActorInvocationException(), new ActorInvocationException("message"), new ActorInvocationException("message", inner), inner);
        AssertDerived(new ActorStateException(), new ActorStateException("message"), new ActorStateException("message", inner), inner);
        AssertDerived(new ActorReminderAlreadyExistsException(), new ActorReminderAlreadyExistsException("message"), new ActorReminderAlreadyExistsException("message", inner), inner);
        AssertDerived(new ActorStateMigrationException(), new ActorStateMigrationException("message"), new ActorStateMigrationException("message", inner), inner);
        AssertDerived(new ActorStateEnvelopeException(), new ActorStateEnvelopeException("message"), new ActorStateEnvelopeException("message", inner), inner);
        AssertDerived(new ActorStateCacheDirtyException(), new ActorStateCacheDirtyException("message"), new ActorStateCacheDirtyException("message", inner), inner);
        AssertDerived(new StateMachineDefinitionException(), new StateMachineDefinitionException("message"), new StateMachineDefinitionException("message", inner), inner);
        AssertDerived(new InvalidActorEventException(), new InvalidActorEventException("message"), new InvalidActorEventException("message", inner), inner);
    }

    [MinimumDaprRuntimeFact("1.18")]
    public void DomainException_ConstructorsCaptureContext()
    {
        var reminder = new ActorReminderAlreadyExistsException("Counter", "one", "wake");
        var migration = new ActorStateMigrationException("migration", "Family", 2, typeof(TestEvent), "shape");
        var envelope = new ActorStateEnvelopeException("envelope", "state", 1, "Plain", "stored", 2, "current", 3);
        var dirty = new ActorStateCacheDirtyException("state", "dirty");
        var definition = new StateMachineDefinitionException("definition", typeof(TestEvent), "Open");

        Assert.Equal("Counter", reminder.ActorType);
        Assert.Equal("one", reminder.ActorId);
        Assert.Equal("wake", reminder.ReminderName);
        Assert.Equal("Family", migration.FamilyName);
        Assert.Equal(2, migration.ChainIndex);
        Assert.Equal(typeof(TestEvent), migration.TargetType);
        Assert.Equal("shape", migration.ShapeHash);
        Assert.Equal("state", envelope.StateName);
        Assert.Equal(1, envelope.FormatVersion);
        Assert.Equal("Plain", envelope.FormKind);
        Assert.Equal("stored", envelope.StoredSerializerId);
        Assert.Equal(2, envelope.StoredSerializerVersion);
        Assert.Equal("current", envelope.CurrentSerializerId);
        Assert.Equal(3, envelope.CurrentSerializerVersion);
        Assert.Equal("state", dirty.StateName);
        Assert.Equal(typeof(TestEvent), definition.ActorType);
        Assert.Equal("Open", definition.StateName);
    }

    [MinimumDaprRuntimeFact("1.18")]
    public void InvalidActorEventException_StateEventConstructorCapturesNames()
    {
        var ex = new InvalidActorEventException(TestState.Open, new TestEvent());

        Assert.Contains("Open", ex.Message);
        Assert.Contains(nameof(TestEvent), ex.Message);
        Assert.Equal("Open", ex.StateName);
        Assert.Equal(typeof(TestEvent).FullName, ex.EventName);
    }

    [MinimumDaprRuntimeFact("1.18")]
    public void InvalidActorEventException_StateEventConstructorAllowsNulls()
    {
        var ex = new InvalidActorEventException((object?)null, (object?)null);

        Assert.Contains("''", ex.Message);
        Assert.Null(ex.StateName);
        Assert.Null(ex.EventName);
    }

    private static void AssertDerived(DaprActorException empty, DaprActorException withMessage, DaprActorException withInner, Exception inner)
    {
        Assert.NotNull(empty);
        Assert.Equal("message", withMessage.Message);
        Assert.Same(inner, withInner.InnerException);
    }

    private enum TestState
    {
        Open,
    }

    private sealed class TestEvent
    {
    }
}
