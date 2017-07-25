using Messenger.Foundation;
using System.Globalization;
using System.Net;
using System.Windows.Controls;

namespace Messenger.Tools
{
    class CodeValidation : ValidationRule
    {
        public override ValidationResult Validate(object value, CultureInfo cultureInfo)
        {
            var str = value as string;
            if (string.IsNullOrEmpty(str))
                return new ValidationResult(false, "输入为空");
            if (int.TryParse(str, out var val))
                if (val > Server.ID)
                    return new ValidationResult(true, string.Empty);
                else
                    return new ValidationResult(false, "用户编号应大于零");
            return new ValidationResult(false, "输入无效");
        }
    }
}
