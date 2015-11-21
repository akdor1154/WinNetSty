using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Animation;
using Windows.UI.Xaml.Navigation;

// The User Control item template is documented at http://go.microsoft.com/fwlink/?LinkId=234236

namespace WinNetSty {
    public sealed partial class DrawPage : Page {
        private NetworkController networkController;

        public DrawPage() {
            this.InitializeComponent();
            networkController = new NetworkController();
            networkController.NetworkError += OnNetworkError;
        }

        private async void OnNetworkError(NetworkController sender, NetworkErrorEventArgs e) {
            EnsureTextVisible();
            MessageTextBox.Text = e.Message;
            await Task.Delay(5000);
            TextFadeOut.Begin();
        }

        private void EnsureTextVisible() {
            if (TextFadeIn.GetCurrentState() != ClockState.Stopped) {
                return;
            }
            if (TextFadeOut.GetCurrentState() != ClockState.Stopped) {
                TextFadeOut.Stop();
                TextFadeIn.Begin();
            }
        }

        private void OnInkDown(DrawCanvas sender, InkButtonDownEventArgs e) {
            networkController.SendInkDown(e);
        }
        private void OnInkUp(DrawCanvas sender, InkButtonUpEventArgs e) {
            networkController.SendInkUp(e);
        }
        private void OnInkMove(DrawCanvas sender, InkEventArgs e) {
            networkController.SendInkMove(e);
        }

        
    }
}
