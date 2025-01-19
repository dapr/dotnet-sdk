// ------------------------------------------------------------------------
// Copyright 2021 The Dapr Authors
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

namespace Dapr.AspNetCore.Test
{
    using System;
    using System.Text.Json;
    using System.Threading.Tasks;
    using Dapr.Client;
    using Dapr.Client.Autogen.Grpc.v1;
    using Shouldly;
    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.AspNetCore.Mvc.ModelBinding;
    using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
    using Microsoft.Extensions.DependencyInjection;
    using Xunit;

    public class StateEntryModelBinderTest
    {
        [Fact]
        public async Task BindAsync_WithoutMatchingRouteValue_ReportsError()
        {
            await using var client = TestClient.CreateForDaprClient();

            var binder = new StateEntryModelBinder("testStore", "test", isStateEntry: false, typeof(Widget));
            var context = CreateContext(CreateServices(client.InnerClient));

            await binder.BindModelAsync(context);

            context.Result.IsModelSet.ShouldBeFalse();
            context.ModelState.ErrorCount.ShouldBe(1);
            context.ModelState["testParameter"].Errors.Count.ShouldBe(1);

            // No request to state store, validated by disposing client
        }

        [Fact]
        public async Task BindAsync_CanBindValue()
        {
            await using var client = TestClient.CreateForDaprClient();

            var binder = new StateEntryModelBinder("testStore", "id", isStateEntry: false, typeof(Widget));

            // Configure Client
            var context = CreateContext(CreateServices(client.InnerClient));
            context.HttpContext.Request.RouteValues["id"] = "test";

            var request = await client.CaptureGrpcRequestAsync(async _ =>
            {
                await binder.BindModelAsync(context);
            });

            // Create Response & Respond
            var state = new Widget() { Size = "small", Color = "yellow", };
            await SendResponseWithState(state, request);

            // Get response and validate
            context.Result.IsModelSet.ShouldBeTrue();
            ((Widget)context.Result.Model).Size.ShouldBe("small");
            ((Widget)context.Result.Model).Color.ShouldBe("yellow");

            context.ValidationState.Count.ShouldBe(1);
            context.ValidationState[context.Result.Model].SuppressValidation.ShouldBeTrue();
        }

        [Fact]
        public async Task BindAsync_CanBindStateEntry()
        {
            await using var client = TestClient.CreateForDaprClient();

            var binder = new StateEntryModelBinder("testStore", "id", isStateEntry: true, typeof(Widget));

            // Configure Client
            var context = CreateContext(CreateServices(client.InnerClient));
            context.HttpContext.Request.RouteValues["id"] = "test";

            var request = await client.CaptureGrpcRequestAsync(async _ =>
            {
                await binder.BindModelAsync(context);
            });

            // Create Response & Respond
            var state = new Widget() { Size = "small", Color = "yellow", };
            await SendResponseWithState(state, request);

            // Get response and validate
            context.Result.IsModelSet.ShouldBeTrue();
            ((StateEntry<Widget>)context.Result.Model).Key.ShouldBe("test");
            ((StateEntry<Widget>)context.Result.Model).Value.Size.ShouldBe("small");
            ((StateEntry<Widget>)context.Result.Model).Value.Color.ShouldBe("yellow");

            context.ValidationState.Count.ShouldBe(1);
            context.ValidationState[context.Result.Model].SuppressValidation.ShouldBeTrue();
        }

        [Fact]
        public async Task BindAsync_ReturnsNullForNonExistentStateEntry()
        {
            await using var client = TestClient.CreateForDaprClient();

            var binder = new StateEntryModelBinder("testStore", "id", isStateEntry: false, typeof(Widget));

            // Configure Client
            var context = CreateContext(CreateServices(client.InnerClient));
            context.HttpContext.Request.RouteValues["id"] = "test";

            var request = await client.CaptureGrpcRequestAsync(async _ =>
            {
                await binder.BindModelAsync(context);
            });

            await SendResponseWithState<string>(null, request);

            context.ModelState.IsValid.ShouldBeTrue();
            context.Result.IsModelSet.ShouldBeFalse();
            context.Result.ShouldBe(ModelBindingResult.Failed());
        }

        [Fact]
        public async Task BindAsync_WithStateEntry_ForNonExistentStateEntry()
        {
            await using var client = TestClient.CreateForDaprClient();

            var binder = new StateEntryModelBinder("testStore", "id", isStateEntry: true, typeof(Widget));

            // Configure Client
            var context = CreateContext(CreateServices(client.InnerClient));
            context.HttpContext.Request.RouteValues["id"] = "test";

            var request = await client.CaptureGrpcRequestAsync(async _ =>
            {
                await binder.BindModelAsync(context);
            });

            await SendResponseWithState<string>(null, request);

            context.ModelState.IsValid.ShouldBeTrue();
            context.Result.IsModelSet.ShouldBeTrue();
            ((StateEntry<Widget>)context.Result.Model).Value.ShouldBeNull();
        }

        private static ModelBindingContext CreateContext(IServiceProvider services)
        {
            return new DefaultModelBindingContext()
            {
                ActionContext = new ActionContext()
                {
                    HttpContext = new DefaultHttpContext()
                    {
                        RequestServices = services,
                    },
                },
                ModelState = new ModelStateDictionary(),
                ModelName = "testParameter",
                ValidationState = new ValidationStateDictionary(),
            };
        }

        private async Task SendResponseWithState<T>(T state, TestClient<DaprClient>.TestGrpcRequest request)
        {
            var stateData = TypeConverters.ToJsonByteString(state, new JsonSerializerOptions(JsonSerializerDefaults.Web));
            var stateResponse = new GetStateResponse()
            {
                Data = stateData,
                Etag = "test",
            };

            await request.CompleteWithMessageAsync(stateResponse);
        }

        private static IServiceProvider CreateServices(DaprClient daprClient)
        {
            var services = new ServiceCollection();
            services.AddSingleton(daprClient);
            return services.BuildServiceProvider();
        }

        private class Widget
        {
            public string Size { get; set; }

            public string Color { get; set; }
        }
    }
}
