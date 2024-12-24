using Grpc.Core;
using Xunit;

namespace Dapr.Common.Test
{
    public class DaprErrorsTest
    {
        [Fact]
        public void DaprExtendedErrorInfo_ThrowsRpcException_ShouldGetExtendedErrorInfo()
        {
            // Arrange


            // Act
            //try
            //{
            //    ThrowsRpcBasedDaprException();
            //}

            // catch (DaprException daprEx)
            // {
                // daprEx.
            // }

        }

        private static void ThrowsRpcBasedDaprException()
        {
            try
            {
                throw new RpcException(new Status(StatusCode.Aborted, "Operation Aborted"));
            }
            catch (RpcException ex) 
            {
                throw new DaprException("An Error Occured", ex);
            }
        }
    }
}
