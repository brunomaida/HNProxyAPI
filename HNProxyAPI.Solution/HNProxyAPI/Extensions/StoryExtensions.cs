using HNProxyAPI.Data;

namespace HNProxyAPI.Extensions
{
    /// <summary>
    /// 
    /// </summary>
    public static class StoryExtensions
    {
        /// <summary>
        /// // readable, 1-line, format
        /// </summary>
        /// <param name="story">the story structure</param>
        /// <returns></returns>
        public static string ToString(this Story story)
        {            
            return $"Id: {story.id}, Title: {story.title}, Uri: {story.uri}, PostedBy: {story.postedBy}, Time: {story.time:yyyy-MM-dd HH:mm:ss}, Score: {story.score}";
        }

    }
}
