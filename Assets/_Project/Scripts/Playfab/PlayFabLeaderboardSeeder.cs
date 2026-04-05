using System.Collections;
using PlayFab;
using PlayFab.ClientModels;
using UnityEngine;

public class PlayFabLeaderboardSeeder : MonoBehaviour
{
    public enum LeaderboardMode
    {
        MainGameTime3Min,
        MainGameTime5Min,
        MainGameSpeedRun,
        Minigame
    }

    [Header("Seeder Config")]
    [SerializeField] private LeaderboardMode leaderboardMode = LeaderboardMode.Minigame;
    [SerializeField] private string overrideStatisticName = string.Empty;
    [SerializeField, Min(1)] private int accountCount = 20;
    [SerializeField] private string accountPrefix = "seed_";
    [SerializeField, Min(0)] private int minScore = 10;
    [SerializeField, Min(0)] private int maxScore = 200;
    [SerializeField] private bool restoreDeviceLoginAfterSeed = true;
    [ContextMenu("Seed Leaderboard")]
    public void SeedLeaderboard()
    {
        StartCoroutine(SeedCoroutine());
    }

    private IEnumerator SeedCoroutine()
    {
        string statisticName = ResolveStatisticName();
        if (string.IsNullOrWhiteSpace(statisticName))
        {
            Debug.LogWarning("[PlayFabSeeder] Statistic name is empty.");
            yield break;
        }

        int safeMin = Mathf.Min(minScore, maxScore);
        int safeMax = Mathf.Max(minScore, maxScore);

        for (int i = 1; i <= accountCount; i++)
        {
            string customId = accountPrefix + i.ToString("00");
            int score = Random.Range(safeMin, safeMax + 1);

            bool done = false;
            bool ok = false;

            var loginRequest = new LoginWithCustomIDRequest
            {
                CustomId = customId,
                CreateAccount = true
            };

            PlayFabClientAPI.LoginWithCustomID(loginRequest, _ =>
            {
                var updateRequest = new UpdatePlayerStatisticsRequest
                {
                    Statistics = new System.Collections.Generic.List<StatisticUpdate>
                    {
                        new StatisticUpdate
                        {
                            StatisticName = statisticName,
                            Value = score
                        }
                    }
                };

                PlayFabClientAPI.UpdatePlayerStatistics(updateRequest, __ =>
                {
                    ok = true;
                    done = true;
                }, error =>
                {
                    Debug.LogWarning("[PlayFabSeeder] Submit failed for " + customId + ": " + error.GenerateErrorReport());
                    done = true;
                });
            }, error =>
            {
                Debug.LogWarning("[PlayFabSeeder] Login failed for " + customId + ": " + error.GenerateErrorReport());
                done = true;
            });

            while (!done)
            {
                yield return null;
            }

            if (ok)
            {
                Debug.Log("[PlayFabSeeder] Seeded " + customId + " score=" + score);
            }

            yield return null;
        }

        if (restoreDeviceLoginAfterSeed)
        {
            yield return LoginWithDeviceId();
        }

        Debug.Log("[PlayFabSeeder] Done seeding " + accountCount + " accounts.");
    }

    private IEnumerator LoginWithDeviceId()
    {
        bool done = false;
        var loginRequest = new LoginWithCustomIDRequest
        {
            CustomId = SystemInfo.deviceUniqueIdentifier,
            CreateAccount = true
        };

        PlayFabClientAPI.LoginWithCustomID(loginRequest, result =>
        {
            PlayFabSessionState.SetLoggedIn(result.PlayFabId);
            done = true;
        }, error =>
        {
            Debug.LogWarning("[PlayFabSeeder] Restore device login failed: " + error.GenerateErrorReport());
            done = true;
        });

        while (!done)
        {
            yield return null;
        }
    }

    private string ResolveStatisticName()
    {
        if (!string.IsNullOrWhiteSpace(overrideStatisticName))
        {
            return overrideStatisticName.Trim();
        }

        switch (leaderboardMode)
        {
            case LeaderboardMode.MainGameTime3Min:
                return "PolySwipe_Time3Min";
            case LeaderboardMode.MainGameTime5Min:
                return "PolySwipe_Time5Min";
            case LeaderboardMode.MainGameSpeedRun:
                return "PolySwipe_SpeedRun";
            case LeaderboardMode.Minigame:
            default:
                return "CareerCatch_Minigame";
        }
    }
}
