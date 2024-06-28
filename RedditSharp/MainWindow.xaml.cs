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
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using Newtonsoft.Json;
using Reddit;
using Reddit.Controllers;
using Reddit.Models;
using Reddit.Things;
using RedditSharp.Models;
using static System.Net.Mime.MediaTypeNames;
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
            if (!System.IO.Directory.Exists(path))
            {
                System.IO.Directory.CreateDirectory(path);
            }
            reddit = new Redditer();
            reddit.OnImageCountUpdated += ImageCountUpdate;
        }
        private void AdjustTextBoxWidth(TextBox textBox)
        {
            var formattedText = new FormattedText(
                textBox.Text,
                System.Globalization.CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                new Typeface(textBox.FontFamily, textBox.FontStyle, textBox.FontWeight, textBox.FontStretch),
                textBox.FontSize,
                Brushes.Black,
                new NumberSubstitution(),
                1);

            // Adding padding for better appearance
            double padding = textBox.Padding.Left + textBox.Padding.Right + 10;
            textBox.Width = formattedText.Width + padding;
        }

        private void SetGuiItems(List<MyImage> entry)
        {
            imagesPanel.Children.Clear();
            foreach (var item in entry)
            {
                StackPanel entryPanel = new();
                Grid grid = new()
                {
                    Margin = new System.Windows.Thickness(10)
                };
                System.Windows.Controls.Image img = new()
                {
                    Height = 700,
                    Stretch = Stretch.Uniform,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Source = item.BitmapImage,
                };
                grid.Children.Add(img);
                Label label = new()
                {
                    Content = item.Name,
                    Visibility = Visibility.Collapsed
                };
                grid.Children.Add(label);
                Rectangle rect = new()
                {
                    Stroke = Brushes.LightGreen,
                    Width = 316,
                    Height = 701,
                    Fill = Brushes.Transparent,
                    StrokeThickness = 2
                };
                grid.Children.Add(rect);
                entryPanel.Children.Add(grid);
                StackPanel infoPanel = new()
                {
                    Orientation = Orientation.Horizontal,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Margin = new System.Windows.Thickness(10)
                };
                TextBox subredditDisplay = new TextBox()
                {
                    IsReadOnly = true,
                    FontSize = 28,
                    Margin = new System.Windows.Thickness(5),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    TextAlignment = TextAlignment.Center,
                    Text = item.SubredditName
                };
                AdjustTextBoxWidth(subredditDisplay);
                infoPanel.Children.Add(subredditDisplay);
                TextBox upvotesDisplay = new TextBox()
                {
                    IsReadOnly = true,
                    FontSize = 28,
                    Margin = new System.Windows.Thickness(5),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    TextAlignment = TextAlignment.Center,
                    Text = item.Upvotes.ToString()
                };
                AdjustTextBoxWidth(upvotesDisplay);
                infoPanel.Children.Add(upvotesDisplay);
                TextBox dimensionsDisplay = new TextBox()
                {
                    IsReadOnly = true,
                    FontSize = 28,
                    Margin = new System.Windows.Thickness(5),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    TextAlignment = TextAlignment.Center,
                };
                dimensionsDisplay.Text = item.Width.ToString() + "x" + item.Height.ToString();
                AdjustTextBoxWidth(dimensionsDisplay);
                infoPanel.Children.Add(dimensionsDisplay);
                //< Button x: Name = "Download" Content = "Tego chce" Width = "140" Height = "40" Margin = "10" Click = "Download_Click" FontSize = "28" HorizontalAlignment = "Center" VerticalAlignment = "Center" />
                Button download = new()
                {
                    Width = 140,
                    Height = 40,
                    FontSize = 28,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    Content = "Tego chce"
                };
                download.Click += Download_Click;
                infoPanel.Children.Add(download);

                entryPanel.Children.Add(infoPanel);
                imagesPanel.Children.Add(entryPanel);
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
        private Object GetParents(Object element, int parentLevel)
        {
            if (parentLevel == 0)
            {
                return element;
            }
            if (element is FrameworkElement)
            {
                if (((FrameworkElement)element).Parent != null)
                {
                    return GetParents(((FrameworkElement)element).Parent, parentLevel - 1);
                }
            }
            return element;
        }

        private void Download_Click(object sender, RoutedEventArgs e)
        {
            Object parent = GetParents(sender, 2);
            //if (parent is StackPanel imgPanel)
            {
                if (parent is StackPanel entryPanel)
                {
                    if (entryPanel.Children[0] is Grid grid)
                    {
                        if (grid.Children[0] is System.Windows.Controls.Image img)
                        {
                            if (grid.Children[1] is Label lbl)
                            {
                                BitmapEncoder encoder = new PngBitmapEncoder();
                                encoder.Frames.Add(BitmapFrame.Create((BitmapSource)img.Source));
                                string path = System.IO.Path.Combine(Directory.GetCurrentDirectory(), DOWNLOAD_DIRECTORY, lbl.Content.ToString());
                                try
                                {
                                    using (var fileStream = new System.IO.FileStream(path, System.IO.FileMode.CreateNew))
                                    {
                                        encoder.Save(fileStream);
                                    }
                                }
                                catch (Exception ex)
                                {
                                    Debug.WriteLine("Image already downloaded " + ex.Message);
                                }
                            }
                        }
                    }
                }
            }
        }
        private void ImageCountUpdate(int count)
        {
            Dispatcher.Invoke(new Action(() => { imageCounter.Text = count.ToString(); }));
        }
    }
}