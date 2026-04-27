using System;
using System.Collections.Generic;
using System.Linq;
using AhuErp.Core.Models;

namespace AhuErp.Core.Services
{
    public sealed class InMemorySavedSearchRepository : ISavedSearchRepository
    {
        private readonly List<SavedSearch> _items = new List<SavedSearch>();
        private int _nextId;

        public SavedSearch Add(SavedSearch search)
        {
            if (search == null) throw new ArgumentNullException(nameof(search));
            search.Id = ++_nextId;
            _items.Add(search);
            return search;
        }

        public SavedSearch Get(int id) => _items.FirstOrDefault(x => x.Id == id);

        public IReadOnlyList<SavedSearch> ListVisibleTo(int ownerId)
            => _items.Where(x => x.OwnerId == ownerId || x.IsShared)
                     .OrderBy(x => x.Name)
                     .ToList()
                     .AsReadOnly();

        public void Update(SavedSearch search)
        {
            var idx = _items.FindIndex(x => x.Id == search.Id);
            if (idx >= 0) _items[idx] = search;
        }

        public void Remove(int id) => _items.RemoveAll(x => x.Id == id);
    }
}
