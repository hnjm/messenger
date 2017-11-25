using Messenger.Models;

namespace Messenger.Modules
{
    /// <summary>
    /// 管理用户界面设置
    /// </summary>
    internal class Settings
    {
        private const string _KeyCtrlEnter = "setting-ctrlenter";

        private bool _ctrlenter = false;

        private static Settings s_ins = new Settings();

        /// <summary>
        /// 使用 ctrl + enter 发送消息还是 enter
        /// </summary>
        public static bool UseCtrlEnter { get => s_ins._ctrlenter; set => s_ins._ctrlenter = value; }

        [AutoLoad(8, AutoLoadFlags.OnLoad)]
        public static void Load()
        {
            var str = Options.GetOption(_KeyCtrlEnter);
            if (str != null && bool.TryParse(str, out var res))
                s_ins._ctrlenter = res;
            return;
        }

        [AutoLoad(16, AutoLoadFlags.OnExit)]
        public static void Save()
        {
            Options.SetOption(_KeyCtrlEnter, s_ins._ctrlenter.ToString());
        }
    }
}
