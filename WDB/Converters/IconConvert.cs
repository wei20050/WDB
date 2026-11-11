using System;
using System.Windows.Data;
using WDB.Entity;

namespace WDB.Converters
{
    public class IconConvert : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            if (value == null) return null;
            var l = (LianJie)value;
            if (l.NoDbType == null)
            {
                return $"pack://application:,,,/WDB;component/Resources/connect/{l.DataBaseType.ToString().ToLower()}.png";
            }
            return $"pack://application:,,,/WDB;component/Resources/connect/{l.NoDbType.ToLower()}.png";
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            return value;
        }
    }
}
