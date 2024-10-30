// ------------------------------------------------------------------------
// Copyright 2022 The Dapr Authors
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

using System.Threading.Tasks;
using Autogenerated = Dapr.Client.Autogen.Grpc.v1;
using Xunit;
using FluentAssertions;
using System;

namespace Dapr.Client.Test
{
    [System.Obsolete]
    public class TryLockResponseTest
    {
        [Fact]
        public async Task TryLockAsync_WithAllValues_ValidateRequest()
        {
            await using var client = TestClient.CreateForDaprClient();
            string storeName = "redis";
            string resourceId = "resourceId";
            string lockOwner = "owner1";
            Int32 expiryInSeconds = 1000;
            var request = await client.CaptureGrpcRequestAsync(async daprClient =>
            {
                return await daprClient.Lock(storeName, resourceId, lockOwner, expiryInSeconds);
            });

            // Get Request and validate
            var envelope = await request.GetRequestEnvelopeAsync<Autogenerated.TryLockRequest>();
            envelope.StoreName.Should().Be("redis");
            envelope.ResourceId.Should().Be("resourceId");
            envelope.LockOwner.Should().Be("owner1");
            envelope.ExpiryInSeconds.Should().Be(1000);

            // Get response and validate
            var invokeResponse = new Autogenerated.TryLockResponse{
                Success = true
            };

            await request.CompleteWithMessageAsync(invokeResponse);

            //testing unlocking
            
            var unlockRequest = await client.CaptureGrpcRequestAsync(async daprClient =>
            {
                return await daprClient.Unlock(storeName, resourceId, lockOwner);
            });
            var unlockEnvelope = await unlockRequest.GetRequestEnvelopeAsync<Autogenerated.UnlockRequest>();
            unlockEnvelope.StoreName.Should().Be("redis");
            unlockEnvelope.ResourceId.Should().Be("resourceId");
            unlockEnvelope.LockOwner.Should().Be("owner1");

            var invokeUnlockResponse = new Autogenerated.UnlockResponse{
                Status = Autogenerated.UnlockResponse.Types.Status.LockDoesNotExist
            };

            var domainUnlockResponse = await unlockRequest.CompleteWithMessageAsync(invokeUnlockResponse);
            domainUnlockResponse.status.Should().Be(LockStatus.LockDoesNotExist);
        }
    }
}