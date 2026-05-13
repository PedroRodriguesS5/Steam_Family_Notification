using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace SteamGameNotify
{
    public class Worker : BackgroundService
    {
        private readonly ILogger<Worker> _logger;
        private readonly IConfiguration _configuration;

        public Worker(ILogger<Worker> logger, IConfiguration configuration)
        {
            _configuration = configuration;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            int targetHour = 9;
            DateTime? lastSuccessDate = null;

            while (!stoppingToken.IsCancellationRequested)
            {
                DateTime now = DateTime.Now;

                DateTime todayTarget = now.Date.AddHours(targetHour);

                bool isTimeToRun = now >= todayTarget &&
                                   (lastSuccessDate == null || lastSuccessDate.Value.Date < now.Date);

                if (isTimeToRun)
                {
                    try
                    {
                        _logger.LogInformation("Running daily Steam check at {Time}", now);
                        await CheckSteamGamesAsync();
                        _logger.LogInformation("Success! Games checked.");

                        lastSuccessDate = now;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Execution failed. Retrying in 5 minutes...");
                        await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);

                        continue;
                    }
                }

                now = DateTime.Now; 
                DateTime nextTarget = now.Date.AddHours(targetHour);

                if (now >= nextTarget)
                {
                    nextTarget = nextTarget.AddDays(1);
                }

                TimeSpan waitTime = nextTarget - now;
                _logger.LogInformation("Next run scheduled for {NextTarget}. Sleeping for {WaitTime}", nextTarget,
                    waitTime);

                await Task.Delay(waitTime, stoppingToken);
            }
        }

        private async Task CheckSteamGamesAsync()
        {
            List<string> steamUserIds = _configuration.GetSection("BotConfig:SteamUserIds").Get<List<string>>();
            string saveFilePath = "saved_library_ids.json";
            if (steamUserIds == null || !steamUserIds.Any())
            {
                _logger.LogWarning("Nenhum Steam ID encontrado! Verifique se a seção BotConfig:SteamUserIds existe no appsettings.");
                return;
            }
            bool isFirstRun = !System.IO.File.Exists(saveFilePath);
            var steamProvider = new SteamProvider(_configuration);
            var allSharedGames = new List<SteamGame>();
            
          
            
            foreach (var steamId in steamUserIds)
            {
                var userGames = await steamProvider.GetGames(steamId);
                allSharedGames.AddRange(userGames);
                await Task.Delay(1000);
            }

            var uniqueGames = allSharedGames
                .GroupBy(g => g.AppId)
                .Select(group => group.First())
                .ToList();

            List<int> actualGamesIds = new List<int>();
            if (!isFirstRun)
            {
                string json = await System.IO.File.ReadAllTextAsync(saveFilePath);
                actualGamesIds = System.Text.Json.JsonSerializer.Deserialize<List<int>>(json) ?? new List<int>();
            }

            if (isFirstRun)
            {
                _logger.LogInformation("First run detected! Saving {count} games", uniqueGames.Count);

                var currentIdsToSave = uniqueGames.Select(g => g.AppId).ToList();
                string newIdsJson = System.Text.Json.JsonSerializer.Serialize(currentIdsToSave);
                await System.IO.File.WriteAllTextAsync(saveFilePath, newIdsJson);
            }
            else
            {
                var newGames = uniqueGames.Where(g => !actualGamesIds.Contains(g.AppId)).ToList();

                if (newGames.Any())
                {
                    _logger.LogInformation("Found {Count} new games!", newGames.Count);

                    foreach (var game in newGames)
                    {
                        bool isShareable = await steamProvider.IsGamePaidAsync(game.AppId);
                        if (isShareable)
                        {
                            _logger.LogInformation("{GameName} is shareable! Sending notification.", game.Name);
                            await SendDiscordNotification(game.Name);
                        }
                        else
                        {
                            _logger.LogInformation("Skipped: {GameName} (Not eligible for Family Sharing or is Free-to-Play).", game.Name);
                        }
                        
                        await Task.Delay(2000);
                    }

                    var currentIdsToSave = uniqueGames.Select(g => g.AppId).ToList();
                    string newJson = System.Text.Json.JsonSerializer.Serialize(currentIdsToSave);
                    await System.IO.File.WriteAllTextAsync(saveFilePath, newJson);
                }
                else
                {
                    _logger.LogInformation("No new games found today.");
                }
            }
            
        }

        private async Task SendDiscordNotification(string gameName)
        {
            string discordWebHookUrl = _configuration["BotConfig:DiscordWebHookUrl"];

            using var httpClient = new HttpClient();

            var payload = new { content = $"🚨 **{gameName}!**\n🎮 Adicionado na família steam" };
            string jsonPayload = System.Text.Json.JsonSerializer.Serialize(payload);
            var httpContent = new StringContent(jsonPayload, System.Text.Encoding.UTF8, "application/json");

            try
            {
                var response = await httpClient.PostAsync(discordWebHookUrl, httpContent);
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError("Failed to send Discord message. Status: {Status}", response.StatusCode);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error connecting to Discord.");
            }
        }
    }
}
