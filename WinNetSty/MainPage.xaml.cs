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
using System.Diagnostics;

// The Blank Page item template is documented at http://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x409

namespace WinNetSty {
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : Page {
        public MainPage() {
            this.InitializeComponent();
            this.SizeChanged += UpdateBindableWidth;
        }

        private Frame SplitContent { get { return (Frame) ShellSplitView.Content; } }

        private void VisualStateChanged(object sender, VisualStateChangedEventArgs e) {
            Debug.WriteLine("resized!");
        }

        public static DependencyProperty BindableWidthProperty = DependencyProperty.Register(
            "BindableWidth",
            typeof(Double),
            typeof(MainPage),
            new PropertyMetadata(500D)
        );

        public double BindableWidth {
            get { return (double)GetValue(BindableWidthProperty); }
            set { SetValue(BindableWidthProperty, value); }
        }

        private void UpdateBindableWidth(object sender, SizeChangedEventArgs e) {
            if (this.AdaptiveStates.CurrentState == this.MinimalState) {
                BindableWidth = e.NewSize.Width;
            } else {
                BindableWidth = 400;
            }
        }

        private void SettingsButtonClicked(object sender, RoutedEventArgs e) {
            ShellSplitView.IsPaneOpen = true;
        }
        private void CloseSettingsButtonClicked(object sender, RoutedEventArgs e) {
            ShellSplitView.IsPaneOpen = false;
        }

        private void PaneClosing(SplitView sender, SplitViewPaneClosingEventArgs args) {
            this.SettingsPage.SaveSettings();
        }
    }
}
