using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
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
        private bool showPhonePreview = true;
        private const double PhoneAspect = 9.0 / 20.0;

        public MainWindow()
        {
            InitializeComponent();
            string path = System.IO.Path.Combine(Directory.GetCurrentDirectory(), DOWNLOAD_DIRECTORY);
            if (!System.IO.Directory.Exists(path))
            {
                System.IO.Directory.CreateDirectory(path);
            }
            reddit = new Redditer(DOWNLOAD_DIRECTORY);
            reddit.OnImageCountUpdated += ImageCountUpdate;

            // Arrow-key navigation for a snappier feel.
            KeyDown += MainWindow_KeyDown;

            string[] files = Directory.GetFiles(DOWNLOAD_DIRECTORY);
            if (files != null && files.Length > 0)
            {
                MessageBoxResult result = MessageBox.Show("Files present in download directory. Would you like to wipe it?", "Wipe downloads", MessageBoxButton.YesNo);

                // Handle the user's response
                switch (result)
                {
                    case MessageBoxResult.Yes:
                        foreach (string file in files)
                        {
                            System.IO.File.Delete(file);
                        }
                        break;
                    case MessageBoxResult.No:
                        break;
                }
            }
            reddit.Start();
        }

        private void MainWindow_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Right)
            {
                Next_Click(sender, e);
                e.Handled = true;
            }
            else if (e.Key == Key.Left)
            {
                Back_Click(sender, e);
                e.Handled = true;
            }
            else if (e.Key == Key.P)
            {
                if (PhonePreviewToggle != null)
                {
                    PhonePreviewToggle.IsChecked = !PhonePreviewToggle.IsChecked;
                }
                e.Handled = true;
            }
        }

        private void PhonePreviewToggle_Changed(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.Primitives.ToggleButton toggle)
            {
                showPhonePreview = toggle.IsChecked == true;
            }
            else if (PhonePreviewToggle != null)
            {
                showPhonePreview = PhonePreviewToggle.IsChecked == true;
            }
            // During InitializeComponent the toggle fires before imagesPanel exists.
            if (imagesPanel == null)
            {
                return;
            }
            ApplyPhonePreviewVisibility();
        }

        private void ApplyPhonePreviewVisibility()
        {
            if (imagesPanel == null)
            {
                return;
            }
            var visibility = showPhonePreview ? Visibility.Visible : Visibility.Collapsed;
            foreach (var child in imagesPanel.Children)
            {
                var overlay = FindPhoneOverlay(child as DependencyObject);
                if (overlay != null)
                {
                    overlay.Visibility = visibility;
                }
            }
        }

        private static Grid? FindPhoneOverlay(DependencyObject? root)
        {
            if (root == null)
            {
                return null;
            }
            int count = VisualTreeHelper.GetChildrenCount(root);
            for (int i = 0; i < count; i++)
            {
                var child = VisualTreeHelper.GetChild(root, i);
                if (child is Grid grid && grid.Tag as string == "PhoneOverlay")
                {
                    return grid;
                }
                var found = FindPhoneOverlay(child);
                if (found != null)
                {
                    return found;
                }
            }
            return null;
        }

        /// <summary>
        /// Builds a modern phone-wallpaper crop preview: a rounded phone frame
        /// (9:20) with dimmed surplus on the sides, a dynamic-island notch
        /// and a small "wallpaper" tag — the sleek successor to the old
        /// flat green rectangle. The phone screen shows a full-bleed
        /// center-crop (UniformToFill) so there are no black bars.
        /// </summary>
        private static Grid BuildPhoneOverlay(Brush success, ImageSource? source)
        {
            var overlay = new Grid
            {
                Tag = "PhoneOverlay",
                IsHitTestVisible = false,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch
            };
            overlay.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            overlay.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            overlay.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var dimBrush = new SolidColorBrush(Color.FromArgb(0xAA, 0x00, 0x00, 0x00));
            var dimLeft = new Border { Background = dimBrush };
            Grid.SetColumn(dimLeft, 0);
            var dimRight = new Border { Background = dimBrush };
            Grid.SetColumn(dimRight, 2);
            overlay.Children.Add(dimLeft);
            overlay.Children.Add(dimRight);

            var phoneFrame = new Border
            {
                BorderBrush = success,
                BorderThickness = new Thickness(2),
                CornerRadius = new CornerRadius(26),
                Background = Brushes.Transparent,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                SnapsToDevicePixels = true,
                Effect = new DropShadowEffect
                {
                    Color = Color.FromRgb(0x22, 0xC5, 0x5E),
                    Opacity = 0.45,
                    BlurRadius = 12,
                    ShadowDepth = 0
                }
            };
            Grid.SetColumn(phoneFrame, 1);

            var inner = new Grid();
            // Full-bleed wallpaper preview: fills the whole phone screen
            // with an explicitly centered crop, so no letterbox bars
            // at top/bottom. ImageBrush (unlike Image) exposes
            // AlignmentX/AlignmentY, guaranteeing dead-center.
            var screenFill = new Border
            {
                Background = new ImageBrush
                {
                    ImageSource = source,
                    Stretch = Stretch.UniformToFill,
                    AlignmentX = AlignmentX.Center,
                    AlignmentY = AlignmentY.Center
                },
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch,
                IsHitTestVisible = false
            };
            RenderOptions.SetBitmapScalingMode(screenFill, BitmapScalingMode.HighQuality);
            inner.Children.Add(screenFill);

            phoneFrame.Child = inner;
            overlay.Children.Add(phoneFrame);

            // Keep the 9:20 aspect fitted to the viewport.
            overlay.SizeChanged += (s, e) =>
            {
                double availW = overlay.ActualWidth;
                double availH = overlay.ActualHeight;
                if (availW <= 0 || availH <= 0)
                {
                    return;
                }
                double h = availH - 4;
                double w = h * PhoneAspect;
                if (w > availW - 4)
                {
                    w = availW - 4;
                    h = w / PhoneAspect;
                }
                phoneFrame.Width = w;
                phoneFrame.Height = h;
                // Clip the fill image to the rounded phone shape
                // (Border alone does not clip children to its corner radius).
                phoneFrame.Clip = new RectangleGeometry(new Rect(0, 0, w, h), 24, 24);
            };

            return overlay;
        }

        private void SetGuiItems(List<MyImage> entry)
        {
            var cardBg = (Brush)FindResource("CardBgBrush");
            var cardBorder = (Brush)FindResource("CardBorderBrush");
            var fieldBg = (Brush)FindResource("FieldBgBrush");
            var accent = (Brush)FindResource("AccentBrush");
            var accentSoft = (Brush)FindResource("AccentSoftBrush");
            var textPrimary = (Brush)FindResource("TextPrimaryBrush");
            var textMuted = (Brush)FindResource("TextMutedBrush");
            var success = (Brush)FindResource("SuccessBrush");
            var primaryStyle = (Style)FindResource("PrimaryButton");

            imagesPanel.Children.Clear();
            foreach (var item in entry)
            {
                var card = new Border
                {
                    Background = cardBg,
                    BorderBrush = cardBorder,
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(12),
                    Padding = new Thickness(5),
                    Margin = new Thickness(0, 0, 12, 0),
                    Width = 720,
                    VerticalAlignment = VerticalAlignment.Center,
                    SnapsToDevicePixels = true,
                    Effect = new DropShadowEffect
                    {
                        Color = Colors.Black,
                        Opacity = 0.45,
                        BlurRadius = 24,
                        ShadowDepth = 0
                    }
                };
                // Subtle hover highlight for a modern feel.
                card.MouseEnter += (s, e) => card.BorderBrush = accent;
                card.MouseLeave += (s, e) => card.BorderBrush = cardBorder;

                // Wide card: the image takes the star-sized column,
                // info + actions live in the fixed side panel.
                var body = new Grid();
                body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(180) });

                // Image viewport with phone-wallpaper crop preview.
                var imageFrame = new Border
                {
                    Height = 720,
                    CornerRadius = new CornerRadius(8),
                    Background = new SolidColorBrush(Color.FromRgb(0x0A, 0x0A, 0x0C)),
                    BorderBrush = cardBorder,
                    BorderThickness = new Thickness(1),
                    ClipToBounds = true,
                    SnapsToDevicePixels = true
                };
                var viewport = new Grid();
                var img = new System.Windows.Controls.Image
                {
                    Stretch = Stretch.Uniform,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(6),
                    Source = item.BitmapImage,
                };
                RenderOptions.SetBitmapScalingMode(img, BitmapScalingMode.HighQuality);
                viewport.Children.Add(img);
                var phoneOverlay = BuildPhoneOverlay(success, item.BitmapImage);
                phoneOverlay.Visibility = showPhonePreview ? Visibility.Visible : Visibility.Collapsed;
                viewport.Children.Add(phoneOverlay);
                imageFrame.Child = viewport;
                Grid.SetColumn(imageFrame, 0);
                body.Children.Add(imageFrame);

                // Side info panel next to the image.
                var side = new Grid
                {
                    Margin = new Thickness(10, 0, 0, 0)
                };
                side.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                side.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                side.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                side.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
                side.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                side.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                Grid.SetColumn(side, 1);
                body.Children.Add(side);

                // Subreddit pill.
                var subPill = new Border
                {
                    Background = accentSoft,
                    CornerRadius = new CornerRadius(14),
                    Padding = new Thickness(10, 5, 12, 5),
                    HorizontalAlignment = HorizontalAlignment.Left,
                    VerticalAlignment = VerticalAlignment.Center
                };
                var subRow = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
                subRow.Children.Add(new Ellipse
                {
                    Width = 7,
                    Height = 7,
                    Fill = accent,
                    VerticalAlignment = VerticalAlignment.Center
                });
                subRow.Children.Add(new TextBlock
                {
                    Text = "r/" + item.SubredditName,
                    FontSize = 12.5,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = textPrimary,
                    Margin = new Thickness(7, 0, 0, 0),
                    VerticalAlignment = VerticalAlignment.Center,
                    TextTrimming = TextTrimming.CharacterEllipsis,
                    MaxWidth = 132
                });
                subPill.Child = subRow;
                Grid.SetRow(subPill, 0);
                side.Children.Add(subPill);

                var dimsText = new TextBlock
                {
                    Text = $"{item.Width} × {item.Height}",
                    FontSize = 12,
                    Foreground = textMuted,
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(2, 8, 0, 0)
                };
                Grid.SetRow(dimsText, 1);
                side.Children.Add(dimsText);

                // Upvotes pill.
                var statsRow = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Margin = new Thickness(0, 10, 0, 0)
                };
                var votesPill = new Border
                {
                    Background = fieldBg,
                    BorderBrush = cardBorder,
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(14),
                    Padding = new Thickness(10, 5, 12, 5)
                };
                var votesText = new TextBlock { FontSize = 13, FontWeight = FontWeights.SemiBold };
                votesText.Inlines.Add(new System.Windows.Documents.Run("▲ ") { Foreground = accent });
                votesText.Inlines.Add(new System.Windows.Documents.Run(item.Upvotes.ToString()) { Foreground = textPrimary });
                votesPill.Child = votesText;
                statsRow.Children.Add(votesPill);

                statsRow.Children.Add(new TextBlock
                {
                    Text = "#" + item.EntryId,
                    FontSize = 12,
                    Foreground = textMuted,
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(10, 0, 0, 0)
                });
                Grid.SetRow(statsRow, 2);
                side.Children.Add(statsRow);

                var cropHint = new TextBlock
                {
                    Text = "Green frame shows the phone wallpaper crop.",
                    FontSize = 11,
                    Foreground = textMuted,
                    TextWrapping = TextWrapping.Wrap,
                    Margin = new Thickness(2, 0, 0, 10)
                };
                Grid.SetRow(cropHint, 4);
                side.Children.Add(cropHint);

                // Save action pinned to the bottom of the side panel.
                var download = new Button
                {
                    Style = primaryStyle,
                    Height = 40,
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    Margin = new Thickness(0, 0, 0, 0),
                    Content = "♥  Save",
                    Tag = item.Name,
                    ToolTip = "Save this image to the downloads folder"
                };
                download.Click += Download_Click;
                Grid.SetRow(download, 5);
                side.Children.Add(download);

                card.Child = body;
                imagesPanel.Children.Add(card);
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
            if (sender is Button { Tag: string name } button && !string.IsNullOrWhiteSpace(name))
            {
                SaveImageByName(button, name);
            }
        }

        private void SaveImageByName(Button source, string fileName)
        {
            // Walk up to the card, then descend to the displayed image
            // (the viewport now also hosts the phone-crop overlay).
            DependencyObject? current = source;
            while (current != null)
            {
                if (current is Border card && card.Child is Grid)
                {
                    var img = FindDescendant<System.Windows.Controls.Image>(card);
                    if (img?.Source is BitmapSource bitmap)
                    {
                        SaveImageSource(bitmap, fileName);
                        return;
                    }
                }
                current = VisualTreeHelper.GetParent(current);
            }
        }

        private static T? FindDescendant<T>(DependencyObject root) where T : DependencyObject
        {
            int count = VisualTreeHelper.GetChildrenCount(root);
            for (int i = 0; i < count; i++)
            {
                var child = VisualTreeHelper.GetChild(root, i);
                if (child is T match)
                {
                    return match;
                }
                var found = FindDescendant<T>(child);
                if (found != null)
                {
                    return found;
                }
            }
            return null;
        }

        private static void SaveImageSource(BitmapSource bitmap, string? fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName))
            {
                return;
            }
            BitmapEncoder encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(bitmap));
            string path = System.IO.Path.Combine(Directory.GetCurrentDirectory(), DOWNLOAD_DIRECTORY, fileName);
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

        private void OpenFolder_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string folder = System.IO.Path.Combine(Directory.GetCurrentDirectory(), DOWNLOAD_DIRECTORY);
                Process.Start("explorer.exe", folder);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"{ex.Message}");
            }
        }

        private void ScrollViewer_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            var scrollViewer = sender as ScrollViewer;
            if (scrollViewer != null)
            {
                if (Keyboard.Modifiers != ModifierKeys.Control)
                {
                    double scrollAmount = 20;
                    scrollViewer.ScrollToHorizontalOffset(scrollViewer.HorizontalOffset - e.Delta / 120 * scrollAmount);
                    e.Handled = true;
                }
            }
        }

        private void ImageCountUpdate(int count)
        {
            Dispatcher.Invoke(new Action(() =>
            {
                if (imageCounter != null)
                {
                    imageCounter.Text = count.ToString();
                }
            }));
        }
    }

    internal static class FluentExtensions
    {
        public static T Also<T>(this T value, Action<T> action)
        {
            action(value);
            return value;
        }
    }
}
