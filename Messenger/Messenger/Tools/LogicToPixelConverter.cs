using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace Messenger.Tools
{
    internal class LogicToPixelConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            // 将像素尺寸转换到逻辑尺寸 (用于在高 DPI 环境以像素为单位进行操作)
            if (value is Visual vis && targetType == typeof(Thickness))
            {
                var win = PresentationSource.FromVisual(vis);
                var hor = 1.0 / win.CompositionTarget.TransformToDevice.M11;
                var ver = 1.0 / win.CompositionTarget.TransformToDevice.M22;
                var thi = new Thickness(hor, ver, hor, ver);
                return thi;
            }
            return null;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new InvalidOperationException();
    }
}
