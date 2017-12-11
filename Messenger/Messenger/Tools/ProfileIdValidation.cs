using Mikodev.Network;
using System.Globalization;
using System.Windows.Controls;

namespace Messenger.Tools
{
    internal class ProfileIdValidation : ValidationRule
    {
        public override ValidationResult Validate(object value, CultureInfo cultureInfo)
        {
            var str = value as string;
            if (string.IsNullOrEmpty(str))
                return new ValidationResult(false, "输入为空");
            if (int.TryParse(str, out var val))
                if (val > Links.Id)
                    return new ValidationResult(true, string.Empty);
                else
                    return new ValidationResult(false, "用户编号应大于零");
            return new ValidationResult(false, "输入无效");
        }
    }
}
