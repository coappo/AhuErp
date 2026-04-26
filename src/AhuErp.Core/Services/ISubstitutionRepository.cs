using System;
using System.Collections.Generic;
using AhuErp.Core.Models;

namespace AhuErp.Core.Services
{
    /// <summary>Репозиторий замещений сотрудников (Phase 11).</summary>
    public interface ISubstitutionRepository
    {
        Substitution Add(Substitution substitution);
        Substitution Get(int id);
        void Update(Substitution substitution);
        IReadOnlyList<Substitution> ListAll();

        /// <summary>
        /// Все замещения сотрудника <paramref name="originalEmployeeId"/>,
        /// независимо от <c>IsActive</c> и интервала.
        /// </summary>
        IReadOnlyList<Substitution> ListByOriginal(int originalEmployeeId);

        /// <summary>
        /// Активные замещения (IsActive=true и <see cref="Substitution.From"/>
        /// ≤ <paramref name="now"/> ≤ <see cref="Substitution.To"/>).
        /// </summary>
        IReadOnlyList<Substitution> ListActive(DateTime now);
    }
}
