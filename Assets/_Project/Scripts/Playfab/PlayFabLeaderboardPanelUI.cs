using System.Collections.Generic;
using System.Text;
using PlayFab.ClientModels;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class PlayFabLeaderboardPanelUI : MonoBehaviour
{
    [Header("Dependencies")]
    [SerializeField] private PlayFabLeaderboard leaderboardManager;

    [Header("UI")]
    [SerializeField] private TMP_InputField scoreInput;
    [SerializeField] private Button submitScoreButton;
    [SerializeField] private Button refreshLeaderboardButton;
    [SerializeField] private TextMeshProUGUI leaderboardText;
    [SerializeField] private TextMeshProUGUI statusText;

    private bool _refreshAfterSubmit;

    private void Awake()
    {
        if (leaderboardManager == null)
        {
            leaderboardManager = FindObjectOfType<PlayFabLeaderboard>();
        }

        if (submitScoreButton != null)
        {
            submitScoreButton.onClick.RemoveListener(HandleSubmitScoreClicked);
            submitScoreButton.onClick.AddListener(HandleSubmitScoreClicked);
        }

        if (refreshLeaderboardButton != null)
        {
            refreshLeaderboardButton.onClick.RemoveListener(HandleRefreshClicked);
            refreshLeaderboardButton.onClick.AddListener(HandleRefreshClicked);
        }

        SetStatus("Sẵn sàng tải bảng xếp hạng.");
    }

    private void OnEnable()
    {
        if (leaderboardManager == null)
        {
            return;
        }

        leaderboardManager.OnScoreSubmitted += HandleScoreSubmitted;
        leaderboardManager.OnScoreSubmitFailed += HandleScoreSubmitFailed;
        leaderboardManager.OnLeaderboardLoaded += HandleLeaderboardLoaded;
        leaderboardManager.OnLeaderboardLoadFailed += HandleLeaderboardLoadFailed;
    }

    private void OnDisable()
    {
        if (leaderboardManager == null)
        {
            return;
        }

        leaderboardManager.OnScoreSubmitted -= HandleScoreSubmitted;
        leaderboardManager.OnScoreSubmitFailed -= HandleScoreSubmitFailed;
        leaderboardManager.OnLeaderboardLoaded -= HandleLeaderboardLoaded;
        leaderboardManager.OnLeaderboardLoadFailed -= HandleLeaderboardLoadFailed;
    }

    private void HandleSubmitScoreClicked()
    {
        if (leaderboardManager == null)
        {
            SetStatus("Thiếu PlayFabLeaderboard trong scene.");
            return;
        }

        if (scoreInput == null || !int.TryParse(scoreInput.text.Trim(), out int score))
        {
            SetStatus("Điểm không hợp lệ.");
            return;
        }

        SetStatus("Đang gửi điểm...");
        SubmitScoreFromGame(score, false);
    }

    private void HandleRefreshClicked()
    {
        if (leaderboardManager == null)
        {
            SetStatus("Thiếu PlayFabLeaderboard trong scene.");
            return;
        }

        RefreshLeaderboard();
    }

    private void HandleScoreSubmitted()
    {
        SetStatus("Gửi điểm thành công.");

        if (_refreshAfterSubmit && leaderboardManager != null)
        {
            _refreshAfterSubmit = false;
            RefreshLeaderboard();
        }
    }

    private void HandleScoreSubmitFailed(string error)
    {
        SetStatus("Gửi điểm thất bại: " + error);
    }

    private void HandleLeaderboardLoaded(List<PlayerLeaderboardEntry> entries)
    {
        StringBuilder sb = new StringBuilder();
        if (entries == null || entries.Count == 0)
        {
            sb.Append("Chưa có dữ liệu.");
        }
        else
        {
            for (int i = 0; i < entries.Count; i++)
            {
                PlayerLeaderboardEntry entry = entries[i];
                string name = string.IsNullOrEmpty(entry.DisplayName) ? "Unknown" : entry.DisplayName;
                sb.AppendLine((entry.Position + 1) + ". " + name + " - " + entry.StatValue);
            }
        }

        if (leaderboardText != null)
        {
            leaderboardText.text = sb.ToString();
        }

        SetStatus("Tải bảng xếp hạng thành công.");
    }

    private void HandleLeaderboardLoadFailed(string error)
    {
        SetStatus("Tải bảng xếp hạng thất bại: " + error);
    }

    private void SetStatus(string value)
    {
        if (statusText != null)
        {
            statusText.text = value;
        }

        Debug.Log("[PlayFabLeaderboardPanelUI] " + value);
    }

    public void SubmitScoreFromGame(int score, bool refreshAfterSubmit)
    {
        if (leaderboardManager == null)
        {
            SetStatus("Thiếu PlayFabLeaderboard trong scene.");
            return;
        }

        _refreshAfterSubmit = refreshAfterSubmit;
        SetStatus("Đang gửi điểm...");
        leaderboardManager.SubmitScore(score);
    }

    public void RefreshLeaderboard()
    {
        if (leaderboardManager == null)
        {
            SetStatus("Thiếu PlayFabLeaderboard trong scene.");
            return;
        }

        SetStatus("Đang tải Top 20...");
        leaderboardManager.GetTopPlayers();
    }
}
