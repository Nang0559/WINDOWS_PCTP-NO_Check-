using DevExpress.CodeParser;
using PCTP.Domain.Events;
using PCTP.Domain.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace PCTP.Infrastructure
{
    /// <summary>
    /// In-process EventBus — thread-safe, không cần thư viện ngoài.
    /// Thay bằng MediatR sau nếu muốn.
    /// </summary>
    //public class InProcessEventBus : IEventBus
    //{
    //    private readonly Dictionary<Type, List<Delegate>> _handlers = new Dictionary<Type, List<Delegate>>();
    //    private readonly object _lock = new object(); // Thay Lock bằng object thường

    //    public void Subscribe<TEvent>(Action<TEvent> handler) where TEvent : DomainEvent
    //    {
    //        lock (_lock)
    //        {
    //            var key = typeof(TEvent);
    //            if (!_handlers.ContainsKey(key))
    //                _handlers[key] = new List<Delegate>();
    //            _handlers[key].Add(handler);
    //        }
    //    }

    //    public void Unsubscribe<TEvent>(Action<TEvent> handler) where TEvent : DomainEvent
    //    {
    //        lock (_lock)
    //        {
    //            if (_handlers.TryGetValue(typeof(TEvent), out var list))
    //                list.Remove(handler);
    //        }
    //    }

    //    public void Publish<TEvent>(TEvent domainEvent) where TEvent : DomainEvent
    //    {
    //        List<Delegate> snapshot;
    //        lock (_lock)
    //        {
    //            if (!_handlers.TryGetValue(typeof(TEvent), out var list)) return;
    //            snapshot = list.ToList();
    //        }
    //        // Chạy trên UI thread nếu cần (WinForms)
    //        foreach (var h in snapshot)
    //            ((Action<TEvent>)h)(domainEvent);
    //    }
    //}
    public class InProcessEventBus : IEventBus
    {
        private readonly Dictionary<Type, List<Delegate>> _handlers
            = new Dictionary<Type, List<Delegate>>();
        private readonly object _lock = new object();

        // Capture UI SynchronizationContext khi khởi tạo
        // BuildPresenter chạy trên UI thread → đúng
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
            List<Delegate> snapshot;
            lock (_lock)
            {
                if (!_handlers.TryGetValue(typeof(TEvent), out var list))
                    return;
                snapshot = list.ToList();
            }

            // Marshal về UI thread — fix cross-thread exception
            _uiContext.Post(_ =>
            {
                foreach (var h in snapshot)
                    ((Action<TEvent>)h)(domainEvent);
            }, null);
        }
    }
}
