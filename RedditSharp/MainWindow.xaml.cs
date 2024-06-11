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
using RedditSharp.Models;

namespace RedditSharp
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private readonly Redditer reddit;
        public MainWindow()
        {
            InitializeComponent();
            reddit = new Redditer();
            reddit.OnImageCountUpdated += ImageCountUpdate;
        }
        private void Next_Click(object sender, RoutedEventArgs e)
        {
            ImageEntry image = reddit.GetNextImage();
            if (image != null)
            {
                currentImage.Source = image.BitmapImage;
            }
        }
        private void Back_Click(object sender, RoutedEventArgs e)
        {

        }
        private void ImageCountUpdate(int count)
        {
            Debug.WriteLine($"New image count {count}");
            Dispatcher.Invoke(new Action(() => {imageCounter.Text = count.ToString();}));
        }
    }
}