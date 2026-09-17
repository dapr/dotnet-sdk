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

using System.Net.Mime;

namespace Dapr.Messaging;

/// <summary>
/// Content-type and wire constants relevant to Dapr pub/sub messaging.
/// </summary>
public static class MessagingConstants
{
    /// <summary>Content type for JSON payloads.</summary>
    public const string ContentTypeApplicationJson = MediaTypeNames.Application.Json;

    /// <summary>Content type for gRPC payloads.</summary>
    public const string ContentTypeApplicationGrpc = "application/grpc";

    /// <summary>Content type for a CloudEvents 1.0 JSON envelope.</summary>
    public const string ContentTypeCloudEvent = "application/cloudevents+json";

    /// <summary>Content type for raw binary payloads.</summary>
    public const string ContentTypeApplicationOctetStream = MediaTypeNames.Application.Octet;
}
