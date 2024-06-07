using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using Newtonsoft.Json;
using Reddit;
using Reddit.Controllers;
using Reddit.Models;
using Reddit.Things;

namespace RedditSharp
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private Redditer reddit;
        public MainWindow()
        {
            InitializeComponent();
            reddit = new Redditer();
        }
        private void Next_Click(object sender, RoutedEventArgs e)
        {
            var image = reddit.GetNextImage();
            if (image != null)
            {
                currentImage.Source = image;
            }
        }
    }
}