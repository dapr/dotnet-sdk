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