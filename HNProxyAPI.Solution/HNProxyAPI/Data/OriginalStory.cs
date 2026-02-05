using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace HNProxyAPI.Data
{
    /* Sample Story Details (JSON):
         * https://hacker-news.firebaseio.com/v0/item/46829147.json
         * Total http response size in bytes: 
         { 
            "by":"iambateman",
            "descendants":422,
            "id":46829147,
            "kids":[46829381,46835766,46829308,46837159,46831343,46835020,46830624,46830542,46829399,46830571,46829439,46829516,46839210,46837916,46829694,46837610,46834320,46831448,46833817,46829348,46839739,46835010,46838155,46830849,46829522,46829679,46837264,46840051,46834362,46831268,46829549,46831892,46832459,46831828,46835887,46836300,46830437,46831845,46829683,46830259,46830886,46830327,46829443,46833714,46835248,46835963,46833681,46831266,46835079,46836968,46834542,46829980,46831346,46831807,46830096,46831016,46832253,46834244,46835662,46834201,46835312,46835293,46830528,46837010,46831900,46829465,46833467,46833245,46830243,46830301,46837918,46829417,46832123,46830566,46833087,46832037,46835425,46832500,46829538,46829344,46829619,46829907,46834266,46831849,46831797,46831767,46831910,46832349,46831402,46831382,46835089,46830223,46830166,46829525,46829638,46830843,46830990,46837146,46829566,46837361,46833940,46832166,46836729,46829892],
            "score":1769,
            "time":1769803524,
            "title":"Antirender: remove the glossy shine on architectural renderings",  
            "type":"story",
            "url":"https://antirender.com/"},
          */

    // #OLD: not used anymore as only Story is required and converted was implented
    // Other attributes can be used in the future if required - only have to change the converter
    /// <summary>
    /// Original Story details retrieved from Hacker News
    /// </summary>    
    public record struct OriginalStory(
        [property: JsonPropertyName("by")] string by,
        [property: JsonPropertyName("descendants")] int descendants,
        [property: JsonPropertyName("kids")] List<int> kids,
        [property: JsonPropertyName("id")] int id,
        [property: JsonPropertyName("title")] string title,
        [property: JsonPropertyName("url")] string url,
        [property: JsonPropertyName("time")] long time,
        [property: JsonPropertyName("type")] string type,
        [property: JsonPropertyName("score")] int score
        );

}