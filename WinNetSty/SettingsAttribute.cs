using System;

namespace WinNetSty {

    [AttributeUsage(AttributeTargets.Field)]
    public class SettingsAttribute : Attribute {
        public SettingsAttribute() { }
    }
    
}
