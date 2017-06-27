using Messenger.Foundation;
using System;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Windows.Data;

namespace Messenger
{
    /// <summary>
    /// 将大小转化为带单位的字符串
    /// </summary>
    class LengthConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var len = 0L;
            if (value is long obl)
                len = obl;
            else if (value is double obd)
                len = (long)obd;
            return Extension.GetLength(len);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
    }

    /// <summary>
    /// 当未读消息过多时 标注 "+" 号
    /// </summary>
    class ProfileHintConverter : IValueConverter
    {
        public int MaxShowValue { get; set; } = 9;

        public string OverflowText { get; set; } = "9+";

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            switch (value)
            {
                case int val when val > MaxShowValue:
                    return OverflowText;
                case int val when val < 0 == false:
                    return val.ToString();
                default:
                    return 0.ToString();
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
    }

    class IsNullOrEmptyConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) => string.IsNullOrEmpty(value?.ToString());

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
    }

    class ImageStringConverter : IValueConverter
    {
        private const int _limit = 3;
        private const int _short = 2;

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var reg = new Regex(@"^[A-Za-z0-9]+$");
            var str = (value == null) ? string.Empty : value.ToString();
            if (str.Length > _limit && _limit > _short && _short > 0)
                str = str.Substring(0, _short);
            if (str.Length > 1 && reg.IsMatch(str) == false)
                str = str.Substring(0, 1);
            return str.ToUpper();
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
    }
}
