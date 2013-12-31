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
    /// Interaction logic for DeleteWindow.xaml
    /// </summary>
    public partial class DeleteWindow : Window
    {
        public DeleteWindow()
        {
            DataContext = this;
            InitializeComponent();
        }


        #region DeletePhotosets dependency property
        public bool DeletePhotosets
        {
            get { return (bool)GetValue(DeletePhotosetsProperty); }
            set { SetValue(DeletePhotosetsProperty, value); }
        }

        public static readonly DependencyProperty DeletePhotosetsProperty =
            DependencyProperty.Register("DeletePhotosets", typeof(bool), typeof(DeleteWindow), new PropertyMetadata(false));
        #endregion


        #region DeletePhotos dependency property
        public bool DeletePhotos
        {
            get { return (bool)GetValue(DeletePhotosProperty); }
            set { SetValue(DeletePhotosProperty, value); }
        }

        public static readonly DependencyProperty DeletePhotosProperty =
            DependencyProperty.Register("DeletePhotos", typeof(bool), typeof(DeleteWindow), new PropertyMetadata(false));
        #endregion

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
            Close();
        }

        private void Button_Click_1(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

    }
}
