using HNProxyAPI.Data;

namespace HNProxyAPI.Extensions
{
    public static class StoryExtensions
    {
        public static string ToString(this Story story)
        {
            // readable, 1-line, format
            return $"Id: {story.id}, Title: {story.title}, Uri: {story.uri}, PostedBy: {story.postedBy}, Time: {story.time:yyyy-MM-dd HH:mm:ss}, Score: {story.score}";
        }

    }
}
