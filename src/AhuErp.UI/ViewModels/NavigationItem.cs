using System;

namespace AhuErp.UI.ViewModels
{
    /// <summary>
    /// Описывает один пункт навигационного меню главного окна.
    /// </summary>
    public sealed class NavigationItem
    {
        public string Title { get; }

        public ViewModelBase ViewModel { get; }

        public NavigationItem(string title, ViewModelBase viewModel)
        {
            if (string.IsNullOrWhiteSpace(title))
                throw new ArgumentException("Название пункта меню не может быть пустым.", nameof(title));
            Title = title;
            ViewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        }
    }
}
