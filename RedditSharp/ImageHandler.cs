using CoenM.ImageHash;
using CoenM.ImageHash.HashAlgorithms;
using RedditSharp.Models;
using System.CodeDom;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Media.Imaging;

namespace RedditSharp
{
    internal class ImageHandler
    {
        // Const parameters
        private const int BACK_IMAGES_THRESHOLD = 7;
        private const int NEXT_IMAGES_THRESHOLD = 35;
        private const double IMAGE_SIMILARITY_THRESHOLD = 90;
        private string downPath = "";
        // Image lists
        private readonly List<List<MyImage>> images = [];
        private readonly List<ImageEntry> entries = [];
        private readonly List<MyImage> duplicates = [];
        private readonly List<(ulong hash, int id)> hashes = [];
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
        private int IsHashAlreadyPresent(ulong newHash)
        {
            for (int i = 0; i < hashes.Count; ++i)
            {
                double simil = CompareHash.Similarity(newHash, hashes[i].hash);
                if (simil > IMAGE_SIMILARITY_THRESHOLD)
                {
                    return hashes[i].id;
                }
            }
            return -1;
        }
        private int IsMatchingImageStillPresent(int id)
        {
            for (int i = 0; i < images.Count; ++i)
            {
                for (int j = 0; j < images[i].Count; ++j)
                {
                    if (images[i][j].EntryId == id)
                    {
                        return i;
                    }
                }
            }
            return -1;
        }
        private async Task Worker()
        {
            int downloadIndex = 0;
            while (true)
            {
                if (images.Count < NEXT_IMAGES_THRESHOLD && entries.Count > (index == -1 ? 0 : index) && downloadIndex < entries.Count)
                {
                    try
                    {
                        var currentEntry = entries[downloadIndex++];
                        if (currentEntry == null)
                        {
                            downloadIndex--;
                            continue;
                        }
                        (BitmapImage? bitmap, byte[]? bytes, string? extension) = await ImageLoader.LoadImageAsync(currentEntry.Url);
                        string actualName = currentEntry.SubredditName + "_" + currentEntry.Id + "." + extension;
                        if (bitmap != null && bytes != null)
                        {
                            PerceptualHash pHash = new();
                            var tmp = SixLabors.ImageSharp.Image.Load<SixLabors.ImageSharp.PixelFormats.Rgba32>(bytes);
                            ulong imgHash = pHash.Hash(tmp);

                            int isImageSimilar = IsHashAlreadyPresent(imgHash);
                            if (isImageSimilar == -1)
                            {
                                hashes.Add((imgHash, downloadIndex - 1));
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
                                    duplicates.Add(new MyImage(currentEntry.Url, currentEntry.Upvotes, entries[isImageSimilar].Url, bitmap.PixelWidth, bitmap.PixelHeight, currentEntry.Id, bitmap, imgHash, isPresent, actualName));
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Unhandled exception {ex.Message}");
                    }
                }
                else if (finished && downloadIndex == entries.Count)
                {
                    MessageBox.Show("Finished");
                    string path = System.IO.Path.Combine(Directory.GetCurrentDirectory(), downPath, "dupes.txt");
                    using (StreamWriter writer = new(path))
                    {
                        foreach (var item in duplicates)
                        {
                            writer.WriteLine($"{item.Url} | {item.SubredditName}");
                            Debug.WriteLine($"{item.Url} | {item.SubredditName}");
                        }
                    }
                    return;
                }
            }
        }

        public void NoMorePictures()
        {
            finished = true;
        }

        public ImageHandler(string downloadPath)
        {
            try
            {
                downPath = downloadPath;
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
