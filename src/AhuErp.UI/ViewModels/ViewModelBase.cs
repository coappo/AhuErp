using CommunityToolkit.Mvvm.ComponentModel;

namespace AhuErp.UI.ViewModels
{
    /// <summary>
    /// Базовый класс ViewModel — единая точка расширения для общей логики
    /// (IoC, журналирование, кэш и т. п.).
    /// </summary>
    public abstract class ViewModelBase : ObservableObject
    {
    }
}
