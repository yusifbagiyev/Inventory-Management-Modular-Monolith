using System.Security.Claims;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using NotificationService.Application.Interfaces;
using SharedServices.Events;
using SharedServices.Persistence;

namespace NotificationService.Application.EventHandlers
{
    /// <summary>Queues notification work for other modules' events to run after the transaction commits.</summary>
    /// <remarks>Nothing runs inline, so slow WhatsApp calls or failures never delay or fail the request.</remarks>
    public class NotificationEventHandlers :
        INotificationHandler<ApprovalRequestCreatedEvent>,
        INotificationHandler<ApprovalRequestProcessedEvent>,
        INotificationHandler<ApprovalRequestCancelledEvent>,
        INotificationHandler<ProductCreatedEvent>,
        INotificationHandler<ProductDeletedEvent>,
        INotificationHandler<RouteCompletedEvent>
    {
        private readonly DbSession _session;
        private readonly IHttpContextAccessor _httpContext;

        public NotificationEventHandlers(DbSession session, IHttpContextAccessor httpContext)
        {
            _session = session;
            _httpContext = httpContext;
        }

        public Task Handle(ApprovalRequestCreatedEvent e, CancellationToken _) => Defer(d => d.ApprovalRequestCreatedAsync, e);
        public Task Handle(ApprovalRequestProcessedEvent e, CancellationToken _) => Defer(d => d.ApprovalRequestProcessedAsync, e);
        public Task Handle(ApprovalRequestCancelledEvent e, CancellationToken _) => Defer(d => d.ApprovalRequestCancelledAsync, e);
        public Task Handle(ProductCreatedEvent e, CancellationToken _) => Defer(e, ActorId(), (d, ev, actor, ct) => d.ProductCreatedAsync(ev, actor, ct));
        public Task Handle(ProductDeletedEvent e, CancellationToken _) => Defer(e, ActorId(), (d, ev, actor, ct) => d.ProductDeletedAsync(ev, actor, ct));
        public Task Handle(RouteCompletedEvent e, CancellationToken _) => Defer(e, ActorId(), (d, ev, actor, ct) => d.RouteCompletedAsync(ev, actor, ct));

        /// <summary>Reads the acting user now, because the deferred work runs without the request.</summary>
        private int? ActorId()
            => int.TryParse(_httpContext.HttpContext?.User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var id) ? id : null;

        private Task Defer<TEvent>(TEvent e, int? actorId, Func<INotificationDispatcher, TEvent, int?, CancellationToken, Task> method)
        {
            _session.AfterCommit((services, cancellationToken) =>
                method(services.GetRequiredService<INotificationDispatcher>(), e, actorId, cancellationToken));
            return Task.CompletedTask;
        }

        private Task Defer<TEvent>(Func<INotificationDispatcher, Func<TEvent, CancellationToken, Task>> method, TEvent e)
        {
            _session.AfterCommit((services, cancellationToken) =>
                method(services.GetRequiredService<INotificationDispatcher>())(e, cancellationToken));
            return Task.CompletedTask;
        }
    }
}
