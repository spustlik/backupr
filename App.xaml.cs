using Backupr.Properties;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using System.Windows;

namespace Backupr
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            ServicePointManager.DefaultConnectionLimit = 1000;
                //FindServicePoint(new Uri(service)).ConnectionLimit = 50;
            if (Settings.Default.OAuthToken == null)
            {
                var dlg = new AuthWindow();
                if (dlg.ShowDialog() != true)
                {
                    this.Shutdown();
                    return;
                }
                MainWindow = new MainWindow();
            }
            this.DispatcherUnhandledException += App_DispatcherUnhandledException;
            base.OnStartup(e);
        }

        void App_DispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
        {
            e.Handled = true;
            if (MainWindow is MainWindow)
            {
                ((MainWindow)MainWindow).AddError(e);
            }
            if (MessageBox.Show(e.Exception.Message+"\nDo you want to continue?", "Error", MessageBoxButton.YesNo, MessageBoxImage.Stop)!=MessageBoxResult.Yes)
            {
                Shutdown();
            }
        }
    }
}
