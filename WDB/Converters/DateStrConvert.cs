using System;
using System.Windows.Data;

namespace WDB.Converters
{
    public class DateStrConvert : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            return value?.ToString().Replace("/","-");
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            return value?.ToString().Replace("-", "/");
        }
    }
}
