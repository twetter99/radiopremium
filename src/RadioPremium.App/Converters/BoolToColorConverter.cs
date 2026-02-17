using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace RadioPremium.App.Converters;

public sealed class BoolToColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        var isTrue = value is bool b && b;
        var colorName = parameter as string ?? "SpotifyGreen";

        if (isTrue)
        {
            // Return accent color when true
            return colorName switch
            {
                "SpotifyGreen" => new SolidColorBrush(Color.FromArgb(255, 30, 215, 96)),
                _ => Application.Current.Resources["TextPrimaryBrush"] as SolidColorBrush ?? new SolidColorBrush(Colors.White)
            };
        }

        // Return secondary color when false
        return Application.Current.Resources["TextSecondaryBrush"] as SolidColorBrush ?? new SolidColorBrush(Colors.Gray);
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotImplementedException();
    }
}
