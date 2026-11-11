using System;
using System.Windows.Data;

namespace WDB.Converters
{
    public class TableIconConvert : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            if (value == null) return null;
            if ((bool)value)
            {
                return $"pack://application:,,,/WDB;component/Resources/view.png";
            }
            return $"pack://application:,,,/WDB;component/Resources/table.png";
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            return value;
        }
    }
}
