using Avalonia.Controls;
using Avalonia.Data.Converters;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VirtualizedGridDemo.Converters
{
    public class BooleanToDisplayModeConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is null)
            {
                return null;
            }

            if (value is bool boolValue)
            {
                return boolValue ? SplitViewDisplayMode.CompactInline : SplitViewDisplayMode.CompactOverlay;
            }

            throw new ArgumentException();
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
