using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using AhuErp.Core.Data;
using AhuErp.Core.Models;

namespace AhuErp.Core.Services
{
    public sealed class EfSavedSearchRepository : ISavedSearchRepository
    {
        private readonly AhuDbContext _ctx;

        public EfSavedSearchRepository(AhuDbContext ctx)
        {
            _ctx = ctx ?? throw new ArgumentNullException(nameof(ctx));
        }

        public SavedSearch Add(SavedSearch search)
        {
            if (search == null) throw new ArgumentNullException(nameof(search));
            _ctx.SavedSearches.Add(search);
            _ctx.SaveChanges();
            return search;
        }

        public SavedSearch Get(int id) => _ctx.SavedSearches.Find(id);

        public IReadOnlyList<SavedSearch> ListVisibleTo(int ownerId)
            => _ctx.SavedSearches
                .Where(x => x.OwnerId == ownerId || x.IsShared)
                .OrderBy(x => x.Name)
                .ToList()
                .AsReadOnly();

        public void Update(SavedSearch search)
        {
            if (search == null) throw new ArgumentNullException(nameof(search));
            _ctx.Entry(search).State = EntityState.Modified;
            _ctx.SaveChanges();
        }

        public void Remove(int id)
        {
            var existing = _ctx.SavedSearches.Find(id);
            if (existing != null)
            {
                _ctx.SavedSearches.Remove(existing);
                _ctx.SaveChanges();
            }
        }
    }
}
