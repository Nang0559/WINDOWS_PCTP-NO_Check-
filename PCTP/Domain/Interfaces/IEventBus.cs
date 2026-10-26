using PCTP.Domain.Events;
using System;

namespace PCTP.Domain.Interfaces
{
    public interface IEventBus
    {
        void Subscribe<TEvent>(Action<TEvent> handler) where TEvent : DomainEvent;
        void Publish<TEvent>(TEvent domainEvent) where TEvent : DomainEvent;
        void PublishSynchronous<TEvent>(TEvent domainEvent) where TEvent : DomainEvent;
        void Unsubscribe<TEvent>(Action<TEvent> handler) where TEvent : DomainEvent;
    }
}
