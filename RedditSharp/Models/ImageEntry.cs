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
        public ImageEntry(string url, int upvotes, string subredditName, int width, int height, string name)
        {
            Url = url;
            Upvotes = upvotes;
            SubredditName = subredditName;
            Name = name;
            Width = width;
            Height = height;
        }

        public string Url { get; set; }
        public int Upvotes { get; set; }
        public string SubredditName { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
        public string Name { get; set; }
    }
}
