using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Configuration;

namespace SteamGameNotify;

public class SteamProvider
{
    private readonly string _steamApiKey;
    private readonly HttpClient _httpClient;

    [JsonPropertyName("game_count")] public int GameCount { get; set; }
    [JsonPropertyName("games")] public List<SteamGame> Games { get; set; }


    public SteamProvider(IConfiguration configuration)
    {
        _steamApiKey = configuration["BotConfig:SteamApiKey"];
        _httpClient = new HttpClient();
    }
    public async Task<List<SteamGame>> GetGames(string steamUserId)
    {
        string url =
            $"http://api.steampowered.com/IPlayerService/GetOwnedGames/v0001/?key={_steamApiKey}&steamid={steamUserId}&format=json&include_appinfo=true";
        
        var steamApiResponse = await _httpClient.GetAsync(url);
        steamApiResponse.EnsureSuccessStatusCode();
        
        var jsonContent = await steamApiResponse.Content.ReadAsStringAsync();

        var apiResult = JsonSerializer.Deserialize<SteamApiResponse>(jsonContent);        
        return apiResult?.Response?.Games ?? new List<SteamGame>();
    }

    public async Task<bool> IsGamePaidAsync(int appId)
    {
        string url = $"https://store.steampowered.com/api/appdetails?appids={appId}";

        try
        {
            var response = await _httpClient.GetAsync(url);
            if (!response.IsSuccessStatusCode) return true; 

            var jsonContent = await response.Content.ReadAsStringAsync();
            using var jsonDoc = JsonDocument.Parse(jsonContent);
            var root = jsonDoc.RootElement;
            string appString = appId.ToString();

            if (root.TryGetProperty(appString, out var appData))
            {
                if (appData.TryGetProperty("success", out var success) && success.GetBoolean())
                {
                    var data = appData.GetProperty("data");

                    if (data.TryGetProperty("is_free", out var isFree) && isFree.GetBoolean())
                    {
                        return false;
                    }
                    
                    return true;
                    
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[DEBUG] Erro ao checar AppId {appId}: {ex.Message}");
        }
        return true; 
    }
   

}