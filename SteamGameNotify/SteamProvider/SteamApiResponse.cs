using System.Text.Json.Serialization;

namespace SteamGameNotify;

public class SteamApiResponse
{
    [JsonPropertyName("response")]
    public SteamGameResponse Response { get; set; }
}