using System;
using System.Collections.Generic;
using System.Linq;
using AhuErp.Core.Models;

namespace AhuErp.Core.Services
{
    /// <summary>
    /// Тривиальный репозиторий сотрудников на базе <see cref="List{T}"/>.
    /// Используется в тестах и в демо-режиме UI, пока отсутствует подключение к БД.
    /// </summary>
    public sealed class InMemoryEmployeeRepository : IEmployeeRepository
    {
        private readonly List<Employee> _employees;

        public InMemoryEmployeeRepository(IEnumerable<Employee> employees = null)
        {
            _employees = employees != null ? employees.ToList() : new List<Employee>();
        }

        public Employee FindByFullName(string fullName)
        {
            if (string.IsNullOrWhiteSpace(fullName)) return null;
            return _employees.FirstOrDefault(e =>
                string.Equals(e.FullName, fullName, StringComparison.OrdinalIgnoreCase));
        }

        public Employee GetById(int id) => _employees.FirstOrDefault(e => e.Id == id);

        public IReadOnlyList<Employee> ListAll() => _employees.AsReadOnly();

        public void Add(Employee employee)
        {
            if (employee == null) throw new ArgumentNullException(nameof(employee));
            _employees.Add(employee);
        }

        public IReadOnlyList<Employee> All() => _employees.AsReadOnly();
    }
}
