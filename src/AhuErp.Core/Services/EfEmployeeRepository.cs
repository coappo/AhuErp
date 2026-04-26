using System;
using System.Linq;
using AhuErp.Core.Data;
using AhuErp.Core.Models;

namespace AhuErp.Core.Services
{
    /// <summary>
    /// EF6-реализация <see cref="IEmployeeRepository"/>. Поиск по ФИО полагается на
    /// case-insensitive collation SQL Server (по умолчанию <c>SQL_Latin1_General_CP1_CI_AS</c>
    /// или аналогичная), что эквивалентно <see cref="StringComparison.OrdinalIgnoreCase"/>
    /// из in-memory реализации для типовых русских ФИО.
    /// </summary>
    public sealed class EfEmployeeRepository : IEmployeeRepository
    {
        private readonly AhuDbContext _ctx;

        public EfEmployeeRepository(AhuDbContext ctx)
        {
            _ctx = ctx ?? throw new ArgumentNullException(nameof(ctx));
        }

        public Employee FindByFullName(string fullName)
        {
            if (string.IsNullOrWhiteSpace(fullName)) return null;
            return _ctx.Employees.FirstOrDefault(e => e.FullName == fullName);
        }

        public Employee GetById(int id) => _ctx.Employees.Find(id);
    }
}
