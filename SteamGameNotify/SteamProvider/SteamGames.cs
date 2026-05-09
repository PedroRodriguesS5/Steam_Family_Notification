using System.Text.Json.Serialization;

namespace SteamGameNotify;

public class SteamGame
{
    [JsonPropertyName("appid")]
    public int AppId { get; set; }
    [JsonPropertyName("name")]
    public required string Name { get; set; }
    public bool IsFreeToPlay { get; set; }
    public List<int> CategoryIds { get; set; } = new();

    public bool IsShareable()
    {
        if (IsFreeToPlay)
        {
            return false;
        }
        
        return CategoryIds.Contains(35);
    }
}