using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace HNProxyAPI.Data
{
    /* SAMPLE STORY JSON
    { 
        "title": "A uBlock Origin update was rejected from the Chrome Web Store",
        "uri": "https://github.com/uBlockOrigin/uBlock-issues/issues/745",
        "postedBy": "ismaildonmez",
        "time": "2019-10-12T13:43:01+00:00",
        "score": 1716,
        "commentCount": 572 (#DEPRECATED - Not used anymore)
    },*/

    /// <summary>
    /// 
    /// </summary>
    /// <param name="id"></param>
    /// <param name="title"></param>
    /// <param name="uri"></param>
    /// <param name="postedBy"></param>
    /// <param name="time"></param>
    /// <param name="score"></param>
    [JsonConverter(typeof(StoryConverter))]
    public readonly record struct Story(
        int id,
        string title,
        string uri,         // from: url         
        string postedBy,    // from: by
        DateTime time,
        int score
        );

    /*
    // #OLD: Implementation of Struct
    /// <summary>
    /// Stack-allocated record type to represent a story from Hacker News
    /// </summary>
    /// <param name="title"></param>
    /// <param name="uri"></param>
    /// <param name="postedBy"></param>
    /// <param name="time"></param>
    /// <param name="score"></param>
    public struct Story
    {
        [JsonPropertyName("title")]
        public string title { get; init; }

        [JsonPropertyName("uri")] 
        public string uri { get; init; }

        [JsonPropertyName("postedBy")] 
        public string postedBy { get; init; }

        [JsonPropertyName("time")] 
        public DateTime time { get; init; }

        [JsonPropertyName("score")] 
        public int score { get; init; }


        public Story(string title, string uri, string postedBy, long time, int score)
        {
            this.title = title;
            this.uri = uri;
            this.postedBy = postedBy;
            this.time = DateTimeOffset.FromUnixTimeSeconds(time).UtcDateTime; // Builds date&time since 1970.01.01 00:00:00.000
            this.score = score;
        }
    }
    */

}
