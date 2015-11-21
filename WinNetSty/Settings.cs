using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using Windows.Storage;

namespace WinNetSty {
    public class Settings : INotifyPropertyChanged {

        ApplicationDataContainer localSettings;
        private IEnumerable<FieldInfo> settingsMetadata;
        public Settings() {
            localSettings = ApplicationData.Current.LocalSettings;
            settingsMetadata = this.getSettingsMetadata();
            LoadSettings();
        }

        private void LoadSettings() {
            foreach (FieldInfo field in settingsMetadata) {
                dynamic settingsValue = localSettings.Values[field.Name];
                field.SetValue(this, settingsValue);
            }
        }

        private IEnumerable<FieldInfo> getSettingsMetadata() {
            FieldInfo[] fields = typeof(Settings).GetFields(BindingFlags.NonPublic | BindingFlags.Instance);
            return fields.Where((field) => field.GetCustomAttribute<SettingsAttribute>() != null);
        }


        public void SaveSettings() {
            foreach (FieldInfo field in settingsMetadata) {
                localSettings.Values[field.Name] = field.GetValue(this);
            }
        }

        [SettingsAttribute]
        private String remoteHost;
        public String RemoteHost {
            get {
                return this.remoteHost;
            }
            set {
                this.remoteHost = value;
                this.OnPropertyChanged();
                this.OnNetworkChanged();
            }
        }

        private const UInt16 defaultPort = 40118;

        [SettingsAttribute]
        private UInt16? remotePort;
        public UInt16? RemotePort {
            get {
                return this.remotePort ?? defaultPort;
            }
            set {
                this.remotePort = value;
                this.OnPropertyChanged();
                this.OnNetworkChanged();
            }
        }

        [SettingsAttribute]
        private Boolean? enableMouse;
        public Boolean EnableMouse {
            get {
                return this.enableMouse ?? true;
            }
            set {
                this.enableMouse = value;
                this.OnPropertyChanged();
            }
        }

        [SettingsAttribute]
        private Boolean? enableTouch;
        public Boolean EnableTouch {
            get {
                return this.enableTouch ?? true;
            }
            set {
                this.enableTouch = value;
                this.OnPropertyChanged();
            }
        }

        [SettingsAttribute]
        private Boolean? enablePen;
        public Boolean EnablePen {
            get {
                return this.enablePen ?? true;
            }
            set {
                this.enablePen = value;
                this.OnPropertyChanged();
            }
        }

        [SettingsAttribute]
        private double? inkPersistence;
        public double InkPersistence {
            get {
                return this.inkPersistence ?? 0.1;
            }
            set {
                this.inkPersistence = value;
                this.OnPropertyChanged();
            }
        }

        public void OnPropertyChanged([CallerMemberName] String propertyName = null) {
            this.PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
        }

        public event PropertyChangedEventHandler PropertyChanged = delegate { };

        public event NetworkChangedEventHandler NetworkChanged = delegate { };

        public delegate void NetworkChangedEventHandler(Settings sender, EventArgs args);

        private void OnNetworkChanged() {
            NetworkChanged?.Invoke(this, new EventArgs());
        }
    }
}
