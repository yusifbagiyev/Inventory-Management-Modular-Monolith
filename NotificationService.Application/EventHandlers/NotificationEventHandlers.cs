using System.Security.Claims;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using NotificationService.Application.Interfaces;
using SharedServices.Events;
using SharedServices.Persistence;

namespace NotificationService.Application.EventHandlers
{
    /// <summary>
    /// Subscribes to other modules' events. Nothing happens inline: the work is queued to run once
    /// the originating transaction commits (and dropped if it rolls back), so slow WhatsApp calls
    /// or notification failures never delay or fail the user's request.
    /// </summary>
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

        /// <summary>The signed-in user of the request raising the event (read now: the work runs later, without it).</summary>
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
