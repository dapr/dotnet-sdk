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
/// Definitions of expected types to be returned from the Dapr runtime.
/// </summary>
internal static class DaprExtendedErrorConstants
{
    public const string ErrorDetailTypeUrl = "type.googleapis.com/";
    public const string GrpcDetails = "grpc-status-details-bin";
    public const string ErrorInfo = $"{ErrorDetailTypeUrl}Google.rpc.ErrorInfo";
    public const string RetryInfo = $"{ErrorDetailTypeUrl}Google.rpc.RetryInfo";
    public const string DebugInfo = $"{ErrorDetailTypeUrl}Google.rpc.DebugInfo";
    public const string QuotaFailure = $"{ErrorDetailTypeUrl}Google.rpc.QuotaFailure";
    public const string PreconditionFailure = $"{ErrorDetailTypeUrl}Google.rpc.PreconditionFailure";
    public const string BadRequest = $"{ErrorDetailTypeUrl}Google.rpc.BadRequest";
    public const string RequestInfo = $"{ErrorDetailTypeUrl}Google.rpc.RequestInfo";
    public const string ResourceInfo = $"{ErrorDetailTypeUrl}Google.rpc.ResourceInfo";
    public const string Help = $"{ErrorDetailTypeUrl}Google.rpc.Help";
    public const string LocalizedMessage = $"{ErrorDetailTypeUrl}Google.rpc.LocalizedMessage";
}