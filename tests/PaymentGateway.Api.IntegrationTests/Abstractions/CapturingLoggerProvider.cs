using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace PaymentGateway.Api.IntegrationTests.Abstractions;

internal sealed class CapturingLoggerProvider : ILoggerProvider
{
    private readonly ConcurrentQueue<string> _lines = new();

    public IReadOnlyCollection<string> Lines => _lines;

    public string Rendered => string.Join(Environment.NewLine, _lines);

    public ILogger CreateLogger(string categoryName) => new CapturingLogger(categoryName, _lines);

    public void Dispose()
    {
    }

    private sealed class CapturingLogger : ILogger
    {
        private readonly string _categoryName;
        private readonly ConcurrentQueue<string> _lines;

        public CapturingLogger(string categoryName, ConcurrentQueue<string> lines)
        {
            _categoryName = categoryName;
            _lines = lines;
        }

        public IDisposable BeginScope<TState>(TState state)
            where TState : notnull
        {
            _lines.Enqueue($"{_categoryName} scope: {state}");

            return NullScope.Instance;
        }

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            _lines.Enqueue($"{logLevel} {_categoryName} {formatter(state, exception)} {exception}");
        }
    }

    private sealed class NullScope : IDisposable
    {
        public static readonly NullScope Instance = new();

        public void Dispose()
        {
        }
    }
}
