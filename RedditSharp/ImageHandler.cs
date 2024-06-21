using RedditSharp.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Security.Policy;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;

namespace RedditSharp
{
    internal class ImageHandler
    {
        private readonly List<MyImage> images;
        private readonly List<ImageEntry> entries;
        private int index;
        public delegate void ImageCallback(int count);
        public event ImageCallback ImageLoaded;
        public async void AddImagesFromURL(string url, string name, string subredditName, int upvotes)
        {
            AddNewImage(url, name, subredditName, upvotes);
        }

        public async void AddNewImage(string url, string name, string subredditName, int upvotes)
        {
            entries.Add(new ImageEntry(url, upvotes, subredditName, 0, 0, name));
            if (index < -1)
            {
                ImageLoaded?.Invoke(entries.Count - index - 1);
            }
            else
            {
                ImageLoaded?.Invoke(entries.Count);
            }
        }
        public MyImage GetNextImage()
        {
            if (index >= images.Count - 1)
            {
                return null;
            }
            if (index > 5)
            {
                images.RemoveAt(0);
                index--;
            }
            return images[++index];
        }
        public MyImage GetPrevImage()
        {
            if (index >= 1)
            {
                return images[--index];
            }
            return null;
        }

        private async Task Worker()
        {
            int downloadIndex = 0;
            while(true)
            {
                if (images.Count < 20 && entries.Count > (index == -1 ? 0 : index))
                {
                    var currentEntry = entries[downloadIndex++];
                    BitmapImage bitmap = await ImageLoader.LoadImageAsync(currentEntry.Url);
                    if (bitmap != null)
                    {
                        images.Add(new MyImage(currentEntry.Url, currentEntry.Upvotes, currentEntry.SubredditName, currentEntry.Width, currentEntry.Height, currentEntry.Name, bitmap));
                    }
                }
                if (index > 5)
                {
                    images.RemoveAt(0);
                    index--;
                }
            }
        }

        public ImageHandler()
        {
            entries = new List<ImageEntry>();
            images = new List<MyImage>();
            index = -1;
            try
            {
                Task tt = new Task(async () =>
                {
                    await Worker();
                });
                tt.Start();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"An error occured: {ex.Message}");
            }
        }
    }
}
