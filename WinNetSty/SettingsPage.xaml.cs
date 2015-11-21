using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;
using Windows.Storage;
// The Blank Page item template is documented at http://go.microsoft.com/fwlink/?LinkId=234238

namespace WinNetSty {
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    /// 

    public class NullInt16Converter : IValueConverter {
        public object Convert(object value, Type targetType, object parameter, string language) {
            UInt16 num = (UInt16)value;
            return num.ToString();
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language) {
            String stringNum = (String)value;

            UInt16 num;

            if (!UInt16.TryParse(stringNum, out num)) return (UInt16?)null;

            return num;
        }
    }

    public sealed partial class SettingsPage : UserControl {

        private Settings settings;

        public SettingsPage() {
            this.InitializeComponent();
            settings = WinNetStyApp.Current.Settings;
        }
        
        public void SaveSettings() {
            settings.SaveSettings();
        }

        
    }
    

    
}
