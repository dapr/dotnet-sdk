using System.Collections.Generic;
using Dapr.Common.Exceptions;
using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;
using Google.Rpc;
using Grpc.Core;
using Xunit;

namespace Dapr.Common.Test
{
    public class DaprExtendedErrorInfoTest
    {

        private static string GrpcDetails = "grpc-status-details-bin";

        public static IEnumerable<object[]> supportedRicherErrorDetails = new List<object[]>()
        {
            new object[] { DaprExtendedErrorType.BadRequest, typeof(DaprBadRequestDetail) },
            new object[] { DaprExtendedErrorType.LocalizedMessage, typeof(DaprLocalizedMessageDetail)},
            new object[] { DaprExtendedErrorType.Help, typeof(DaprHelpDetail )},
            new object[] { DaprExtendedErrorType.PreconditionFailure, typeof(DaprPreconditionFailureDetail )},
            new object[] { DaprExtendedErrorType.QuotaFailure, typeof(DaprQuotaFailureDetail )},
            new object[] { DaprExtendedErrorType.DebugInfo, typeof(DaprDebugInfoDetail )},
            new object[] { DaprExtendedErrorType.ErrorInfo, typeof(DaprErrorInfoDetail )},
            new object[] { DaprExtendedErrorType.ResourceInfo, typeof(DaprResourceInfoDetail )},
            new object[] { DaprExtendedErrorType.RetryInfo, typeof(DaprRetryInfoDetail )},
            new object[] { DaprExtendedErrorType.RequestInfo, typeof(DaprRequestInfoDetail )},
        };


        [Fact]
        public void DaprExtendedErrorInfo_ThrowsRpcException_ShouldGetExtendedErrorInfo()
        {
            // Arrange
            DaprExtendedErrorInfo result = null;

            // Act
            try
            {
                ThrowsRpcBasedDaprException();
            }

            catch (DaprException daprEx)
            {
                Assert.True(daprEx.TryGetExtendedErrorInfo(out result));
            }

            Assert.NotNull(result);
            Assert.Single(result.Details);
            Assert.Collection(result.Details, entry =>
            {
                Assert.IsAssignableFrom<DaprExtendedErrorDetail>(entry);
                Assert.IsType<DaprPreconditionFailureDetail>(entry);
            });
        }

        private static void ThrowsRpcBasedDaprException()
        {
            var metadataEntry = new Google.Rpc.Status()
            {
                Code = 1,
                Message = "Status Message",
            };

            PreconditionFailure failure = new();

            metadataEntry.Details.Add(new Any() { TypeUrl = "type.googleapis.com/Google.rpc.PreconditionFailure", Value = failure.ToByteString() });


            Metadata trailers = new()
            {
                { GrpcDetails, metadataEntry.ToByteArray() }
            };

            throw new DaprException("RpcBasedDaprException", new RpcException(status: new Grpc.Core.Status(StatusCode.FailedPrecondition, "PrecondtionFailure"), trailers: trailers));
        }
    }
}
