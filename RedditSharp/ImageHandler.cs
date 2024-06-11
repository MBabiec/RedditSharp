using RedditSharp.Models;
using System;
using System.Collections.Generic;
using System.Linq;
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
        public async void AddNewImage(string url, string subredditName, int upvotes)
        {
            BitmapImage bitmap = await ImageLoader.LoadImageAsync(url);
            if (bitmap != null)
            {
                entries.Add(new ImageEntry(url, upvotes, subredditName, 0, 0, bitmap));
                ImageLoaded?.Invoke(entries.Count);
            }
        }
        public ImageEntry GetNextImage()
        {
            if (index >= entries.Count)
            {
                return null;
            }
            return entries[index++];
        }
        public ImageHandler()
        {
            entries = new List<ImageEntry>();
        }
    }
}
