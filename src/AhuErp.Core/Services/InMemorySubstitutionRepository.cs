using System;
using System.Collections.Generic;
using System.Linq;
using AhuErp.Core.Models;

namespace AhuErp.Core.Services
{
    /// <summary>In-memory реализация для тестов и демо-режима.</summary>
    public sealed class InMemorySubstitutionRepository : ISubstitutionRepository
    {
        private readonly List<Substitution> _items = new List<Substitution>();
        private int _nextId = 1;

        public Substitution Add(Substitution substitution)
        {
            if (substitution == null) throw new ArgumentNullException(nameof(substitution));
            if (substitution.Id == 0) substitution.Id = _nextId++;
            else _nextId = Math.Max(_nextId, substitution.Id + 1);
            _items.Add(substitution);
            return substitution;
        }

        public Substitution Get(int id) => _items.FirstOrDefault(s => s.Id == id);

        public void Update(Substitution substitution)
        {
            if (substitution == null) throw new ArgumentNullException(nameof(substitution));
            var index = _items.FindIndex(s => s.Id == substitution.Id);
            if (index < 0) throw new InvalidOperationException($"Замещение #{substitution.Id} не найдено.");
            _items[index] = substitution;
        }

        public IReadOnlyList<Substitution> ListAll()
            => _items.OrderByDescending(s => s.From).ToList().AsReadOnly();

        public IReadOnlyList<Substitution> ListByOriginal(int originalEmployeeId)
            => _items.Where(s => s.OriginalEmployeeId == originalEmployeeId)
                     .OrderByDescending(s => s.From).ToList().AsReadOnly();

        public IReadOnlyList<Substitution> ListActive(DateTime now)
            => _items.Where(s => s.CoversMoment(now))
                     .OrderByDescending(s => s.From).ToList().AsReadOnly();
    }
}
