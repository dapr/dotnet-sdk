// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
// ------------------------------------------------------------
using System;
using System.Threading.Tasks;
using Xunit.Abstractions;

namespace Dapr.E2E.Test
{
    // Guarantees we run the app a single time for the whole test class.
    public class DaprTestAppFixture : IDisposable
    {
        private readonly object @lock = new object();
        private Task<State> task;

        public void Dispose()
        {
            Task<State> task;
            lock (@lock)
            {
                if (this.task is null)
                {
                    // App didn't start, do nothing.
                    return;
                }

                task = this.task;
            }

            State state;
            try
            {
                state = this.task.GetAwaiter().GetResult();
            }
            catch
            {
                // App failed during start, do nothing.
                return;
            }

            try
            {
                state.App?.Stop();
            }
            catch (Exception ex)
            {
                try
                {
                    // see: https://github.com/xunit/xunit/issues/2146
                    state.Output.WriteLine("Failed to shut down app: " + ex);
                }
                catch (InvalidOperationException)
                {
                }
            }
        }

        public Task<State> StartAsync(ITestOutputHelper output)
        {
            lock (@lock)
            {
                if (this.task is null)
                {
                    this.task = Task.Run(() => Launch(output));
                }

                return this.task;
            }
        }

        private State Launch(ITestOutputHelper output)
        {
            var app = new DaprTestApp(output, "testapp", useAppPort: true);
            try
            {
                var (httpEndpoint, grpcEndpoint) = app.Start();
                return new State()
                {
                    App = app,
                    HttpEndpoint = httpEndpoint,
                    GrpcEndpoint = grpcEndpoint,
                    Output = output,
                };
            }
            catch (Exception startException)
            {
                try
                {
                    app.Stop();
                    throw;
                }
                catch (Exception stopException)
                {
                    throw new AggregateException(startException, stopException);
                }
            }
        }

        public class State
        {
            public string HttpEndpoint;
            public string GrpcEndpoint;
            public DaprTestApp App;
            public ITestOutputHelper Output;
        }
    }
}
