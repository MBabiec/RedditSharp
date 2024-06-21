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
        private void SetGuiItems(MyImage entry)
        {
            currentImage.Source = entry.BitmapImage;
            subredditDisplay.Text = entry.SubredditName;
            upvotesDisplay.Text = entry.Upvotes.ToString();
        }
        private void Next_Click(object sender, RoutedEventArgs e)
        {
            MyImage image = reddit.GetNextImage();
            if (image != null)
            {
                SetGuiItems(image);
                imageCounter.Text = (int.Parse(imageCounter.Text) - 1).ToString();
            }
        }
        private void Back_Click(object sender, RoutedEventArgs e)
        {
            MyImage image = reddit.GetPrevImage();
            if (image != null)
            {
                SetGuiItems(image);
                imageCounter.Text = (int.Parse(imageCounter.Text) + 1).ToString();
            }
        }
        private void ImageCountUpdate(int count)
        {
            Dispatcher.Invoke(new Action(() => {imageCounter.Text = count.ToString();}));
        }
    }
}