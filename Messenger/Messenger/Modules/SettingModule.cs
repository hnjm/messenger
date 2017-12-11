using Messenger.Models;

namespace Messenger.Modules
{
    /// <summary>
    /// 管理用户界面设置
    /// </summary>
    internal class SettingModule
    {
        private const string _KeyCtrlEnter = "hotkey-control-enter";

        private bool _ctrlenter = false;

        private static readonly SettingModule s_ins = new SettingModule();

        /// <summary>
        /// 使用 ctrl + enter 发送消息还是 enter
        /// </summary>
        public static bool UseCtrlEnter { get => s_ins._ctrlenter; set => s_ins._ctrlenter = value; }

        [Loader(8, LoaderFlags.OnLoad)]
        public static void Load()
        {
            var str = OptionModule.GetOption(_KeyCtrlEnter);
            if (str != null && bool.TryParse(str, out var res))
                s_ins._ctrlenter = res;
            return;
        }

        [Loader(16, LoaderFlags.OnExit)]
        public static void Save()
        {
            OptionModule.SetOption(_KeyCtrlEnter, s_ins._ctrlenter.ToString());
        }
    }
}
