using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;

namespace RedditSharp.Models
{
    internal class ImageEntry
    {
        public string url { get; set; }
        public int upvotes { get; set; }
        public string subredditName { get; set; }
        public int width { get; set; }
        public int height { get; set; }
        public BitmapImage BitmapImage { get; set; }
        public ImageEntry(string url, int upvotes, string subredditName, int width, int height, BitmapImage bitmapImage)
        {
            this.url = url;
            this.upvotes = upvotes;
            this.subredditName = subredditName;
            this.width = width;
            this.height = height;
            BitmapImage = bitmapImage;
        }
    }
}
