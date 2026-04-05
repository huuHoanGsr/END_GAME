using System.Collections.Generic;
using System.Collections;
using PlayFab.ClientModels;
using UnityEngine;
using UnityEngine.UIElements;

public class PlayFabLeaderboardUIToolkitController : MonoBehaviour
{
    [Header("Dependencies")]
    [SerializeField] private UIDocument uiDocument;
    [SerializeField] private PlayFabLeaderboard leaderboardManager;

    [Header("Behavior")]
    [SerializeField] private bool loadOnEnable = true;

    private VisualElement _overlay;
    private Label _statusLabel;
    private Button _refreshButton;
    private Button _closeButton;
    private ScrollView _entriesScroll;
    private bool _isBindingInProgress;

    private void Awake()
    {
        if (uiDocument == null)
        {
            uiDocument = GetComponent<UIDocument>();
        }

        if (leaderboardManager == null)
        {
            leaderboardManager = FindObjectOfType<PlayFabLeaderboard>();
        }
    }

    private void OnEnable()
    {
        EnsureUIBound();

        if (leaderboardManager != null)
        {
            leaderboardManager.OnLeaderboardLoaded += HandleLeaderboardLoaded;
            leaderboardManager.OnLeaderboardLoadFailed += HandleLeaderboardLoadFailed;
        }

        if (loadOnEnable)
        {
            RefreshLeaderboard();
        }
    }

    private void OnDisable()
    {
        if (leaderboardManager != null)
        {
            leaderboardManager.OnLeaderboardLoaded -= HandleLeaderboardLoaded;
            leaderboardManager.OnLeaderboardLoadFailed -= HandleLeaderboardLoadFailed;
        }
    }

    public void RefreshLeaderboard()
    {
        EnsureUIBound();

        if (leaderboardManager == null)
        {
            SetStatus("Missing PlayFabLeaderboard in scene.");
            return;
        }

        SetStatus("Loading Top 20...");
        leaderboardManager.GetTopPlayers();
    }

    public void ShowPanel()
    {
        EnsureUIBound();

        if (_overlay != null)
        {
            _overlay.style.display = DisplayStyle.Flex;
        }

        RefreshLeaderboard();
    }

    public void HidePanel()
    {
        EnsureUIBound();

        if (_overlay != null)
        {
            _overlay.style.display = DisplayStyle.None;
        }
    }

    private void EnsureUIBound()
    {
        if (_overlay != null && _entriesScroll != null)
        {
            return;
        }

        if (_isBindingInProgress)
        {
            return;
        }

        StartCoroutine(BindUIWhenReady());
    }

    private IEnumerator BindUIWhenReady()
    {
        _isBindingInProgress = true;

        int maxFrames = 30;
        int frame = 0;
        while (frame < maxFrames)
        {
            if (TryBindUI())
            {
                _isBindingInProgress = false;
                yield break;
            }

            frame++;
            yield return null;
        }

        _isBindingInProgress = false;
        Debug.LogWarning("[PlayFabLeaderboardUIToolkitController] UI Toolkit chưa sẵn sàng để bind sau 30 frames.");
    }

    private bool TryBindUI()
    {
        if (uiDocument == null)
        {
            Debug.LogWarning("[PlayFabLeaderboardUIToolkitController] Missing UIDocument reference.");
            return false;
        }

        VisualElement root = uiDocument.rootVisualElement;
        if (root == null)
        {
            return false;
        }

        _overlay = root.Q<VisualElement>("leaderboard-overlay");
        if (_overlay == null)
        {
            // Fallback để không bị treo hiển thị nếu query name bị trượt thời điểm bind.
            _overlay = root;
        }

        _statusLabel = root.Q<Label>("status-label");
        _refreshButton = root.Q<Button>("refresh-button");
        _closeButton = root.Q<Button>("close-button");
        _entriesScroll = root.Q<ScrollView>("entries-scroll");

        if (_refreshButton != null)
        {
            _refreshButton.clicked -= RefreshLeaderboard;
            _refreshButton.clicked += RefreshLeaderboard;
        }

        if (_closeButton != null)
        {
            _closeButton.clicked -= HidePanel;
            _closeButton.clicked += HidePanel;
        }

        SetStatus("Ready");
        return true;
    }

    private void HandleLeaderboardLoaded(List<PlayerLeaderboardEntry> entries)
    {
        RenderEntries(entries);
        SetStatus("Leaderboard updated.");
    }

    private void HandleLeaderboardLoadFailed(string error)
    {
        SetStatus("Load failed: " + error);
    }

    private void RenderEntries(List<PlayerLeaderboardEntry> entries)
    {
        if (_entriesScroll == null)
        {
            return;
        }

        _entriesScroll.Clear();

        if (entries == null || entries.Count == 0)
        {
            Label emptyLabel = new Label("No data yet.");
            emptyLabel.AddToClassList("col-name");
            _entriesScroll.Add(emptyLabel);
            return;
        }

        for (int i = 0; i < entries.Count; i++)
        {
            PlayerLeaderboardEntry entry = entries[i];

            VisualElement row = new VisualElement();
            row.AddToClassList("entry-row");

            Label rankLabel = new Label((entry.Position + 1).ToString());
            rankLabel.AddToClassList("col-rank");

            string displayName = string.IsNullOrEmpty(entry.DisplayName) ? "Unknown" : entry.DisplayName;
            Label nameLabel = new Label(displayName);
            nameLabel.AddToClassList("col-name");

            Label scoreLabel = new Label(entry.StatValue.ToString());
            scoreLabel.AddToClassList("col-score");

            row.Add(rankLabel);
            row.Add(nameLabel);
            row.Add(scoreLabel);
            _entriesScroll.Add(row);
        }
    }

    private void SetStatus(string value)
    {
        if (_statusLabel != null)
        {
            _statusLabel.text = value;
        }

        Debug.Log("[PlayFabLeaderboardUIToolkitController] " + value);
    }
}
