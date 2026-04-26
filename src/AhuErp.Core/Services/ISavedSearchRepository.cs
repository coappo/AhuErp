using System.Collections.Generic;
using AhuErp.Core.Models;

namespace AhuErp.Core.Services
{
    public interface ISavedSearchRepository
    {
        SavedSearch Add(SavedSearch search);
        SavedSearch Get(int id);
        IReadOnlyList<SavedSearch> ListVisibleTo(int ownerId);
        void Update(SavedSearch search);
        void Remove(int id);
    }
}
