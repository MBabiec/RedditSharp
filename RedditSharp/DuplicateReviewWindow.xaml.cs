using System;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Media.Imaging;

namespace RedditSharp
{
    /// <summary>
    /// Shows the new local image next to the similar image already on Drive.
    /// DialogResult true = upload anyway, false/null = skip. Drive file is
    /// never modified here.
    /// </summary>
    public partial class DuplicateReviewWindow : Window
    {
        public DuplicateReviewWindow(
            string localFileName,
            byte[] localBytes,
            string driveFileName,
            byte[] driveBytes,
            double similarityPercent)
        {
            InitializeComponent();

            SimilarityText.Text =
                $"“{localFileName}” looks like “{driveFileName}” ({similarityPercent:0.0}% similar).";
            NewNameText.Text = localFileName;
            DriveNameText.Text = driveFileName;

            NewImage.Source = TryDecode(localBytes, "new image");
            DriveImage.Source = TryDecode(driveBytes, "drive image");

            // Safest default: Skip focused, Enter won't accidentally upload.
            SkipButton.Focus();
        }

        private static BitmapImage? TryDecode(byte[] bytes, string what)
        {
            try
            {
                var bitmap = new BitmapImage();
                using (var ms = new MemoryStream(bytes))
                {
                    bitmap.BeginInit();
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.StreamSource = ms;
                    bitmap.EndInit();
                    bitmap.Freeze();
                }
                return bitmap;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Could not decode {what}: {ex.Message}");
                return null;
            }
        }

        private void UploadButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
            Close();
        }

        private void SkipButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
