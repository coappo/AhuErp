using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace AhuErp.Core.Models
{
    /// <summary>
    /// Структурное подразделение учреждения. Поддерживает иерархию через
    /// <see cref="ParentDepartmentId"/> (в одном уровне допустимо несколько
    /// корней). Используется в номенклатуре дел, отчётах по исполнительской
    /// дисциплине и оргструктуре.
    /// </summary>
    public class Department
    {
        public int Id { get; set; }

        [Required]
        [StringLength(256)]
        public string Name { get; set; }

        /// <summary>Краткий код отдела для регистрационных индексов (например, «АХУ»).</summary>
        [StringLength(16)]
        public string ShortCode { get; set; }

        public bool IsActive { get; set; } = true;

        /// <summary>
        /// Родительское подразделение (Phase 11). Null — корневой отдел.
        /// FK self-referencing с ON DELETE NO ACTION; удаление родителя
        /// при наличии потомков → доменная ошибка из <c>OrgStructureService</c>.
        /// </summary>
        public int? ParentDepartmentId { get; set; }
        public virtual Department ParentDepartment { get; set; }

        public virtual ICollection<Department> ChildDepartments { get; set; }
            = new HashSet<Department>();

        /// <summary>Руководитель отдела (Phase 11). Опциональная ссылка.</summary>
        public int? HeadEmployeeId { get; set; }
        public virtual Employee HeadEmployee { get; set; }

        public virtual ICollection<NomenclatureCase> NomenclatureCases { get; set; }
            = new HashSet<NomenclatureCase>();
    }
}
