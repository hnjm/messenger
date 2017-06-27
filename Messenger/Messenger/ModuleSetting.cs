using Messenger.Foundation;
using System;

namespace Messenger
{
    class ModuleSetting
    {
        public const string KeyCtrlEnter = "setting-ctrlenter";

        private bool ctrlenter = false;

        private static ModuleSetting instance = new ModuleSetting();

        public static bool UseCtrlEnter { get => instance.ctrlenter; set => instance.ctrlenter = value; }

        public static void Load()
        {
            try
            {
                var str = ModuleOption.GetOption(KeyCtrlEnter);
                if (str != null)
                    instance.ctrlenter = bool.Parse(str);
            }
            catch (Exception ex)
            {
                Log.E(nameof(ModuleSetting), ex, "读取配置出错");
            }
        }

        public static void Save()
        {
            ModuleOption.SetOption(KeyCtrlEnter, instance.ctrlenter.ToString());
        }
    }
}
