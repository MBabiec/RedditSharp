using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;

namespace RedditSharp.Models
{
    internal class MyImage : ImageEntry
    {
        public BitmapImage BitmapImage { get; set; }
        public ulong hash { get; set; }
        public int entryId { get; set; }
        public MyImage(string url, int upvotes, string subredditName, int width, int height, string name, BitmapImage bitmap, ulong hash, int entryId) : base(url, upvotes, subredditName, width, height, name)
        {
            this.BitmapImage = bitmap;
            this.hash = hash;
            this.entryId = entryId;
        }
    }
}
