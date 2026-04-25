using System.Windows;
using System.Windows.Controls;
using AhuErp.UI.ViewModels;

namespace AhuErp.UI
{
    public partial class LoginWindow : Window
    {
        public LoginWindow(LoginViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
            viewModel.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(LoginViewModel.IsAuthenticated) &&
                    viewModel.IsAuthenticated)
                {
                    DialogResult = true;
                    Close();
                }
            };
            Loaded += (_, __) => FullNameBox.Focus();

            // PasswordBox.Password не DependencyProperty и не уведомляет привязки об
            // изменении — поэтому CommandParameter, забинденный на PasswordBox.Password,
            // навсегда остаётся пустой строкой (значение, прочитанное на момент создания
            // привязки). Принудительно обновляем target-side биндинга на каждое
            // изменение пароля, чтобы LoginCommand получал актуальную строку.
            PasswordBox.PasswordChanged += (_, __) =>
            {
                LoginButton
                    .GetBindingExpression(Button.CommandParameterProperty)
                    ?.UpdateTarget();
            };
        }
    }
}
