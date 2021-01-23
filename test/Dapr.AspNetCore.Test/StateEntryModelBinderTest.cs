﻿// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
// ------------------------------------------------------------

namespace Dapr.AspNetCore.Test
{
    using System;
    using System.Net;
    using System.Text.Json;
    using System.Threading.Tasks;
    using Dapr.Client;
    using Dapr.Client.Autogen.Grpc.v1;
    using FluentAssertions;
    using Grpc.Net.Client;
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
            var binder = new StateEntryModelBinder("testStore", "test", isStateEntry: false, typeof(Widget));

            var httpClient = new TestHttpClient();
            var context = CreateContext(CreateServices(httpClient));

            await binder.BindModelAsync(context);
            context.Result.IsModelSet.Should().BeFalse();
            context.ModelState.ErrorCount.Should().Be(1);
            context.ModelState["testParameter"].Errors.Count.Should().Be(1);

            httpClient.Requests.Count.Should().Be(0);
        }

        [Fact]
        public async Task BindAsync_CanBindValue()
        {
            var binder = new StateEntryModelBinder("testStore", "id", isStateEntry: false, typeof(Widget));

            // Configure Client
            var httpClient = new TestHttpClient();
            var context = CreateContext(CreateServices(httpClient));
            context.HttpContext.Request.RouteValues["id"] = "test";
            var task = binder.BindModelAsync(context);

            // Create Response & Respond
            var state = new Widget() { Size = "small", Color = "yellow", };
            httpClient.Requests.TryDequeue(out var entry).Should().BeTrue();
            await SendResponseWithState(state, entry);

            // Get response and validate
            await task;
            context.Result.IsModelSet.Should().BeTrue();
            context.Result.Model.As<Widget>().Size.Should().Be("small");
            context.Result.Model.As<Widget>().Color.Should().Be("yellow");

            context.ValidationState.Count.Should().Be(1);
            context.ValidationState[context.Result.Model].SuppressValidation.Should().BeTrue();
        }

        [Fact]
        public async Task BindAsync_CanBindStateEntry()
        {
            var binder = new StateEntryModelBinder("testStore", "id", isStateEntry: true, typeof(Widget));

            // Configure Client
            var httpClient = new TestHttpClient();
            var context = CreateContext(CreateServices(httpClient));
            context.HttpContext.Request.RouteValues["id"] = "test";
            var task = binder.BindModelAsync(context);

            // Create Response & Respond
            var state = new Widget() { Size = "small", Color = "yellow", };
            httpClient.Requests.TryDequeue(out var entry).Should().BeTrue();
            await SendResponseWithState(state, entry);

            // Get response and validate
            await task;
            context.Result.IsModelSet.Should().BeTrue();
            context.Result.Model.As<StateEntry<Widget>>().Key.Should().Be("test");
            context.Result.Model.As<StateEntry<Widget>>().Value.Size.Should().Be("small");
            context.Result.Model.As<StateEntry<Widget>>().Value.Color.Should().Be("yellow");

            context.ValidationState.Count.Should().Be(1);
            context.ValidationState[context.Result.Model].SuppressValidation.Should().BeTrue();
        }

        [Fact]
        public async Task BindAsync_ReturnsNullForNonExistentStateEntry()
        {
            var binder = new StateEntryModelBinder("testStore", "id", isStateEntry: false, typeof(Widget));

            // Configure Client
            var httpClient = new TestHttpClient();
            var context = CreateContext(CreateServices(httpClient));
            context.HttpContext.Request.RouteValues["id"] = "test";
            var task = binder.BindModelAsync(context);

            httpClient.Requests.TryDequeue(out var entry).Should().BeTrue();
            await SendResponseWithState<string>(null, entry);

            await task;
            context.ModelState.IsValid.Should().BeTrue();
            context.Result.IsModelSet.Should().BeFalse();
            context.Result.Should().Be(ModelBindingResult.Failed());
        }

        [Fact]
        public async Task BindAsync_WithStateEntry_ForNonExistentStateEntry()
        {
            var binder = new StateEntryModelBinder("testStore", "id", isStateEntry: true, typeof(Widget));

            // Configure Client
            var httpClient = new TestHttpClient();
            var context = CreateContext(CreateServices(httpClient));
            context.HttpContext.Request.RouteValues["id"] = "test";
            var task = binder.BindModelAsync(context);

            httpClient.Requests.TryDequeue(out var entry).Should().BeTrue();
            await SendResponseWithState<string>(null, entry);

            await task;
            context.ModelState.IsValid.Should().BeTrue();
            context.Result.IsModelSet.Should().BeTrue();
            ((StateEntry<Widget>)context.Result.Model).Value.Should().BeNull();
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

        private async Task SendResponseWithState<T>(T state, TestHttpClient.Entry entry)
        {
            var stateData = TypeConverters.ToJsonByteString(state, new JsonSerializerOptions(JsonSerializerDefaults.Web));
            var stateResponse = new GetStateResponse
            {
                Data = stateData,
                Etag = "test",
            };

            var streamContent = await GrpcUtils.CreateResponseContent(stateResponse);
            var response = GrpcUtils.CreateResponse(HttpStatusCode.OK, streamContent);
            entry.Completion.SetResult(response);
        }

        private static IServiceProvider CreateServices(TestHttpClient httpClient)
        {
            var services = new ServiceCollection();
            var daprClient = new DaprClientBuilder()
                .UseGrpcChannelOptions(new GrpcChannelOptions { HttpClient = httpClient })
                .Build();

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
