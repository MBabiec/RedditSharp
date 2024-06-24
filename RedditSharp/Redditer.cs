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
        private static ImageHandler imageHandler;
        public delegate void ImageCountUpdated(int count);
        public event ImageCountUpdated OnImageCountUpdated;
        public Redditer()
        {
            //LinkPost posts = (LinkPost)client.Subreddit(multis.Subreddits[0].Name).About().Posts.Hot[imageCounter];
            //LoadImageAsync(posts.URL);
            //LinkPost posts = (LinkPost)client.Subreddit(multis.Subreddits[0].Name).About().Posts.Hot[++imageCounter];
            //BitmapImage bitmapImage = await ImageLoader.LoadImageAsync(imageUrl);
            imageHandler = new();
            imageHandler.ImageLoaded += ImageLoadedCallback;
            StartWorker();
        }

        private void ImageLoadedCallback(int count)
        {
            OnImageCountUpdated?.Invoke(count);
        }

        private static async Task Worker()
        {
            int currentIndex;
            string path = System.IO.Path.Combine(Directory.GetCurrentDirectory(), "tokens.json");
            string json = System.IO.File.ReadAllText(path);
            var tokens = JsonConvert.DeserializeObject<TokenModel>(json);
            RedditClient client = new(tokens.AppID, tokens.RefreshToken);
            LabeledMulti multis = client.Account.Me.Multis()[0];
            DateTime startTime = DateTime.UtcNow;

            foreach (var subreddit in multis.Subreddits)
            {
                int outdatedPosts = 0;
                //var posts = client.Subreddit(subreddit.Name).About().Posts.Hot;
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
                        if (timePassed.TotalDays > 7)
                        {
                            outdatedPosts++;
                        }
                        else
                        {
                            outdatedPosts = 0;
                        }
                        if (outdatedPosts >= 10)
                        {
                            Debug.WriteLine($"Finished {subreddit.Name}");
                            break;
                        }
                        if (linkPost.URL.Contains("gallery"))
                        {
                            Debug.WriteLine("Found gallery");
                            foreach (var item in linkPost.MediaMetadata)
                            {
                                int cnt = 0;
                                string imageName = subreddit.Name + "_" + linkPost.Id + System.IO.Path.GetExtension(linkPost.URL) + "_" + cnt++.ToString();
                                imageHandler.AddImagesFromURL(item.Value.s.u, imageName, subreddit.Name, linkPost.Ups);
                                lastGoodPost = linkPost.CreatedUTC;
                                lastGoodName = linkPost.Name;
                            }
                            var dupa = linkPost;
                        }
                        else
                        {
                            string imageName = subreddit.Name + "_" + linkPost.Id + System.IO.Path.GetExtension(linkPost.URL);
                            imageHandler.AddImagesFromURL(linkPost.URL, imageName, subreddit.Name, linkPost.Ups);
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
        }

        private void StartWorker()
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

        public MyImage GetNextImage()
        {
            return imageHandler.GetNextImage();
        }
        public MyImage GetPrevImage()
        {
            return imageHandler.GetPrevImage();
        }
    }
}
