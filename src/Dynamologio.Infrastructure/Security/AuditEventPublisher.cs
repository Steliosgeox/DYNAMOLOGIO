using System;
using System.Collections.Generic;
using Dynamologio.Core.Enums;
using Dynamologio.Core.Interfaces;
using Dynamologio.Core.Models;
using Newtonsoft.Json;

namespace Dynamologio.Infrastructure.Security
{
    public class AuditEventPublisher : IAuditEventPublisher
    {
        private readonly IEnumerable<IAuditEventSink> _sinks;
        private readonly ICurrentActor _actor;
        private readonly IClock _clock;

        public AuditEventPublisher(IEnumerable<IAuditEventSink> sinks, ICurrentActor actor, IClock clock)
        {
            _sinks = sinks ?? throw new ArgumentNullException(nameof(sinks));
            _actor = actor ?? throw new ArgumentNullException(nameof(actor));
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        }

        public void Publish(AuditAction action, string entityType, string entityId, string summary, object oldValue = null, object newValue = null)
        {
            var ev = new AuditEvent
            {
                Username = _actor.GetActor(),
                Action = action,
                EntityType = entityType ?? string.Empty,
                EntityId = entityId ?? string.Empty,
                Summary = summary ?? string.Empty,
                OldValueJson = oldValue != null ? JsonConvert.SerializeObject(oldValue) : string.Empty,
                NewValueJson = newValue != null ? JsonConvert.SerializeObject(newValue) : string.Empty,
                AppVersion = "1.0.0.0",
                CreatedAt = _clock.Now,
                CreatedBy = _actor.GetActor(),
                ModifiedAt = _clock.Now,
                ModifiedBy = _actor.GetActor()
            };

            foreach(var sink in _sinks)
            {
                sink.Sink(ev);
            }
        }
    }
}
