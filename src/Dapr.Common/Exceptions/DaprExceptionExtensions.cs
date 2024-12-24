using System.Diagnostics.CodeAnalysis;
using Grpc.Core;

namespace Dapr.Common.Exceptions
{
    /// <summary>
    /// Provides help extension methods for <see cref="DaprException"/>
    /// </summary>
    public static class DaprExceptionExtensions
    {
        private static string GrpcDetails = "grpc-status-details-bin";

        /// <summary>
        /// Attempt to retrieve <see cref="DaprExtendedErrorInfo"/> from <see cref="DaprException"/>
        /// </summary>
        /// <returns></returns>
        public static bool TryGetExtendedErrorInfo(this DaprException exception, [NotNullWhen(true)] out DaprExtendedErrorInfo? daprExtendedErrorInfo)
        {
            daprExtendedErrorInfo = null;
            if (exception.InnerException is not RpcException rpcException)
            {
                return false;
            }

            var metadata = rpcException.Trailers.Get(GrpcDetails);

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
}
