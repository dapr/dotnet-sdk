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

using System.Diagnostics.CodeAnalysis;
using Grpc.Core;

namespace Dapr.Common.Exceptions;

/// <summary>
/// Provides extension methods for <see cref="DaprException"/>.
/// </summary>
public static class DaprExceptionExtensions
{
    /// <summary>
    /// Attempt to retrieve <see cref="DaprExtendedErrorInfo"/> from <see cref="DaprException"/>.
    /// </summary>
    /// <param name="exception">A Dapr exception. <see cref="DaprException"/>.</param>
    /// <param name="daprExtendedErrorInfo"><see cref="DaprExtendedErrorInfo"/> out if parsable from inner exception, null otherwise.</param>
    /// <returns>True if extended info is available, false otherwise.</returns>
    public static bool TryGetExtendedErrorInfo(this DaprException exception, [NotNullWhen(true)] out DaprExtendedErrorInfo? daprExtendedErrorInfo)
    {
        daprExtendedErrorInfo = null;
        if (exception.InnerException is not RpcException rpcException)
        {
            return false;
        }

        var metadata = rpcException.Trailers.Get(DaprExtendedErrorConstants.GrpcDetails);

        if (metadata is null)
        {
            return false;
        }

        var status = Google.Rpc.Status.Parser.ParseFrom(metadata.ValueBytes);

        daprExtendedErrorInfo = new DaprExtendedErrorInfo(status.Code, status.Message)
        {
            Details = status.Details.Select(detail => ExtendedErrorDetailFactory.CreateErrorDetail(detail)).ToArray(),
        };

        return true;
    }
}