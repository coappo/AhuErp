using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using AhuErp.Core.Data;
using AhuErp.Core.Models;

namespace AhuErp.Core.Services
{
    /// <summary>EF6-репозиторий замещений (Phase 11).</summary>
    public sealed class EfSubstitutionRepository : ISubstitutionRepository
    {
        private readonly AhuDbContext _ctx;

        public EfSubstitutionRepository(AhuDbContext ctx)
        {
            _ctx = ctx ?? throw new ArgumentNullException(nameof(ctx));
        }

        public Substitution Add(Substitution substitution)
        {
            _ctx.Substitutions.Add(substitution);
            _ctx.SaveChanges();
            return substitution;
        }

        public Substitution Get(int id) => _ctx.Substitutions.Find(id);

        public void Update(Substitution substitution)
        {
            if (_ctx.Entry(substitution).State == EntityState.Detached)
            {
                _ctx.Substitutions.Attach(substitution);
                _ctx.Entry(substitution).State = EntityState.Modified;
            }
            _ctx.SaveChanges();
        }

        public IReadOnlyList<Substitution> ListAll()
            => _ctx.Substitutions
                .Include(s => s.OriginalEmployee)
                .Include(s => s.SubstituteEmployee)
                .OrderByDescending(s => s.From)
                .ToList()
                .AsReadOnly();

        public IReadOnlyList<Substitution> ListByOriginal(int originalEmployeeId)
            => _ctx.Substitutions
                .Include(s => s.SubstituteEmployee)
                .Where(s => s.OriginalEmployeeId == originalEmployeeId)
                .OrderByDescending(s => s.From)
                .ToList()
                .AsReadOnly();

        public IReadOnlyList<Substitution> ListActive(DateTime now)
            => _ctx.Substitutions
                .Include(s => s.OriginalEmployee)
                .Include(s => s.SubstituteEmployee)
                .Where(s => s.IsActive && s.From <= now && now <= s.To)
                .OrderByDescending(s => s.From)
                .ToList()
                .AsReadOnly();
    }
}
