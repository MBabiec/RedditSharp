using CoenM.ImageHash.HashAlgorithms;
using RedditSharp.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Security.Policy;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;
using System.Drawing;
using CoenM.ImageHash;
using System.Windows;

namespace RedditSharp
{
    internal class ImageHandler
    {
        // Const parameters
        private const int BACK_IMAGES_THRESHOLD = 7;
        private const int NEXT_IMAGES_THRESHOLD = 35;
        private const double IMAGE_SIMILARITY_THRESHOLD = 90;
        // Image lists
        private readonly List<List<MyImage>> images = [];
        private readonly List<ImageEntry> entries = [];
        private readonly List<ulong> hashes = [];
        // Internal
        private int index = -1;
        public delegate void ImageCallback(int count);
        public event ImageCallback? ImageLoaded;
        private bool finished = false;
        public void AddImagesFromURL(string url, string id, string subredditName, int upvotes)
        {
            AddNewImage(url, id, subredditName, upvotes);
        }

        private void AddNewImage(string url, string id, string subredditName, int upvotes)
        {
            entries.Add(new ImageEntry(url, upvotes, subredditName, 0, 0, id));
        }
        public List<MyImage>? GetNextImage()
        {
            if (index >= images.Count - 1)
            {
                return null;
            }
            ImageLoaded?.Invoke(images.Count - index - 1);
            List<MyImage> item = images[++index];
            if (index > BACK_IMAGES_THRESHOLD)
            {
                images.RemoveAt(0);
                index--;
            }
            return item;
        }
        public List<MyImage>? GetPrevImage()
        {
            if (index >= 1)
            {
                ImageLoaded?.Invoke(images.Count - index + 1);
                return images[--index];
            }
            return null;
        }
        private int IsHashAlreadyPresent(ulong newHash, string url)
        {
            for (int i = 0; i < hashes.Count; ++i)
            {
                double simil = CompareHash.Similarity(newHash, hashes[i]);
                if (simil > IMAGE_SIMILARITY_THRESHOLD)
                {
                    return i;
                }
            }
            return -1;
        }
        private int IsMatchingImageStillPresent(int id)
        {
            for (int i = 0; i < images.Count; ++i)
            {
                if (images[i][0].EntryId == id)
                {
                    return i;
                }
            }
            return -1;
        }
        private async Task Worker()
        {
            int downloadIndex = 0;
            while (true)
            {
                if (images.Count < NEXT_IMAGES_THRESHOLD && entries.Count > (index == -1 ? 0 : index))
                {
                    try
                    {
                        var currentEntry = entries[downloadIndex++];
                        (BitmapImage? bitmap, byte[]? bytes, string? extension) = await ImageLoader.LoadImageAsync(currentEntry.Url);
                        string actualName = currentEntry.SubredditName + "_" + currentEntry.Id + "." + extension;
                        if (bitmap != null && bytes != null)
                        {
                            PerceptualHash pHash = new();
                            var tmp = SixLabors.ImageSharp.Image.Load<SixLabors.ImageSharp.PixelFormats.Rgba32>(bytes);
                            ulong imgHash = pHash.Hash(tmp);

                            int isImageSimilar = IsHashAlreadyPresent(imgHash, currentEntry.Url);
                            hashes.Add(imgHash);
                            if (isImageSimilar == -1)
                            {
                                List<MyImage> item = [];
                                item.Add(new MyImage(currentEntry.Url, currentEntry.Upvotes, currentEntry.SubredditName, bitmap.PixelWidth, bitmap.PixelHeight, currentEntry.Id, bitmap, imgHash, downloadIndex - 1, actualName));
                                images.Add(item);
                                ImageLoaded?.Invoke(images.Count - index);
                            }
                            else
                            {
                                int isPresent = IsMatchingImageStillPresent(isImageSimilar);
                                if (isPresent != -1)
                                {
                                    images[isPresent].Add(new MyImage(currentEntry.Url, currentEntry.Upvotes, currentEntry.SubredditName, bitmap.PixelWidth, bitmap.PixelHeight, currentEntry.Id, bitmap, imgHash, isPresent, actualName));
                                    ImageLoaded?.Invoke(images.Count - index); //Technically not necessary
                                    Debug.WriteLine($"Found similar image lol {isPresent} {currentEntry.Url}");
                                }
                                else
                                {
                                    Debug.WriteLine($"Matching image no longer present {isImageSimilar} {currentEntry.Url}");
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        if (finished && entries.Count < downloadIndex)
                        {
                            MessageBox.Show("Finished");
                            return;
                        }
                        else
                        {
                            Debug.WriteLine($"Unhandled exception {ex.Message}");
                        }
                    }
                }
            }
        }

        public void NoMorePictures()
        {
            finished = true;
        }

        public ImageHandler()
        {
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
