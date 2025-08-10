using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;

namespace RedditSharp.Models
{
    internal class ImageEntry(string url, int upvotes, string subredditName, int width, int height, string id)
    {
        public string Url { get; set; } = url;
        public int Upvotes { get; set; } = upvotes;
        public string SubredditName { get; set; } = subredditName;
        public int Width { get; set; } = width;
        public int Height { get; set; } = height;
        public string Id { get; set; } = id;
    }
}
