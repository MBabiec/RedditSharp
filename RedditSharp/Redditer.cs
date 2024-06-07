using Newtonsoft.Json;
using Reddit;
using Reddit.Controllers;
using Reddit.Models;
using Reddit.Things;
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
        public Redditer()
        {
            //LinkPost posts = (LinkPost)client.Subreddit(multis.Subreddits[0].Name).About().Posts.Hot[imageCounter];
            //LoadImageAsync(posts.URL);
            //LinkPost posts = (LinkPost)client.Subreddit(multis.Subreddits[0].Name).About().Posts.Hot[++imageCounter];
            //BitmapImage bitmapImage = await ImageLoader.LoadImageAsync(imageUrl);
            StartWorker();
        }

        private static async Task Worker()
        {
            //while (true)
            //{
            //    Debug.WriteLine("Running in the 90s");
            //    await Task.Delay(1000);
            //}
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
                } while (outdatedPosts < 10);
            }
        }

        private static async void StartWorker()
        {
            try
            {
                await Worker();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"An error occured: {ex.Message}");
            }
        }

        public BitmapImage GetNextImage()
        {
            BitmapImage image = null;
            return image;
        }
    }
}
