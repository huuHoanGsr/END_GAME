using PlayFab;
using PlayFab.ClientModels;
using UnityEngine;
using System.Collections.Generic;
using System;

public class PlayFabLeaderboard : MonoBehaviour
{
    [SerializeField] private string statisticName = "CareerCatch_Minigame";

    public string StatisticName => statisticName;

    public event Action OnScoreSubmitted;
    public event Action<string> OnScoreSubmitFailed;
    public event Action<List<PlayerLeaderboardEntry>> OnLeaderboardLoaded;
    public event Action<string> OnLeaderboardLoadFailed;

    public void SetStatisticName(string newStatisticName)
    {
        if (string.IsNullOrWhiteSpace(newStatisticName))
        {
            return;
        }

        statisticName = newStatisticName.Trim();
    }

    // Gửi điểm lên Leaderboard (Yêu cầu 3 & 9)
    public void SubmitScore(int score) {
        // Chống spam cơ bản: Nếu điểm cao bất thường thì không gửi (Yêu cầu 6)
        if (score > 10000) { 
            string message = "Điểm quá cao, nghi vấn hack!";
            Debug.LogWarning(message);
            OnScoreSubmitFailed?.Invoke(message);
            return;
        }

        var request = new UpdatePlayerStatisticsRequest {
            Statistics = new List<StatisticUpdate> {
                new StatisticUpdate {
                    StatisticName = statisticName, // Phải trùng tên trên Web PlayFab
                    Value = score
                }
            }
        };

        PlayFabClientAPI.UpdatePlayerStatistics(request, 
            result =>
            {
                Debug.Log("Đã lưu thành tích cuối cùng thành công!");
                OnScoreSubmitted?.Invoke();
            },
            error =>
            {
                string message = error.GenerateErrorReport();
                Debug.LogError(message);
                OnScoreSubmitFailed?.Invoke(message);
            }
        );
    }

    // Lấy Top 20 (Yêu cầu 5)
    public void GetTopPlayers() {
        if (!PlayFabClientAPI.IsClientLoggedIn())
        {
            const string message = "Chưa đăng nhập PlayFab. Hãy đợi vài giây rồi thử lại.";
            Debug.LogWarning("[PlayFabLeaderboard] " + message);
            OnLeaderboardLoadFailed?.Invoke(message);
            return;
        }

        var request = new GetLeaderboardRequest {
            StatisticName = statisticName,
            StartPosition = 0,
            MaxResultsCount = 20
        };

        PlayFabClientAPI.GetLeaderboard(request, result => {
            foreach (var item in result.Leaderboard) {
                Debug.Log($"{item.Position + 1}. {item.DisplayName} - {item.StatValue}");
            }
            OnLeaderboardLoaded?.Invoke(result.Leaderboard);
        }, error =>
        {
            string message = error.GenerateErrorReport();
            Debug.LogError(message);
            OnLeaderboardLoadFailed?.Invoke(message);
        });
    }
}