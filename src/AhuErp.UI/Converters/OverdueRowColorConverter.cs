using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using AhuErp.Core.Models;

namespace AhuErp.UI.Converters
{
    /// <summary>
    /// Возвращает цвет фона строки DataGrid в зависимости от срока исполнения документа:
    /// красный — просрочен, жёлтый — осталось &lt; 3 дней, прозрачный — всё в порядке.
    /// Используется в привязках <c>Background="{Binding Converter=...}"</c>.
    /// </summary>
    [ValueConversion(typeof(Document), typeof(Brush))]
    public sealed class OverdueRowColorConverter : IValueConverter
    {
        public static readonly Brush OverdueBrush = new SolidColorBrush(Color.FromRgb(0xFE, 0xCA, 0xCA));
        public static readonly Brush DueSoonBrush = new SolidColorBrush(Color.FromRgb(0xFE, 0xF3, 0xC7));
        public static readonly Brush NormalBrush = Brushes.Transparent;

        /// <summary>Порог раннего предупреждения в сутках.</summary>
        public int DueSoonDays { get; set; } = 3;

        static OverdueRowColorConverter()
        {
            OverdueBrush.Freeze();
            DueSoonBrush.Freeze();
        }

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (!(value is Document document))
            {
                return NormalBrush;
            }

            var now = DateTime.Now;

            if (document.Status == DocumentStatus.Completed ||
                document.Status == DocumentStatus.Cancelled)
            {
                return NormalBrush;
            }

            if (document.Deadline < now)
            {
                return OverdueBrush;
            }

            if (document.Deadline - now <= TimeSpan.FromDays(DueSoonDays))
            {
                return DueSoonBrush;
            }

            return NormalBrush;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}
