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

using Xunit.Abstractions;

namespace Dapr.E2E.Test.Actors.Generators;

internal sealed class XUnitLoggingProvider : ILoggerProvider
{
    private readonly ITestOutputHelper output;

    public XUnitLoggingProvider(ITestOutputHelper output)
    {
        this.output = output;
    }

    public ILogger CreateLogger(string categoryName)
    {
        return new XUnitLogger(categoryName, this.output);
    }

    public void Dispose()
    {
    }

    private sealed class XUnitLogger : ILogger
    {
        private readonly string categoryName;
        private readonly ITestOutputHelper output;

        public XUnitLogger(string categoryName, ITestOutputHelper output)
        {
            this.categoryName = categoryName;
            this.output = output;
        }

#nullable disable
        public IDisposable BeginScope<TState>(TState state)
        {
            return new XUnitLoggerScope();
        }
#nullable enable

        public bool IsEnabled(LogLevel logLevel)
        {
            return true;
        }

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            this.output.WriteLine($"{this.categoryName}: {formatter(state, exception).TrimEnd(Environment.NewLine.ToCharArray())}");
        }
    }

    private sealed class XUnitLoggerScope : IDisposable
    {
        public void Dispose()
        {
        }
    }
}