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

using System.ComponentModel;

namespace Dapr.Messaging;

/// <summary>
/// Identifies the optional hosting features required by the <c>[DaprTopic]</c> subscriptions discovered
/// in a consuming assembly. Computed by the Dapr messaging source generator and passed to
/// <c>DaprMessagingRegistration.Register</c>.
/// </summary>
/// <remarks>
/// This type is public only so source-generated code can reference it; it is not intended to be used
/// directly from application code.
/// </remarks>
[Flags]
[EditorBrowsable(EditorBrowsableState.Never)]
public enum DaprMessagingFeatures
{
    /// <summary>
    /// No optional hosting features are required. Applies when an assembly declares no subscriptions, or
    /// only <see cref="DeliveryMode.Streaming"/> subscriptions, which need neither gRPC server hosting
    /// nor HTTP routing.
    /// </summary>
    None = 0,

    /// <summary>
    /// At least one <see cref="DeliveryMode.Programmatic"/> subscription exists, requiring ASP.NET Core
    /// gRPC server hosting and the Dapr <c>AppCallback</c> push service.
    /// </summary>
    ProgrammaticSubscriptions = 1,

    /// <summary>
    /// At least one <see cref="DeliveryMode.Http"/> subscription exists, requiring ASP.NET Core routing
    /// for the <c>/dapr/subscribe</c> discovery endpoint and the delivery routes.
    /// </summary>
    HttpSubscriptions = 2,
}
