// ------------------------------------------------------------------------
// Copyright 2023 The Dapr Authors
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

using System;
using System.Threading;
using System.Threading.Tasks;
using Dapr.Actors.Runtime;
using Dapr.Actors.Communication;
using Moq;
using Xunit;

namespace Dapr.Actors.Test
{
    public class ActorStateManagerUnloadStateTest
    {
        [Fact]
        public async Task UnloadState_RemovesFromMemoryButNotStore()
        {
            var interactor = new Moq.Mock<TestDaprInteractor>();
            // Simulate state existence only after SaveStateAsync
            bool stateSaved = false;
            interactor.Setup(d => d.GetStateAsync(
                Moq.It.IsAny<string>(),
                Moq.It.IsAny<string>(),
                Moq.It.Is<string>(key => key == "big-data"),
                Moq.It.IsAny<CancellationToken>()))
                .ReturnsAsync(() =>
                    stateSaved
                        ? new Dapr.Actors.Communication.ActorStateResponse<string>("\"payload\"", null)
                        : new Dapr.Actors.Communication.ActorStateResponse<string>("", null));
            var host = ActorHost.CreateForTest<TestActor>();
            host.StateProvider = new DaprStateProvider(interactor.Object, new System.Text.Json.JsonSerializerOptions());
            var mngr = new ActorStateManager(new TestActor(host));
            var token = new CancellationToken();

            // Add and save state
            await mngr.AddStateAsync("big-data", "payload", token);
            await mngr.SaveStateAsync(token);
            stateSaved = true;
            Assert.Equal("payload", await mngr.GetStateAsync<string>("big-data", token));

            // Unload from memory
            await mngr.UnloadStateAsync("big-data");

            // Should reload from store
            interactor.Setup(d => d.GetStateAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new Dapr.Actors.Communication.ActorStateResponse<string>("\"payload\"", null));
            Assert.Equal("payload", await mngr.GetStateAsync<string>("big-data", token));
        }

        [Fact]
        public async Task UnloadState_ThrowsIfModifiedUnlessAllowed()
        {
            var interactor = new Moq.Mock<TestDaprInteractor>();
            // Default: state does not exist
            interactor.Setup(d => d.GetStateAsync(
                Moq.It.IsAny<string>(),
                Moq.It.IsAny<string>(),
                Moq.It.IsAny<string>(),
                Moq.It.IsAny<CancellationToken>()))
                .ReturnsAsync(new Dapr.Actors.Communication.ActorStateResponse<string>("", null));
            var host = ActorHost.CreateForTest<TestActor>();
            host.StateProvider = new DaprStateProvider(interactor.Object, new System.Text.Json.JsonSerializerOptions());
            var mngr = new ActorStateManager(new TestActor(host));
            var token = new CancellationToken();

            await mngr.AddStateAsync("key", "value", token);
            // Not yet saved, so is modified
            await Assert.ThrowsAsync<InvalidOperationException>(() => mngr.UnloadStateAsync("key"));

            // Should not throw if allowed
            await mngr.UnloadStateAsync("key", new UnloadStateOptions { AllowUnloadingWhenStateModified = true });
        }
    }
}
