using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using AhuErp.Core.Data;
using AhuErp.Core.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AhuErp.UI.ViewModels
{
    /// <summary>
    /// Phase 11 — раздел «Оргструктура». Дерево отделов, их руководителей и
    /// сотрудников. Для администратора доступно создание/деактивация отделов
    /// и переподчинение.
    /// </summary>
    public partial class OrgStructureViewModel : ViewModelBase
    {
        private readonly AhuDbContext _ctx;

        public ObservableCollection<DepartmentNode> Roots { get; } = new ObservableCollection<DepartmentNode>();

        [ObservableProperty]
        private DepartmentNode selectedNode;

        [ObservableProperty]
        private string errorMessage;

        public OrgStructureViewModel(AhuDbContext ctx)
        {
            _ctx = ctx ?? throw new ArgumentNullException(nameof(ctx));
            Reload();
        }

        [RelayCommand]
        private void Reload()
        {
            Roots.Clear();
            ErrorMessage = null;
            try
            {
                var all = _ctx.Departments.ToList();
                var employees = _ctx.Employees.ToList();
                var byId = all.ToDictionary(d => d.Id);
                var groups = all.GroupBy(d => d.ParentDepartmentId).ToDictionary(g => g.Key ?? 0, g => g.ToList());

                foreach (var root in all.Where(d => d.ParentDepartmentId == null).OrderBy(d => d.Name))
                {
                    Roots.Add(BuildNode(root, employees, groups));
                }
            }
            catch (Exception ex)
            {
                ErrorMessage = ex.Message;
            }
        }

        private static DepartmentNode BuildNode(Department d, List<Employee> employees,
                                                Dictionary<int, List<Department>> groups)
        {
            var node = new DepartmentNode
            {
                Id = d.Id,
                Name = d.Name,
                ShortCode = d.ShortCode,
                IsActive = d.IsActive,
                HeadName = employees.FirstOrDefault(e => e.Id == d.HeadEmployeeId)?.FullName,
                EmployeeCount = employees.Count(e => e.DepartmentId == d.Id && e.IsActive),
            };
            if (groups.TryGetValue(d.Id, out var children))
            {
                foreach (var child in children.OrderBy(c => c.Name))
                {
                    node.Children.Add(BuildNode(child, employees, groups));
                }
            }
            return node;
        }
    }

    /// <summary>Узел дерева оргструктуры (ViewModel-проекция отдела).</summary>
    public class DepartmentNode
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string ShortCode { get; set; }
        public bool IsActive { get; set; }
        public string HeadName { get; set; }
        public int EmployeeCount { get; set; }
        public ObservableCollection<DepartmentNode> Children { get; } = new ObservableCollection<DepartmentNode>();
    }
}
