using PCTP.Domain.Events;
using PCTP.Domain.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Windows.Forms;

namespace PCTP.Infrastructure
{
    /// <summary>
    /// In-process EventBus — thread-safe, không cần thư viện ngoài.
    /// Publish() giữ semantics bất đồng bộ hiện tại.
    /// PublishSynchronous() dùng cho các application flow cần chờ UI quyết định.
    /// </summary>
    public class InProcessEventBus : IEventBus
    {
        private readonly Dictionary<Type, List<Delegate>> _handlers
            = new Dictionary<Type, List<Delegate>>();
        private readonly object _lock = new object();

        // BuildPresenter chạy trên UI thread → capture đúng SynchronizationContext.
        private readonly SynchronizationContext _uiContext
            = SynchronizationContext.Current
              ?? new WindowsFormsSynchronizationContext();

        public void Subscribe<TEvent>(Action<TEvent> handler)
            where TEvent : DomainEvent
        {
            lock (_lock)
            {
                var key = typeof(TEvent);
                if (!_handlers.ContainsKey(key))
                    _handlers[key] = new List<Delegate>();
                _handlers[key].Add(handler);
            }
        }

        public void Unsubscribe<TEvent>(Action<TEvent> handler)
            where TEvent : DomainEvent
        {
            lock (_lock)
            {
                if (_handlers.TryGetValue(typeof(TEvent), out var list))
                    list.Remove(handler);
            }
        }

        public void Publish<TEvent>(TEvent domainEvent)
            where TEvent : DomainEvent
        {
            var snapshot = GetHandlers<TEvent>();
            if (snapshot.Count == 0) return;

            // Preserve existing asynchronous semantics for normal UI events.
            _uiContext.Post(_ => InvokeHandlers(snapshot, domainEvent), null);
        }

        public void PublishSynchronous<TEvent>(TEvent domainEvent)
            where TEvent : DomainEvent
        {
            var snapshot = GetHandlers<TEvent>();
            if (snapshot.Count == 0) return;

            // The FIFO confirmation flow must not continue to CNK until the user
            // has answered the dialog. Send blocks the caller until the UI handler
            // has completed. If already on the UI thread, invoke directly to avoid
            // a self-deadlock.
            if (SynchronizationContext.Current == _uiContext)
            {
                InvokeHandlers(snapshot, domainEvent);
                return;
            }

            _uiContext.Send(_ => InvokeHandlers(snapshot, domainEvent), null);
        }

        private List<Delegate> GetHandlers<TEvent>() where TEvent : DomainEvent
        {
            lock (_lock)
            {
                if (!_handlers.TryGetValue(typeof(TEvent), out var list))
                    return new List<Delegate>();
                return list.ToList();
            }
        }

        private static void InvokeHandlers<TEvent>(IEnumerable<Delegate> handlers, TEvent domainEvent)
            where TEvent : DomainEvent
        {
            foreach (var handler in handlers)
                ((Action<TEvent>)handler)(domainEvent);
        }
    }
}
