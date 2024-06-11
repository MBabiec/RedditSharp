using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RedditSharp.Models
{
    internal class TokenModel
    {
        [JsonProperty("accessToken")]
        public required string AccessToken { set; get; }

        [JsonProperty("refreshToken")]
        public required string RefreshToken { set; get; }

        [JsonProperty("appID")]
        public required string AppID { set; get; }
    }
}
