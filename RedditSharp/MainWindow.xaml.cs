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
        private const string DOWNLOAD_DIRECTORY = "downloads";
        private readonly Redditer reddit;
        private string imageName;
        public MainWindow()
        {
            InitializeComponent();
            string path = System.IO.Path.Combine(Directory.GetCurrentDirectory(), DOWNLOAD_DIRECTORY);
            if (!System.IO.Directory.Exists(path) )
            {
                System.IO.Directory.CreateDirectory(path);
            }
            reddit = new Redditer();
            reddit.OnImageCountUpdated += ImageCountUpdate;
        }
        private void SetGuiItems(MyImage entry)
        {
            imageName = entry.Name;
            currentImage.Source = entry.BitmapImage;
            subredditDisplay.Text = entry.SubredditName;
            upvotesDisplay.Text = entry.Upvotes.ToString();
            dimensionsDisplay.Text = entry.Width.ToString() + "x" + entry.Height.ToString();
        }
        private void Next_Click(object sender, RoutedEventArgs e)
        {
            MyImage? image = reddit.GetNextImage();
            if (image != null)
            {
                SetGuiItems(image);
                imageCounter.Text = (int.Parse(imageCounter.Text) - 1).ToString();
            }
        }
        private void Back_Click(object sender, RoutedEventArgs e)
        {
            MyImage? image = reddit.GetPrevImage();
            if (image != null)
            {
                SetGuiItems(image);
                imageCounter.Text = (int.Parse(imageCounter.Text) + 1).ToString();
            }
        }
        private void Download_Click(object sender, RoutedEventArgs e)
        {
            BitmapEncoder encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create((BitmapSource)currentImage.Source));
            string path = System.IO.Path.Combine(Directory.GetCurrentDirectory(), DOWNLOAD_DIRECTORY, imageName);
            using (var fileStream = new System.IO.FileStream(path, System.IO.FileMode.Create))
            {
                encoder.Save(fileStream);
            }
        }
        private void ImageCountUpdate(int count)
        {
            Dispatcher.Invoke(new Action(() => {imageCounter.Text = count.ToString();}));
        }
    }
}