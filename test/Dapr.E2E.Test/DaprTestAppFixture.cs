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
using System;
using System.Threading.Tasks;
using Xunit.Abstractions;

namespace Dapr.E2E.Test;

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

    public Task<State> StartAsync(ITestOutputHelper output, DaprRunConfiguration configuration)
    {
        lock (@lock)
        {
            if (this.task is null)
            {
                this.task = Task.Run(() => Launch(output, configuration));
            }

            return this.task;
        }
    }

    private State Launch(ITestOutputHelper output, DaprRunConfiguration configuration)
    {
        var app = new DaprTestApp(output, configuration.AppId);
        try
        {
            var (httpEndpoint, grpcEndpoint) = app.Start(configuration);
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