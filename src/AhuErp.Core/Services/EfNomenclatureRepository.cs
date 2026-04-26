using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using AhuErp.Core.Data;
using AhuErp.Core.Models;

namespace AhuErp.Core.Services
{
    /// <summary>EF6-реализация <see cref="INomenclatureRepository"/>.</summary>
    public sealed class EfNomenclatureRepository : INomenclatureRepository
    {
        private readonly AhuDbContext _ctx;

        public EfNomenclatureRepository(AhuDbContext ctx)
        {
            _ctx = ctx ?? throw new ArgumentNullException(nameof(ctx));
        }

        public IReadOnlyList<NomenclatureCase> ListCases(int? year, bool activeOnly)
        {
            IQueryable<NomenclatureCase> q = _ctx.NomenclatureCases;
            if (year.HasValue) q = q.Where(c => c.Year == year.Value);
            if (activeOnly) q = q.Where(c => c.IsActive);
            return q.OrderBy(c => c.Index).ToList().AsReadOnly();
        }

        public NomenclatureCase GetCase(int id) => _ctx.NomenclatureCases.Find(id);

        public NomenclatureCase AddCase(NomenclatureCase @case)
        {
            _ctx.NomenclatureCases.Add(@case);
            _ctx.SaveChanges();
            return @case;
        }

        public NomenclatureCase UpdateCase(NomenclatureCase @case)
        {
            if (_ctx.Entry(@case).State == EntityState.Detached)
            {
                _ctx.NomenclatureCases.Attach(@case);
                _ctx.Entry(@case).State = EntityState.Modified;
            }
            _ctx.SaveChanges();
            return @case;
        }

        public IReadOnlyList<DocumentTypeRef> ListTypes(bool activeOnly)
        {
            IQueryable<DocumentTypeRef> q = _ctx.DocumentTypeRefs;
            if (activeOnly) q = q.Where(t => t.IsActive);
            return q.OrderBy(t => t.Name).ToList().AsReadOnly();
        }

        public DocumentTypeRef GetType(int id) => _ctx.DocumentTypeRefs.Find(id);

        public DocumentTypeRef AddType(DocumentTypeRef typeRef)
        {
            _ctx.DocumentTypeRefs.Add(typeRef);
            _ctx.SaveChanges();
            return typeRef;
        }

        public DocumentTypeRef UpdateType(DocumentTypeRef typeRef)
        {
            if (_ctx.Entry(typeRef).State == EntityState.Detached)
            {
                _ctx.DocumentTypeRefs.Attach(typeRef);
                _ctx.Entry(typeRef).State = EntityState.Modified;
            }
            _ctx.SaveChanges();
            return typeRef;
        }

        public int GetMaxSequence(int documentTypeRefId, int year)
        {
            // Считаем не количество, а реальный максимум числовой последовательности
            // в уже выданных регистрационных номерах данного вида за указанный год.
            // Использование Count() было бы небезопасно: после удаления документа
            // следующий номер мог бы повторить уже выпущенный.
            var numbers = _ctx.Documents
                .Where(d => d.DocumentTypeRefId == documentTypeRefId
                            && d.RegistrationDate.HasValue
                            && d.RegistrationDate.Value.Year == year
                            && d.RegistrationNumber != null)
                .Select(d => d.RegistrationNumber)
                .ToList();

            int max = 0;
            foreach (var raw in numbers)
            {
                var seq = ParseTrailingSequence(raw);
                if (seq > max) max = seq;
            }
            return max;
        }

        /// <summary>
        /// Извлекает числовую последовательность из хвоста регистрационного номера
        /// (поддерживаем шаблоны вида «АХУ-01-02/2026-00037» — берём последний
        /// «числовой блок»). Возвращает 0, если распарсить не удалось.
        /// </summary>
        private static int ParseTrailingSequence(string registrationNumber)
        {
            if (string.IsNullOrEmpty(registrationNumber)) return 0;
            int end = registrationNumber.Length - 1;
            while (end >= 0 && !char.IsDigit(registrationNumber[end])) end--;
            if (end < 0) return 0;
            int start = end;
            while (start - 1 >= 0 && char.IsDigit(registrationNumber[start - 1])) start--;
            var slice = registrationNumber.Substring(start, end - start + 1);
            return int.TryParse(slice, out var value) ? value : 0;
        }

        /// <summary>
        /// В EF-реализации <see cref="GetMaxSequence"/> вычисляется по реальным
        /// документам, поэтому отдельный счётчик не нужен — метод пустой.
        /// </summary>
        public void BumpSequence(int documentTypeRefId, int year, int sequence)
        {
        }

        public IReadOnlyList<Department> ListDepartments()
            => _ctx.Departments.OrderBy(d => d.Name).ToList().AsReadOnly();

        public Department AddDepartment(Department department)
        {
            _ctx.Departments.Add(department);
            _ctx.SaveChanges();
            return department;
        }
    }
}
