namespace Pigeon.Messaging.Consuming.Dispatching
{
    using System.Threading;

    internal sealed class ConsumeContextAccessor : IConsumeContextAccessor
    {
        private static readonly AsyncLocal<ContextHolder> CurrentContext = new();

        public ConsumeContext ConsumeContext => CurrentContext.Value?.Context;

        public IDisposable Push(ConsumeContext context)
        {
            var previous = CurrentContext.Value;
            CurrentContext.Value = new ContextHolder { Context = context };
            return new RestoreContextScope(previous);
        }

        private sealed class RestoreContextScope : IDisposable
        {
            private readonly ContextHolder _previous;
            private bool _disposed;

            public RestoreContextScope(ContextHolder previous)
            {
                _previous = previous;
            }

            public void Dispose()
            {
                if (_disposed)
                    return;

                CurrentContext.Value = _previous;
                _disposed = true;
            }
        }

        private sealed class ContextHolder
        {
            public ConsumeContext Context { get; init; }
        }
    }
}
