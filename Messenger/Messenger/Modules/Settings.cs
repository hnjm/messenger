using Messenger.Foundation;
using System;

namespace Messenger.Modules
{
    class Settings
    {
        public const string KeyCtrlEnter = "setting-ctrlenter";

        private bool ctrlenter = false;

        private static Settings instance = new Settings();

        public static bool UseCtrlEnter { get => instance.ctrlenter; set => instance.ctrlenter = value; }

        public static void Load()
        {
            try
            {
                var str = Options.GetOption(KeyCtrlEnter);
                if (str != null)
                    instance.ctrlenter = bool.Parse(str);
            }
            catch (Exception ex)
            {
                Log.E(nameof(Settings), ex, "读取配置出错");
            }
        }

        public static void Save()
        {
            Options.SetOption(KeyCtrlEnter, instance.ctrlenter.ToString());
        }
    }
}
