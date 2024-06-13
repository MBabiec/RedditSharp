using RedditSharp.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;

namespace RedditSharp
{
    internal class ImageHandler
    {
        private readonly List<ImageEntry> entries;
        private int index;
        public delegate void ImageCallback(int count);
        public event ImageCallback ImageLoaded;
        public async void AddImagesFromURL(string url, string name, string subredditName, int upvotes)
        {
            if (url.Contains("gallery"))
            {
                AddImagesFromGallery(url, name, subredditName, upvotes);
            }
            else
            {
                AddNewImage(url, name, subredditName, upvotes);
            }
        }
        public async void AddImagesFromGallery(string url, string name, string subredditName, int upvotes)
        {
            using (HttpClient client = new())
            {
                var req = await client.GetAsync(url);
            }
        }

        public async void AddNewImage(string url, string name, string subredditName, int upvotes)
        {
            BitmapImage bitmap = await ImageLoader.LoadImageAsync(url);
            if (bitmap != null)
            {
                entries.Add(new ImageEntry(url, upvotes, subredditName, 0, 0, name, bitmap));
                if (index < 2137)
                {
                    ImageLoaded?.Invoke(entries.Count - index - 1);
                }
                else
                {
                    ImageLoaded?.Invoke(entries.Count);
                }
            }
        }
        public ImageEntry GetNextImage()
        {
            if (index == 2137)
            {
                index = 0;
                return entries[index];
            }
            if (index >= entries.Count - 1)
            {
                return null;
            }
            return entries[++index];
        }
        public ImageEntry GetPrevImage()
        {
            if (index - 1 >= 0 && index != 2137)
            {
                return entries[--index];
            }
            return null;
        }
        public ImageHandler()
        {
            entries = new List<ImageEntry>();
            index = 2137;
        }
    }
}
