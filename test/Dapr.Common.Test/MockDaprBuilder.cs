using System;

namespace Dapr.Common.Test
{
    public sealed class MockDaprBuilder : DaprGenericClientBuilder<MockDaprClient>
    {
        public MockDaprBuilder()
        {
        }

        /// <summary>
        /// Builds the client instance from the properties of the builder.
        /// </summary>
        /// <returns>The Dapr client instance.</returns>
        /// <summary>
        /// Builds the client instance from the properties of the builder.
        /// </summary>
        public override MockDaprClient Build()
        {
            throw new NotImplementedException();
        }
    }

    public sealed class MockDaprClient
    {
    }
}
