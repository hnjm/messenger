using Messenger.Extensions;
using System;
using System.Globalization;
using System.Windows.Data;

namespace Messenger.Tools
{
    /// <summary>
    /// 将大小转化为带单位的字符串
    /// </summary>
    internal class LengthConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var len = 0L;
            if (value is long obl)
                len = obl;
            else if (value is double obd)
                len = (long)obd;
            return Converts._GetLength(len);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
    }
}
