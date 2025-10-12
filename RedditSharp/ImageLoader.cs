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
        public static async Task<(BitmapImage?, byte[]?, string? extension)> LoadImageAsync(string imageUrl)
        {
            if (imageUrl.StartsWith("downloads"))
            {
                BitmapImage bitmap = LoadImage(imageUrl);
                byte[] imageData = BitmapImageToByteArray(bitmap);
                return (bitmap, imageData, System.IO.Path.GetExtension(imageUrl));
            }
            else
            {
                try
                {
                    (byte[]? imageData, string? extension) = await DownloadImageDataAsync(imageUrl);
                    if (imageData == null)
                    {
                        return (null, null, null);
                    }

                    using (MemoryStream ms = new(imageData))
                    {
                        BitmapImage bitmap = new();
                        bitmap.BeginInit();
                        bitmap.CacheOption = BitmapCacheOption.OnLoad;
                        bitmap.StreamSource = ms;
                        bitmap.EndInit();
                        bitmap.Freeze();
                        return (bitmap, imageData, extension);
                    }
                }
                catch (NotSupportedException)
                {
                    throw new NotSupportedException("Nie znaleziono odpowiedniego składnika przetwarzania obrazu potrzebnego do zakończenia tej operacji.");
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"An error occurred while loading the image: {ex.Message} from URL: {imageUrl}");
                    Debug.WriteLine(ex.InnerException?.Message);
                    return (null, null, null);
                }
            }
        }

        private static async Task<(byte[]?, string? extension)> DownloadImageDataAsync(string imageUrl)
        {
            using (HttpClient client = new())
            {
                client.Timeout = TimeSpan.FromMinutes(10);
                try
                {
                    if (imageUrl.Contains("catbox"))
                    {
                        var request = new HttpRequestMessage(HttpMethod.Get, imageUrl);
                        request.Headers.UserAgent.ParseAdd("Dotnet");
                        request.Headers.Host = request.RequestUri?.Host;
                        using HttpResponseMessage response = await client.SendAsync(request);
                        var slashIndex = response.Content.Headers.ContentType!.MediaType!.LastIndexOf('/') + 1;
                        return (await response.Content.ReadAsByteArrayAsync(), response.Content.Headers.ContentType?.MediaType[slashIndex..]);
                    }
                    else
                    {
                        HttpWebRequest request = (HttpWebRequest)WebRequest.Create(imageUrl);
                        request.Method = "HEAD";
                        HttpWebResponse response = (HttpWebResponse)request.GetResponse();
                        if (response.ContentType.Contains("image"))
                        {
                            string dupa = response.ContentType[(response.ContentType.LastIndexOf('/') + 1)..];
                            return (await client.GetByteArrayAsync(imageUrl), response.ContentType[(response.ContentType.LastIndexOf('/') + 1)..]);
                        }
                        else
                        {
                            return (null, null);
                        }
                    }
                }
                catch (WebException ex)
                {
                    Debug.WriteLine($"Failed to download image at Url {imageUrl} {ex.Message}");
                    return (null, null);
                }
            }
        }
        private static BitmapImage LoadImage(string imagePath)
        {
            BitmapImage bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.UriSource = new Uri(imagePath, UriKind.RelativeOrAbsolute);
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.EndInit();
            bitmap.Freeze(); // Optional: Freeze the bitmap for thread safety and performance
            return bitmap;
        }
        private static byte[] BitmapImageToByteArray(BitmapImage bitmapImage)
        {
            using (MemoryStream memoryStream = new MemoryStream())
            {
                BitmapEncoder encoder = new PngBitmapEncoder(); // or any other encoder (JpegBitmapEncoder, etc.)
                encoder.Frames.Add(BitmapFrame.Create(bitmapImage));
                encoder.Save(memoryStream);
                return memoryStream.ToArray();
            }
        }
    }
}
