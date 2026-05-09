using System.Text.Json.Serialization;

namespace SteamGameNotify;

public class SteamGameResponse
{
    [JsonPropertyName("games")]
    public List<SteamGame> Games { get; set; }
    
    [JsonPropertyName("game_count")]
    public int GameCount { get; set; }
    
}