using Microsoft.UI.Xaml.Data;

namespace RadioPremium.App.Converters;

public sealed class BoolToOpacityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        var isTrue = value is bool b && b;
        return isTrue ? 1.0 : 0.3;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotImplementedException();
    }
}
