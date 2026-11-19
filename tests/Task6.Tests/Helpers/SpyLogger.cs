using Microsoft.Extensions.Logging;

namespace Task6.Tests.Helpers
{
    // Ručno napisan zamjenski objekat (spy): pamti sve poruke koje servis zapiše
    // i javlja testu kada se pojavi poruka koju čeka.
    public class SpyLogger<T> : ILogger<T>
    {
        private readonly List<(LogLevel Level, string Message)> _entries = new();
        private readonly object _lock = new();
        private readonly Func<LogLevel, string, bool> _signal;
        private readonly TaskCompletionSource _signalled = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public SpyLogger(Func<LogLevel, string, bool> signal)
        {
            _signal = signal;
        }

        public IReadOnlyList<(LogLevel Level, string Message)> Entries
        {
            get { lock (_lock) { return _entries.ToList(); } }
        }

        // Čeka traženu poruku najviše 5 sekundi, da test nikad ne visi.
        public Task WaitForSignalAsync() => _signalled.Task.WaitAsync(TimeSpan.FromSeconds(5));

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            var message = formatter(state, exception);
            lock (_lock)
            {
                _entries.Add((logLevel, message));
            }

            if (_signal(logLevel, message))
                _signalled.TrySetResult();
        }
    }
}
