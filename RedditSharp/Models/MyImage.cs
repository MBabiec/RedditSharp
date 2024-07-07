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
        public ulong Hash { get; set; }
        public int EntryId { get; set; }
        public string Name { get; set; }
        public MyImage(string url, int upvotes, string subredditName, int width, int height, string id, BitmapImage bitmap, ulong hash, int entryId, string name) : base(url, upvotes, subredditName, width, height, id)
        {
            this.BitmapImage = bitmap;
            this.Hash = hash;
            this.EntryId = entryId;
            this.Name = name;
        }
    }
}
