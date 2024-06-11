using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;
using System.Windows;

namespace RedditSharp
{
    internal class ImageLoader
    {
        public static async Task<BitmapImage> LoadImageAsync(string imageUrl)
        {
            try
            {
                byte[] imageData = await DownloadImageDataAsync(imageUrl);

                using (MemoryStream ms = new(imageData))
                {
                    BitmapImage bitmap = new();
                    bitmap.BeginInit();
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.StreamSource = ms;
                    bitmap.EndInit();
                    bitmap.Freeze();
                    return bitmap;
                }
            }
            catch (NotSupportedException)
            {
                throw new NotSupportedException("Nie znaleziono odpowiedniego składnika przetwarzania obrazu potrzebnego do zakończenia tej operacji.");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"An error occurred while loading the image: {ex.Message} from URL: {imageUrl}");
                return null;
            }
        }

        private static async Task<byte[]> DownloadImageDataAsync(string imageUrl)
        {
            using (HttpClient client = new())
            {
                return await client.GetByteArrayAsync(imageUrl);
            }
        }
    }
}
