using Messenger.Models;

namespace Messenger.Modules
{
    internal class Settings
    {
        public const string KeyCtrlEnter = "setting-ctrlenter";

        private bool _ctrlenter = false;

        private static Settings s_ins = new Settings();

        public static bool UseCtrlEnter { get => s_ins._ctrlenter; set => s_ins._ctrlenter = value; }

        [AutoLoad(8, AutoLoadFlag.OnLoad)]
        public static void Load()
        {
            var str = Options.GetOption(KeyCtrlEnter);
            if (str != null && bool.TryParse(str, out var res))
                s_ins._ctrlenter = res;
            return;
        }

        [AutoLoad(16, AutoLoadFlag.OnExit)]
        public static void Save()
        {
            Options.SetOption(KeyCtrlEnter, s_ins._ctrlenter.ToString());
        }
    }
}
