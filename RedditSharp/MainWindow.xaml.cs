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
        // Review mode: images from the downloads folder, whole queue kept in memory.
        private bool isReviewMode;
        private readonly List<MyImage> reviewItems = new();
        private int reviewIndex = -1;
        // Last reddit batch, restored when leaving review mode.
        private List<MyImage>? lastBrowseBatch;

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
            else if (e.Key == Key.R)
            {
                if (ReviewModeToggle != null)
                {
                    ReviewModeToggle.IsChecked = !ReviewModeToggle.IsChecked;
                }
                e.Handled = true;
            }
        }

        private void ReviewModeToggle_Changed(object sender, RoutedEventArgs e)
        {
            bool checkedNow;
            if (sender is System.Windows.Controls.Primitives.ToggleButton toggle)
            {
                checkedNow = toggle.IsChecked == true;
            }
            else if (ReviewModeToggle != null)
            {
                checkedNow = ReviewModeToggle.IsChecked == true;
            }
            else
            {
                return;
            }
            // During InitializeComponent the toggle fires before imagesPanel exists.
            if (imagesPanel == null)
            {
                return;
            }
            if (checkedNow)
            {
                EnterReviewMode();
            }
            else
            {
                ExitReviewMode();
            }
        }

        private void EnterReviewMode()
        {
            isReviewMode = true;
            LoadReviewQueue();
            if (reviewItems.Count == 0)
            {
                reviewIndex = -1;
                ShowPlaceholder("No saved images",
                    "Your downloads folder is empty. Browse reddit and hit ♥ Save first.");
                UpdateReviewCounter();
                return;
            }
            reviewIndex = 0;
            SetGuiItems(new List<MyImage> { reviewItems[reviewIndex] });
            UpdateReviewCounter();
        }

        private void ExitReviewMode()
        {
            isReviewMode = false;
            reviewItems.Clear();
            reviewIndex = -1;
            if (lastBrowseBatch != null)
            {
                SetGuiItems(lastBrowseBatch);
                return;
            }
            ShowPlaceholder("Browse mode",
                "Use Next / Back below — or the ← → arrow keys — to browse reddit.");
        }

        /// <summary>
        /// Loads every decodable file from the downloads folder into memory.
        /// Nothing is ever evicted: the whole queue stays available for
        /// Next / Back navigation at all times.
        /// </summary>
        private void LoadReviewQueue()
        {
            reviewItems.Clear();
            string folder = System.IO.Path.Combine(Directory.GetCurrentDirectory(), DOWNLOAD_DIRECTORY);
            if (!Directory.Exists(folder))
            {
                return;
            }
            string[] files = Directory.GetFiles(folder);
            Array.Sort(files, StringComparer.OrdinalIgnoreCase);
            int position = 0;
            foreach (string file in files)
            {
                try
                {
                    byte[] bytes = File.ReadAllBytes(file);
                    var bitmap = new BitmapImage();
                    using (var ms = new MemoryStream(bytes))
                    {
                        bitmap.BeginInit();
                        bitmap.CacheOption = BitmapCacheOption.OnLoad;
                        bitmap.StreamSource = ms;
                        bitmap.EndInit();
                        bitmap.Freeze();
                    }
                    string fileName = System.IO.Path.GetFileName(file);
                    reviewItems.Add(new MyImage(
                        file, 0, ParseSubreddit(fileName),
                        bitmap.PixelWidth, bitmap.PixelHeight, fileName,
                        bitmap, 0, position++, fileName));
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Skipping undecodable file {file}: {ex.Message}");
                }
            }
        }

        private static string ParseSubreddit(string fileName)
        {
            // Saved files are named {Subreddit}_{Id}.{ext}; the id never
            // contains '_', so split on the last one (subreddits may).
            string stem = System.IO.Path.GetFileNameWithoutExtension(fileName);
            int sep = stem.LastIndexOf('_');
            if (sep > 0)
            {
                return stem[..sep];
            }
            return string.IsNullOrWhiteSpace(stem) ? "Saved" : stem;
        }

        private static string FormatFileSize(long bytes)
        {
            if (bytes < 1024)
            {
                return $"{bytes} B";
            }
            if (bytes < 1024 * 1024)
            {
                return $"{bytes / 1024.0:0.#} KB";
            }
            return $"{bytes / (1024.0 * 1024.0):0.#} MB";
        }

        private void UpdateReviewCounter()
        {
            if (imageCounter == null)
            {
                return;
            }
            imageCounter.Text = reviewItems.Count == 0 || reviewIndex < 0
                ? $"0 / {reviewItems.Count}"
                : $"{reviewIndex + 1} / {reviewItems.Count}";
        }

        private void ShowPlaceholder(string title, string subtitle)
        {
            var cardBg = (Brush)FindResource("CardBgBrush");
            var cardBorder = (Brush)FindResource("CardBorderBrush");
            var textPrimary = (Brush)FindResource("TextPrimaryBrush");
            var textSecondary = (Brush)FindResource("TextSecondaryBrush");
            var accent = (Brush)FindResource("AccentBrush");
            var accentSoft = (Brush)FindResource("AccentSoftBrush");

            imagesPanel.Children.Clear();
            var card = new Border
            {
                Background = cardBg,
                BorderBrush = cardBorder,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(12),
                Padding = new Thickness(48, 40, 48, 40),
                Margin = new Thickness(0, 0, 12, 0),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Effect = new DropShadowEffect
                {
                    Color = Colors.Black,
                    Opacity = 0.45,
                    BlurRadius = 24,
                    ShadowDepth = 0
                }
            };
            var stack = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center, MaxWidth = 420 };
            var icon = new Border
            {
                Width = 52,
                Height = 52,
                CornerRadius = new CornerRadius(16),
                Background = accentSoft,
                HorizontalAlignment = HorizontalAlignment.Center
            };
            icon.Child = new TextBlock
            {
                Text = "◐",
                FontSize = 24,
                Foreground = accent,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            stack.Children.Add(icon);
            stack.Children.Add(new TextBlock
            {
                Text = title,
                FontSize = 20,
                FontWeight = FontWeights.SemiBold,
                TextAlignment = TextAlignment.Center,
                Foreground = textPrimary,
                Margin = new Thickness(0, 16, 0, 0)
            });
            stack.Children.Add(new TextBlock
            {
                Text = subtitle,
                FontSize = 13,
                TextAlignment = TextAlignment.Center,
                TextWrapping = TextWrapping.Wrap,
                Foreground = textSecondary,
                Margin = new Thickness(0, 8, 0, 0)
            });
            card.Child = stack;
            imagesPanel.Children.Add(card);
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
                if (isReviewMode)
                {
                    // No upvote data for saved files — show file size instead.
                    string size = "?";
                    try
                    {
                        if (File.Exists(item.Url))
                        {
                            size = FormatFileSize(new FileInfo(item.Url).Length);
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Could not read file size for {item.Url}: {ex.Message}");
                    }
                    votesText.Inlines.Add(new System.Windows.Documents.Run("⧉ ") { Foreground = accent });
                    votesText.Inlines.Add(new System.Windows.Documents.Run(size) { Foreground = textPrimary });
                }
                else
                {
                    votesText.Inlines.Add(new System.Windows.Documents.Run("▲ ") { Foreground = accent });
                    votesText.Inlines.Add(new System.Windows.Documents.Run(item.Upvotes.ToString()) { Foreground = textPrimary });
                }
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

                // Action pinned to the bottom of the side panel:
                // delete (with confirmation) in review mode, save in browse mode.
                var action = new Button
                {
                    Height = 40,
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    Margin = new Thickness(0, 0, 0, 0)
                };
                if (isReviewMode)
                {
                    action.Style = (Style)FindResource("DangerButton");
                    action.Content = "🗑  Delete";
                    action.Tag = item.Url;
                    action.ToolTip = "Delete this image from the downloads folder";
                    action.Click += Delete_Click;
                }
                else
                {
                    action.Style = primaryStyle;
                    action.Content = "♥  Save";
                    action.Tag = item.Name;
                    action.ToolTip = "Save this image to the downloads folder";
                    action.Click += Download_Click;
                }
                Grid.SetRow(action, 5);
                side.Children.Add(action);

                card.Child = body;
                imagesPanel.Children.Add(card);
            }
        }

        private void Next_Click(object sender, RoutedEventArgs e)
        {
            if (isReviewMode)
            {
                if (reviewItems.Count == 0)
                {
                    return;
                }
                if (reviewIndex < reviewItems.Count - 1)
                {
                    reviewIndex++;
                    SetGuiItems(new List<MyImage> { reviewItems[reviewIndex] });
                    UpdateReviewCounter();
                }
                return;
            }
            List<MyImage>? imageBatch = reddit.GetNextImage();
            if (imageBatch != null)
            {
                lastBrowseBatch = imageBatch;
                SetGuiItems(imageBatch);
            }
        }

        private void Back_Click(object sender, RoutedEventArgs e)
        {
            if (isReviewMode)
            {
                if (reviewItems.Count == 0)
                {
                    return;
                }
                if (reviewIndex > 0)
                {
                    reviewIndex--;
                    SetGuiItems(new List<MyImage> { reviewItems[reviewIndex] });
                    UpdateReviewCounter();
                }
                return;
            }
            List<MyImage>? imageBatch = reddit.GetPrevImage();
            if (imageBatch != null)
            {
                lastBrowseBatch = imageBatch;
                SetGuiItems(imageBatch);
            }
        }

        private void Delete_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button { Tag: string path } || string.IsNullOrWhiteSpace(path))
            {
                return;
            }
            string fileName = System.IO.Path.GetFileName(path);
            MessageBoxResult answer = MessageBox.Show(
                $"Delete '{fileName}' from the downloads folder permanently?",
                "Delete image",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);
            if (answer != MessageBoxResult.Yes)
            {
                return;
            }
            try
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to delete {path}: {ex.Message}");
                MessageBox.Show($"Could not delete '{fileName}': {ex.Message}", "Delete failed",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
            int removedAt = reviewItems.FindIndex(item =>
                string.Equals(item.Url, path, StringComparison.OrdinalIgnoreCase));
            if (removedAt >= 0)
            {
                reviewItems.RemoveAt(removedAt);
                for (int i = 0; i < reviewItems.Count; i++)
                {
                    reviewItems[i].EntryId = i;
                }
            }
            if (reviewItems.Count == 0)
            {
                reviewIndex = -1;
                ShowPlaceholder("No saved images",
                    "Your downloads folder is empty. Browse reddit and hit ♥ Save first.");
            }
            else
            {
                reviewIndex = Math.Min(reviewIndex, reviewItems.Count - 1);
                SetGuiItems(new List<MyImage> { reviewItems[reviewIndex] });
            }
            UpdateReviewCounter();
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

        private async void UploadToDrive_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button button)
            {
                return;
            }
            string folder = System.IO.Path.Combine(Directory.GetCurrentDirectory(), DOWNLOAD_DIRECTORY);
            if (!Directory.Exists(folder) || Directory.GetFiles(folder).Length == 0)
            {
                MessageBox.Show("Downloads folder is empty — nothing to upload.",
                    "Upload to Drive", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            button.IsEnabled = false;
            string originalContent = button.Content?.ToString() ?? "Upload to Drive";
            var progress = new Progress<string>(fileName => button.Content = $"⇪ {fileName}");
            try
            {
                GoogleDriveUploader.UploadResult result =
                    await GoogleDriveUploader.UploadFolderAsync(folder, progress);
                MessageBox.Show(
                    $"Done: {result.Uploaded} uploaded, {result.Skipped} skipped, {result.Failed} failed.",
                    "Upload to Drive", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Drive upload failed: {ex.Message}");
                MessageBox.Show($"Upload failed: {ex.Message}\n\n" +
                    "Check GoogleDriveUploader.ServiceAccountKeyPath and DriveFolderId.",
                    "Upload to Drive", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                button.Content = originalContent;
                button.IsEnabled = true;
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
                // In review mode the counter shows queue position instead.
                if (isReviewMode || imageCounter == null)
                {
                    return;
                }
                imageCounter.Text = count.ToString();
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
