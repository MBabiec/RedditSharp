using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Ink;
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
using static System.Net.WebRequestMethods;

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
        private void SetGuiItems(List<MyImage> entry)
        {
            imagesPanel.Children.Clear();
            foreach (var item in entry)
            {
                Grid grid = new Grid();
                grid.Margin = new System.Windows.Thickness(10);
                Image img = new Image();
                img.Height = 700;
                img.Stretch = Stretch.Uniform;
                img.HorizontalAlignment = HorizontalAlignment.Center;
                img.Source = item.BitmapImage;
                grid.Children.Add(img);
                Rectangle rect = new Rectangle();
                rect.Stroke = Brushes.LightGreen;
                rect.Width = 316;
                rect.Height = 701;
                rect.Fill = Brushes.Transparent;
                rect.StrokeThickness = 2;
                grid.Children.Add(rect);
                imagesPanel.Children.Add(grid);

                imageName = item.Name;
                subredditDisplay.Text = item.SubredditName;
                upvotesDisplay.Text = item.Upvotes.ToString();
                dimensionsDisplay.Text = item.Width.ToString() + "x" + item.Height.ToString();
            }
        }
        private void Next_Click(object sender, RoutedEventArgs e)
        {
            List<MyImage>? imageBatch = reddit.GetNextImage();
            if (imageBatch != null)
            {
                SetGuiItems(imageBatch);
            }
        }
        private void Back_Click(object sender, RoutedEventArgs e)
        {
            List<MyImage>? imageBatch = reddit.GetPrevImage();
            if (imageBatch != null)
            {
                SetGuiItems(imageBatch);
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