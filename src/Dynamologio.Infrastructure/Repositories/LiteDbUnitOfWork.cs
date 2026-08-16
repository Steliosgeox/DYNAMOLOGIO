using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using Dynamologio.Core.Interfaces;
using Dynamologio.Core.Models;
using Dynamologio.Infrastructure.LiteDb;
using LiteDB;

namespace Dynamologio.Infrastructure.Repositories
{
    public class LiteDbRepository<T> : IRepository<T> where T : EntityBase
    {
        protected readonly LiteDbContext _context;
        protected readonly ILiteCollection<T> _collection;

        public LiteDbRepository(LiteDbContext context, string collectionName = null)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _collection = _context.GetCollection<T>(collectionName ?? typeof(T).Name);
        }

        public virtual T GetById(Guid id)
        {
            return _collection.FindById(new BsonValue(id));
        }

        public virtual IEnumerable<T> GetAll()
        {
            return _collection.FindAll().ToList();
        }

        public virtual IEnumerable<T> Find(Expression<Func<T, bool>> predicate)
        {
            return _collection.Find(predicate).ToList();
        }

        public virtual void Insert(T entity)
        {
            if (entity == null) throw new ArgumentNullException(nameof(entity));
            if (entity.Id == Guid.Empty) entity.Id = Guid.NewGuid();
            entity.CreatedAt = DateTime.Now;
            entity.ModifiedAt = DateTime.Now;
            _collection.Insert(entity);
        }

        public virtual void Update(T entity)
        {
            if (entity == null) throw new ArgumentNullException(nameof(entity));
            entity.ModifiedAt = DateTime.Now;
            _collection.Update(entity);
        }

        public virtual void Delete(Guid id)
        {
            _collection.Delete(new BsonValue(id));
        }

        public virtual bool Exists(Guid id)
        {
            return _collection.Exists(Query.EQ("_id", new BsonValue(id)));
        }

        public virtual int Count()
        {
            return _collection.Count();
        }
    }

    public class LiteDbUnitOfWork : IUnitOfWork
    {
        private readonly LiteDbContext _context;
        private bool _disposed = false;

        public IRepository<Personnel> Personnel { get; }
        public IRepository<Rank> Ranks { get; }
        public IRepository<OrganisationUnit> OrganisationUnits { get; }
        public IRepository<StatusType> StatusTypes { get; }
        public IRepository<StatusEvent> StatusEvents { get; }
        public IRepository<ServiceType> ServiceTypes { get; }
        public IRepository<ServiceAssignment> ServiceAssignments { get; }
        public IRepository<AuditEvent> AuditEvents { get; }
        public IRepository<ReportTemplate> ReportTemplates { get; }
        public IRepository<ImportBatch> ImportBatches { get; }
        public IRepository<AppSetting> AppSettings { get; }

        public LiteDbUnitOfWork(LiteDbContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));

            Personnel = new LiteDbRepository<Personnel>(_context, "personnel");
            Ranks = new LiteDbRepository<Rank>(_context, "ranks");
            OrganisationUnits = new LiteDbRepository<OrganisationUnit>(_context, "organisation_units");
            StatusTypes = new LiteDbRepository<StatusType>(_context, "status_types");
            StatusEvents = new LiteDbRepository<StatusEvent>(_context, "status_events");
            ServiceTypes = new LiteDbRepository<ServiceType>(_context, "service_types");
            ServiceAssignments = new LiteDbRepository<ServiceAssignment>(_context, "service_assignments");
            AuditEvents = new LiteDbRepository<AuditEvent>(_context, "audit_events");
            ReportTemplates = new LiteDbRepository<ReportTemplate>(_context, "report_templates");
            ImportBatches = new LiteDbRepository<ImportBatch>(_context, "import_batches");
            AppSettings = new LiteDbRepository<AppSetting>(_context, "app_settings");

            EnsureIndexes();
        }

        private void EnsureIndexes()
        {
            var pCol = _context.GetCollection<Personnel>("personnel");
            pCol.EnsureIndex(x => x.MilitaryServiceNumber);
            pCol.EnsureIndex(x => x.LastName);
            pCol.EnsureIndex(x => x.RankId);
            pCol.EnsureIndex(x => x.OrganisationUnitId);
            pCol.EnsureIndex(x => x.IsArchived);

            var eCol = _context.GetCollection<StatusEvent>("status_events");
            eCol.EnsureIndex(x => x.PersonnelId);
            eCol.EnsureIndex(x => x.StartAt);
            eCol.EnsureIndex(x => x.EndAtExclusive);
            eCol.EnsureIndex(x => x.IsCancelled);

            var aCol = _context.GetCollection<AuditEvent>("audit_events");
            aCol.EnsureIndex(x => x.CreatedAt);
            aCol.EnsureIndex(x => x.EntityType);
        }

        public void Commit()
        {
            // LiteDB commits document updates automatically upon method invocation.
        }

        public void Rollback()
        {
            // LiteDB rollback handling where relevant
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _context?.Dispose();
                _disposed = true;
            }
        }
    }
}
