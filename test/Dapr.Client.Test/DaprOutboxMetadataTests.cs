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

namespace Dapr.Client.Test;

using Shouldly;
using Xunit;

public class DaprOutboxMetadataTests
{
    [Fact]
    public void Constants_MatchDaprRuntimeContract()
    {
        // These string values are the wire contract used by the Dapr sidecar's
        // outbox implementation. If they change, the SDK stops interoperating
        // with the runtime. This test locks them.
        DaprOutboxMetadata.Projection.ShouldBe("outbox.projection");
        DaprOutboxMetadata.ProjectionEnabled.ShouldBe("true");
        DaprOutboxMetadata.CloudEventId.ShouldBe("cloudevent.id");
        DaprOutboxMetadata.CloudEventSource.ShouldBe("cloudevent.source");
        DaprOutboxMetadata.CloudEventType.ShouldBe("cloudevent.type");
        DaprOutboxMetadata.CloudEventSubject.ShouldBe("cloudevent.subject");
        DaprOutboxMetadata.CloudEventDataContentType.ShouldBe("cloudevent.datacontenttype");
        DaprOutboxMetadata.CloudEventTraceId.ShouldBe("cloudevent.traceid");
        DaprOutboxMetadata.CloudEventTraceParent.ShouldBe("cloudevent.traceparent");
        DaprOutboxMetadata.CloudEventTraceState.ShouldBe("cloudevent.tracestate");
    }

    [Fact]
    public void CloudEventOverrideKeys_AreDerivedFromSharedPropertyNames()
    {
        // The cloudevent.* override keys must be the shared bare property names prefixed
        // with "cloudevent.", tying the outbox contract to the single source of truth.
        DaprOutboxMetadata.CloudEventId.ShouldBe("cloudevent." + CloudEventPropertyNames.Id);
        DaprOutboxMetadata.CloudEventSource.ShouldBe("cloudevent." + CloudEventPropertyNames.Source);
        DaprOutboxMetadata.CloudEventType.ShouldBe("cloudevent." + CloudEventPropertyNames.Type);
        DaprOutboxMetadata.CloudEventSubject.ShouldBe("cloudevent." + CloudEventPropertyNames.Subject);
        DaprOutboxMetadata.CloudEventDataContentType.ShouldBe("cloudevent." + CloudEventPropertyNames.DataContentType);
        DaprOutboxMetadata.CloudEventTraceId.ShouldBe("cloudevent." + CloudEventPropertyNames.TraceId);
        DaprOutboxMetadata.CloudEventTraceParent.ShouldBe("cloudevent." + CloudEventPropertyNames.TraceParent);
        DaprOutboxMetadata.CloudEventTraceState.ShouldBe("cloudevent." + CloudEventPropertyNames.TraceState);
    }
}
