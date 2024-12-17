using Newtonsoft.Json;
using Reddit;
using Reddit.Controllers;
using Reddit.Inputs;
using Reddit.Inputs.Listings;
using Reddit.Models;
using Reddit.Things;
using RedditSharp.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;

namespace RedditSharp
{
    internal class Redditer
    {
        private ImageHandler imageHandler;
        public delegate void ImageCountUpdated(int count);
        public event ImageCountUpdated OnImageCountUpdated;
        public Redditer(string downloadPath)
        {
            imageHandler = new(downloadPath);
            imageHandler.ImageLoaded += ImageLoadedCallback;
        }

        private void ImageLoadedCallback(int count)
        {
            OnImageCountUpdated?.Invoke(count);
        }

        private async Task Worker()
        {
            int currentIndex;
            string path = System.IO.Path.Combine(Directory.GetCurrentDirectory(), "tokens.json");
            string json = System.IO.File.ReadAllText(path);
            var tokens = JsonConvert.DeserializeObject<TokenModel>(json);
            RedditClient client = new(tokens.AppID, tokens.RefreshToken);
            LabeledMulti multis = client.Account.Me.Multis()[0];
            DateTime startTime = DateTime.UtcNow;

            // Uncomment to load from disc instead
            //string[] files = Directory.GetFiles("downloads");

            //Console.WriteLine("Files in directory:");
            //foreach (string file in files)
            //{
            //    imageHandler.AddImagesFromURL(file, file, "Dupa", 69);
            //}
            //imageHandler.NoMorePictures();

            //return;

            foreach (var subreddit in multis.Subreddits)
            {
                int outdatedPosts = 0;
                var posts = client.Models.Listings.Hot(new ListingsHotInput(limit: 100), subreddit.Name).Data.Children;
                DateTime lastGoodPost = startTime;
                currentIndex = 0;
                string lastGoodName = "";
                do
                {
                    try
                    {
                        var linkPost = posts[currentIndex++].Data;
                        if (linkPost.IsSelf)
                        {
                            continue;
                        }
                        TimeSpan timePassed = startTime - linkPost.CreatedUTC;
                        if (timePassed.TotalDays >= 7)
                        {
                            outdatedPosts++;
                            if (outdatedPosts >= 10)
                            {
                                Debug.WriteLine($"Finished {subreddit.Name}");
                                break;
                            }
                            else
                            {
                                continue;
                            }
                        }
                        else
                        {
                            outdatedPosts = 0;
                        }
                        if (linkPost.URL.Contains("gallery"))
                        {
                            if (linkPost.MediaMetadata is not null)
                            {
                                foreach (var item in linkPost.MediaMetadata)
                                {
                                    int cnt = 0;
                                    imageHandler.AddImagesFromURL(item.Value.s.u, linkPost.Id + cnt++.ToString(), subreddit.Name, linkPost.Ups);
                                    lastGoodPost = linkPost.CreatedUTC;
                                    lastGoodName = linkPost.Name;
                                }
                            }
                        }
                        else
                        {
                            imageHandler.AddImagesFromURL(linkPost.URL, linkPost.Id, subreddit.Name, linkPost.Ups);
                            lastGoodPost = linkPost.CreatedUTC;
                            lastGoodName = linkPost.Name;
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Run out of images in {subreddit.Name} lastGoodPost {lastGoodPost} index {currentIndex} outdatedPosts {outdatedPosts}");
                        posts = client.Models.Listings.Hot(new ListingsHotInput(after: lastGoodName), subreddit.Name).Data.Children;
                        currentIndex = 0;
                    }
                } while (outdatedPosts < 10);
            }
            Debug.WriteLine("Finished lol");
            imageHandler.NoMorePictures();
        }

        public void Start()
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

        public List<MyImage>? GetNextImage()
        {
            return imageHandler.GetNextImage();
        }
        public List<MyImage>? GetPrevImage()
        {
            return imageHandler.GetPrevImage();
        }
    }
}
