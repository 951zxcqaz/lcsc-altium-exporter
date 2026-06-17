using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace Transform.App.Converters;

public class StatusToColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var status = value as string;
        return status switch
        {
            "成功" => Brushes.Green,
            "失败" => Brushes.Red,
            "进行中" => Brushes.Blue,
            _ => Brushes.Gray
        };
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
