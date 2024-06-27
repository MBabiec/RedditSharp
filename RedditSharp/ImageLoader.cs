using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;
using System.Windows;
using System.Net;
using System.Diagnostics;

namespace RedditSharp
{
    internal class ImageLoader
    {
        public static async Task<(BitmapImage?, byte[]?)> LoadImageAsync(string imageUrl)
        {
            try
            {
                byte[]? imageData = await DownloadImageDataAsync(imageUrl);
                if (imageData == null)
                {
                    return (null, null);
                }

                using (MemoryStream ms = new(imageData))
                {
                    BitmapImage bitmap = new();
                    bitmap.BeginInit();
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.StreamSource = ms;
                    bitmap.EndInit();
                    bitmap.Freeze();
                    return (bitmap, imageData);
                }
            }
            catch (NotSupportedException)
            {
                throw new NotSupportedException("Nie znaleziono odpowiedniego składnika przetwarzania obrazu potrzebnego do zakończenia tej operacji.");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"An error occurred while loading the image: {ex.Message} from URL: {imageUrl}");
                return (null, null);
            }
        }

        private static async Task<byte[]?> DownloadImageDataAsync(string imageUrl)
        {
            using (HttpClient client = new())
            {
                client.Timeout = TimeSpan.FromMinutes(10);
                try
                {
                    HttpWebRequest request = (HttpWebRequest)WebRequest.Create(imageUrl);
                    request.Method = "HEAD";
                    HttpWebResponse response = (HttpWebResponse)request.GetResponse();
                    if (response.ContentType.Contains("image"))
                    {
                        return await client.GetByteArrayAsync(imageUrl);
                    }
                    else
                    {
                        return null;
                    }
                }
                catch (WebException ex)
                {
                    Debug.WriteLine($"Failed to download image at Url {imageUrl} {ex.Message}");
                    return null;
                }
            }
        }
    }
}
