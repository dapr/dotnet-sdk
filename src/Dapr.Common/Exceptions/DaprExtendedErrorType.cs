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

namespace Dapr.Common.Exceptions;

/// <summary>
/// Extended error detail types.
/// This is based on the Richer Error Model (see <see href="https://google.aip.dev/193#error_model"/> and
/// <see href="https://github.com/googleapis/googleapis/blob/master/google/rpc/error_details.proto"/>)
/// and is implemented by the Dapr runtime (see <see href="https://github.com/dapr/dapr/blob/master/pkg/api/errors/README.md"/>).
/// </summary>
public enum DaprExtendedErrorType
{
    /// <summary>
    /// Unknown extended error type.
    /// Implemented by <see cref="DaprUnknownDetail"/>.
    /// </summary>
    Unknown,

    /// <summary>
    /// Retry info detail type.
    /// See <see cref="DaprRetryInfoDetail"/>.
    /// </summary>
    RetryInfo,

    /// <summary>
    /// Debug info detail type.
    /// See <see cref="DaprDebugInfoDetail"/>.
    /// </summary>
    DebugInfo,

    /// <summary>
    /// Quote failure detail type.
    /// See <see cref="DaprQuotaFailureDetail"/>.
    /// </summary>
    QuotaFailure,

    /// <summary>
    /// Precondition failure detail type.
    /// See <see cref="DaprPreconditionFailureDetail"/>.
    /// </summary>
    PreconditionFailure,

    /// <summary>
    /// Request info detail type.
    /// See <see cref="DaprRequestInfoDetail"/>.
    /// </summary>
    RequestInfo,

    /// <summary>
    /// Localized message detail type.
    /// See <see cref="DaprLocalizedMessageDetail"/>.
    /// </summary>
    LocalizedMessage,

    /// <summary>
    /// Bad request detail type.
    /// See <see cref="DaprBadRequestDetail"/>.
    /// </summary>
    BadRequest,

    /// <summary>
    /// Error info detail type.
    /// See <see cref="DaprErrorInfoDetail"/>.
    /// </summary>
    ErrorInfo,

    /// <summary>
    /// Help detail type.
    /// See <see cref="DaprHelpDetail"/>.
    /// </summary>
    Help,

    /// <summary>
    /// Resource info detail type.
    /// See <see cref="DaprResourceInfoDetail"/>.
    /// </summary>
    ResourceInfo
}