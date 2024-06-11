using Newtonsoft.Json;
using Reddit;
using Reddit.Controllers;
using Reddit.Models;
using Reddit.Things;
using RedditSharp.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;
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
            imageHandler = new ImageHandler();
            imageHandler.ImageLoaded += ImageLoadedCallback;
            StartWorker();
        }

        private void ImageLoadedCallback(int count)
        {
            OnImageCountUpdated?.Invoke(count);
        }

        private static async Task Worker()
        {
            int currentIndex = 0;
            string path = System.IO.Path.Combine("C:\\Users\\mbabiec\\Documents\\fun\\RedditSharp\\RedditSharp\\tokens.json");
            string json = System.IO.File.ReadAllText(path);
            var tokens = JsonConvert.DeserializeObject<TokenModel>(json);
            RedditClient client = new(tokens.AppID, tokens.RefreshToken);
            LabeledMulti multis = client.Account.Me.Multis()[0];
            DateTime startTime = DateTime.UtcNow;
            foreach (var subreddit in multis.Subreddits)
            {
                int outdatedPosts = 0;
                do
                {
                    var linkPost = client.Subreddit(subreddit.Name).Posts.IHot[currentIndex++];
                    if (linkPost is not LinkPost)
                    {
                        continue;
                    }
                    TimeSpan timePassed = startTime - linkPost.Created;
                    if (timePassed.TotalDays > 7)
                    {
                        outdatedPosts++;
                    }
                    else
                    {
                        outdatedPosts = 0;
                    }
                    imageHandler.AddNewImage(((LinkPost)linkPost).URL, subreddit.Name, linkPost.UpVotes);
                    await Task.Delay(200);
                } while (outdatedPosts < 10);
            }
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

        public ImageEntry GetNextImage()
        {
            return imageHandler.GetNextImage();
        }
    }
}
