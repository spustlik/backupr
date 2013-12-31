using FlickrNet;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace Backupr
{
    /// <summary>
    /// Interaction logic for AuthWindow.xaml
    /// </summary>
    public partial class AuthWindow : Window
    {
        private OAuthRequestToken requestToken;

        public AuthWindow()
        {
            InitializeComponent();
            var f = FlickrManager.GetInstance();
            requestToken = f.OAuthGetRequestToken("oob");

            string url = f.OAuthCalculateAuthorizationUrl(requestToken.Token, AuthLevel.Delete);

            System.Diagnostics.Process.Start(url);

        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            if (String.IsNullOrEmpty(authCode.Text))
            {
                MessageBox.Show("You must paste the verifier code into the textbox above.");
                return;
            }
            var f = FlickrManager.GetInstance();
            try
            {
                var accessToken = f.OAuthGetAccessToken(requestToken, authCode.Text);
                FlickrManager.OAuthToken = accessToken;
                DialogResult = true;
                Close();
            }
            catch (FlickrApiException ex)
            {
                MessageBox.Show("Failed to get access token. Error message: " + ex.Message);
            }
        }

        private void Button_Click_1(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
        
    }
}
