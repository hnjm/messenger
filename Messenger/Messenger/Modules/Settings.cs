using Messenger.Models;
using System;
using System.Diagnostics;

namespace Messenger.Modules
{
    internal class Settings
    {
        public const string KeyCtrlEnter = "setting-ctrlenter";

        private bool ctrlenter = false;

        private static Settings instance = new Settings();

        public static bool UseCtrlEnter { get => instance.ctrlenter; set => instance.ctrlenter = value; }

        [AutoLoad(8, AutoLoadFlag.OnLoad)]
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
                Trace.WriteLine(ex);
            }
        }

        [AutoLoad(16, AutoLoadFlag.OnExit)]
        public static void Save()
        {
            Options.SetOption(KeyCtrlEnter, instance.ctrlenter.ToString());
        }
    }
}
