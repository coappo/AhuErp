using System.Collections.Generic;
using AhuErp.Core.Models;

namespace AhuErp.Core.Services
{
    public interface ISavedSearchService
    {
        SavedSearch Save(int ownerId, string name, DocumentSearchFilter filter, bool isShared);
        IReadOnlyList<SavedSearch> ListForUser(int ownerId);
        SavedSearch Get(int id);

        /// <summary>Десериализует фильтр из <see cref="SavedSearch.FilterJson"/>.</summary>
        DocumentSearchFilter LoadFilter(int id);
        void Delete(int id, int actorId);
    }
}
