using System.Collections.Generic;
using PlayFab;
using PlayFab.ClientModels;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

public class UIManager : MonoBehaviour
{
    [Header("Panels - Dùng CanvasGroup để tối ưu")]
    [SerializeField] private CanvasGroup mainMenuPanel;
    [SerializeField] private CanvasGroup gameplayPanel;
    [SerializeField] private CanvasGroup resultPanel;

    [Header("Gameplay UI")]
    [SerializeField] private Transform cardArea; // Nơi thẻ bài sinh ra
    [SerializeField] private GameObject cardPrefab; // Nhớ gắn script SwipeCard vào prefab này
    [SerializeField] private CanvasGroup likeHint;
    [SerializeField] private CanvasGroup skipHint;
    [SerializeField] private RectTransform likeStampTransform;
    [SerializeField] private RectTransform skipStampTransform;
    [SerializeField] private Text countdownText;
    [SerializeField] private Text gameplayScoreText;
    [SerializeField] private Text gameplayEventText;
    [SerializeField] private RectTransform gameplayObstacleLayer;
    [SerializeField] private GameObject runtimeObstaclePrefab;
    [SerializeField, Min(1)] private int defaultObstacleCount = 3;
    [SerializeField] private Vector2 obstacleSizeRange = new Vector2(120f, 220f);
    [SerializeField] private Color obstacleColor = new Color(0f, 0f, 0f, 0.35f);
    [SerializeField, Min(40f)] private float obstacleFallSpeed = 260f;
    [SerializeField, Min(0f)] private float obstacleTopSpawnOffset = 120f;
    [SerializeField, Min(0f)] private float obstacleBottomPadding = 40f;
    [SerializeField] private bool obstacleLoopFromTop = true;

    [Header("Result UI")]
    [SerializeField] private Text categoryResultText;
    [SerializeField] public Button settingButton;
    [Header("Playtime Gate")]
    [SerializeField] private GameObject playtimeStatusRoot;
    [SerializeField] private TextMeshProUGUI playtimeStatusText;
    [SerializeField] private Button playtimeStatusExitButton;
    [Header("PlayFab")]
    [SerializeField] private bool submitResultToPlayFab = true;
    [SerializeField] private string playFabStatisticNameTime3Min = "PolySwipe_Time3Min";
    [SerializeField] private string playFabStatisticNameTime5Min = "PolySwipe_Time5Min";
    [SerializeField] private string playFabStatisticNameSpeedRun = "PolySwipe_SpeedRun";

    [Header("Leaderboard")]
    [SerializeField] private PlayFabLeaderboard leaderboardManager;
    [SerializeField] private PlayFabLeaderboardUIToolkitController leaderboardUIToolkitController;

    [Header("Scene Navigation")]
    [SerializeField] private string minigameSceneName = "Minigame";

    private bool _playFabLoginInProgress;
    private bool _playFabLoggedIn;

    private struct RuntimeObstacleInstance
    {
        public GameObject gameObject;
        public RectTransform rectTransform;
        public float speed;
    }

    private readonly List<RuntimeObstacleInstance> _runtimeObstacles = new List<RuntimeObstacleInstance>();

    private struct PendingPlayFabSubmission
    {
        public string statisticName;
        public int score;
    }

    private PendingPlayFabSubmission? _pendingPlayFabSubmission;

    private void Start()
    {
        EnsurePlayFabLogin();
        HideLeaderboardPanel();
        BindPlaytimeExitButton();
        EnsureRuntimeEventUIReady();
        ClearPlaytimeStatus();
    }

    private void OnEnable()
    {
        GameEvents.OnMainMenuEntered += ShowMainMenu;
        GameEvents.OnGameplayEntered += ShowGameplay;
        GameEvents.OnGameEnded += ShowResult;
        GameEvents.OnTimerUpdated += UpdateTimerText;
        GameEvents.OnScoreUpdated += UpdateGameplayScoreText;
        GameEvents.OnRandomEventStateChanged += HandleRandomEventStateChanged;
        
        GameEvents.OnCardSpawned += SpawnNewCard;
    }

    private void OnDisable()
    {
        GameEvents.OnMainMenuEntered -= ShowMainMenu;
        GameEvents.OnGameplayEntered -= ShowGameplay;
        GameEvents.OnGameEnded -= ShowResult;
        GameEvents.OnTimerUpdated -= UpdateTimerText;
        GameEvents.OnScoreUpdated -= UpdateGameplayScoreText;
        GameEvents.OnRandomEventStateChanged -= HandleRandomEventStateChanged;
        
        GameEvents.OnCardSpawned -= SpawnNewCard;
    }

    private void Update()
    {
        TickRuntimeObstacleMotion();
    }

    // Các hàm xử lý chuyển State UI
    private void ShowMainMenu()
    {
        SetSettingButtonActive(true);
        HideLeaderboardPanel();
        SwitchPanel(mainMenuPanel);
        SetTimerText(string.Empty);
        SetGameplayEventText(string.Empty);
        ClearRuntimeObstacles();
        ClearPlaytimeStatus();
    }

    private void ShowGameplay()
    {
        SetSettingButtonActive(false);
        HideLeaderboardPanel();
        SwitchPanel(gameplayPanel);
        UpdateGameplayScoreText(0);
        SetGameplayEventText(string.Empty);
        ClearRuntimeObstacles();
        ClearPlaytimeStatus();
    }
    private void ShowResult(GameResultData result) 
    { 
        SetSettingButtonActive(false);
        HideLeaderboardPanel();
        SwitchPanel(resultPanel); 
        SetTimerText(string.Empty);
        SetGameplayEventText(string.Empty);
        ClearRuntimeObstacles();
        ClearPlaytimeStatus();

        if (categoryResultText != null)
        {
            categoryResultText.text = result.finalScore.ToString();
        }

        SubmitResultToPlayFab(result.finalScore, GetPolySwipeStatisticNameByCurrentMode());
    }

    private void SpawnNewCard(QuestionCardSO cardData)
    {
        // Sinh ra thẻ bài và truyền dữ liệu cho nó
        GameObject newCard = Instantiate(cardPrefab, cardArea);

        SwipeCard swipeCard = newCard.GetComponent<SwipeCard>();
        if (swipeCard != null)
        {
            swipeCard.ConfigureSwipeFeedback(likeHint, skipHint, likeStampTransform, skipStampTransform);
            swipeCard.Setup(cardData);
        }
        else
        {
            Debug.LogWarning("[UIManager] Card Prefab thiếu component SwipeCard.");
        }
    }

    // Tiện ích bật/tắt Panel siêu nhẹ, không gây giật lag (Rebuild)
    private void SwitchPanel(CanvasGroup activePanel)
    {
        SetPanelActive(mainMenuPanel, false);
        SetPanelActive(gameplayPanel, false);
        SetPanelActive(resultPanel, false);

        SetPanelActive(activePanel, true);
    }

    private void SetPanelActive(CanvasGroup panel, bool isActive)
    {
        if (panel == null)
        {
            return;
        }

        panel.alpha = isActive ? 1f : 0f;
        panel.interactable = isActive;
        panel.blocksRaycasts = isActive;
    }

    private void SetSettingButtonActive(bool isActive)
    {
        if (settingButton == null)
        {
            return;
        }

        settingButton.gameObject.SetActive(isActive);
    }

    private void UpdateTimerText(float remainingSeconds, float totalSeconds)
    {
        if (countdownText == null)
        {
            return;
        }

        int clampedSeconds = Mathf.Max(0, Mathf.CeilToInt(remainingSeconds));
        int minutes = clampedSeconds / 60;
        int seconds = clampedSeconds % 60;
        countdownText.text = string.Format("{0:00}:{1:00}", minutes, seconds);
    }

    private void SetTimerText(string value)
    {
        if (countdownText == null)
        {
            return;
        }

        countdownText.text = value;
    }

    public void ShowPlaytimeBlocked(string message)
    {
        if (playtimeStatusRoot != null)
        {
            playtimeStatusRoot.SetActive(true);
        }

        if (playtimeStatusText == null)
        {
            return;
        }

        playtimeStatusText.text = message;
    }

    private void ClearPlaytimeStatus()
    {
        if (playtimeStatusRoot != null)
        {
            playtimeStatusRoot.SetActive(false);
        }

        if (playtimeStatusText == null)
        {
            return;
        }

        playtimeStatusText.text = string.Empty;
    }

    private void BindPlaytimeExitButton()
    {
        if (playtimeStatusExitButton == null)
        {
            return;
        }

        playtimeStatusExitButton.onClick.RemoveListener(ClearPlaytimeStatus);
        playtimeStatusExitButton.onClick.AddListener(ClearPlaytimeStatus);
    }

    private void UpdateGameplayScoreText(int score)
    {
        if (gameplayScoreText == null)
        {
            return;
        }

        gameplayScoreText.text = "Điểm: " + score;
    }

    private void HandleRandomEventStateChanged(GameplayRandomEventState eventState)
    {
        if (eventState.isActive)
        {
            SetGameplayEventText("Event: " + eventState.eventName);
        }
        else
        {
            SetGameplayEventText(string.Empty);
        }

        if (eventState.eventType != GameplayRandomEventType.ObstacleBurst)
        {
            return;
        }

        if (eventState.isActive)
        {
            int obstacleCount = eventState.obstacleCount > 0 ? eventState.obstacleCount : defaultObstacleCount;
            SpawnRuntimeObstacles(obstacleCount);
        }
        else
        {
            ClearRuntimeObstacles();
        }
    }

    private void SetGameplayEventText(string value)
    {
        if (gameplayEventText == null)
        {
            return;
        }

        gameplayEventText.text = value;
    }

    private void SpawnRuntimeObstacles(int count)
    {
        ClearRuntimeObstacles();

        RectTransform targetLayer = ResolveObstacleLayer();
        if (targetLayer == null)
        {
            Debug.LogWarning("[UIManager] Gameplay obstacle layer is invalid. Skip obstacle spawn.");
            return;
        }

        Vector2 validatedSizeRange = new Vector2(
            Mathf.Max(40f, Mathf.Min(obstacleSizeRange.x, obstacleSizeRange.y)),
            Mathf.Max(60f, Mathf.Max(obstacleSizeRange.x, obstacleSizeRange.y)));

        float topSpawnY = (targetLayer.rect.height * 0.5f) + obstacleTopSpawnOffset;

        for (int i = 0; i < count; i++)
        {
            GameObject obstacleObject = InstantiateRuntimeObstacle(targetLayer, validatedSizeRange);
            if (obstacleObject == null)
            {
                continue;
            }

            RectTransform obstacleRect = obstacleObject.GetComponent<RectTransform>();
            if (obstacleRect == null)
            {
                Destroy(obstacleObject);
                continue;
            }

            obstacleObject.transform.SetAsLastSibling();

            obstacleRect.anchoredPosition = new Vector2(
                GetRandomObstacleX(targetLayer, obstacleRect.rect.width),
                topSpawnY + Random.Range(0f, 80f));

            float speed = obstacleFallSpeed * Random.Range(0.85f, 1.2f);
            _runtimeObstacles.Add(new RuntimeObstacleInstance
            {
                gameObject = obstacleObject,
                rectTransform = obstacleRect,
                speed = speed
            });
        }
    }

    private GameObject InstantiateRuntimeObstacle(RectTransform targetLayer, Vector2 validatedSizeRange)
    {
        if (runtimeObstaclePrefab != null)
        {
            return Instantiate(runtimeObstaclePrefab, targetLayer, false);
        }

        GameObject fallbackObstacle = new GameObject("RuntimeObstacle", typeof(RectTransform), typeof(Image));
        fallbackObstacle.transform.SetParent(targetLayer, false);

        RectTransform fallbackRect = fallbackObstacle.GetComponent<RectTransform>();
        float width = Random.Range(validatedSizeRange.x, validatedSizeRange.y);
        float height = Random.Range(validatedSizeRange.x, validatedSizeRange.y);
        fallbackRect.sizeDelta = new Vector2(width, height);

        Image fallbackImage = fallbackObstacle.GetComponent<Image>();
        fallbackImage.color = obstacleColor;
        fallbackImage.raycastTarget = true;

        return fallbackObstacle;
    }

    private void TickRuntimeObstacleMotion()
    {
        if (_runtimeObstacles.Count == 0)
        {
            return;
        }

        RectTransform targetLayer = ResolveObstacleLayer();
        if (targetLayer == null)
        {
            return;
        }

        float lowerBoundY = -(targetLayer.rect.height * 0.5f) - obstacleBottomPadding;
        float upperSpawnY = (targetLayer.rect.height * 0.5f) + obstacleTopSpawnOffset;

        for (int i = _runtimeObstacles.Count - 1; i >= 0; i--)
        {
            RuntimeObstacleInstance obstacle = _runtimeObstacles[i];
            if (obstacle.gameObject == null || obstacle.rectTransform == null)
            {
                _runtimeObstacles.RemoveAt(i);
                continue;
            }

            Vector2 currentPos = obstacle.rectTransform.anchoredPosition;
            currentPos.y -= obstacle.speed * Time.deltaTime;

            if (currentPos.y < lowerBoundY)
            {
                if (obstacleLoopFromTop)
                {
                    currentPos.y = upperSpawnY;
                    currentPos.x = GetRandomObstacleX(targetLayer, obstacle.rectTransform.rect.width);
                }
                else
                {
                    Destroy(obstacle.gameObject);
                    _runtimeObstacles.RemoveAt(i);
                    continue;
                }
            }

            obstacle.rectTransform.anchoredPosition = currentPos;
            _runtimeObstacles[i] = obstacle;
        }
    }

    private float GetRandomObstacleX(RectTransform targetLayer, float obstacleWidth)
    {
        float safeHalfWidth = Mathf.Max(0f, (targetLayer.rect.width - obstacleWidth) * 0.5f);
        return Random.Range(-safeHalfWidth, safeHalfWidth);
    }

    private RectTransform ResolveObstacleLayer()
    {
        if (IsSceneObject(gameplayObstacleLayer))
        {
            return gameplayObstacleLayer;
        }

        if (IsSceneObject(gameplayPanel))
        {
            gameplayObstacleLayer = gameplayPanel.transform as RectTransform;
            return gameplayObstacleLayer;
        }

        return null;
    }

    private void EnsureRuntimeEventUIReady()
    {
        if (!IsSceneObject(gameplayPanel))
        {
            return;
        }

        RectTransform gameplayPanelRect = gameplayPanel.transform as RectTransform;
        if (gameplayPanelRect == null)
        {
            return;
        }

        if (!IsSceneObject(gameplayObstacleLayer))
        {
            GameObject obstacleLayerObject = new GameObject("RuntimeObstacleLayer", typeof(RectTransform));
            obstacleLayerObject.transform.SetParent(gameplayPanelRect, false);

            RectTransform obstacleLayerRect = obstacleLayerObject.GetComponent<RectTransform>();
            obstacleLayerRect.anchorMin = Vector2.zero;
            obstacleLayerRect.anchorMax = Vector2.one;
            obstacleLayerRect.offsetMin = Vector2.zero;
            obstacleLayerRect.offsetMax = Vector2.zero;

            gameplayObstacleLayer = obstacleLayerRect;
        }

        if (gameplayEventText == null)
        {
            GameObject eventTextObject = new GameObject("RuntimeEventText", typeof(RectTransform), typeof(Text));
            eventTextObject.transform.SetParent(gameplayPanelRect, false);
            eventTextObject.transform.SetAsLastSibling();

            RectTransform eventTextRect = eventTextObject.GetComponent<RectTransform>();
            eventTextRect.anchorMin = new Vector2(0.5f, 1f);
            eventTextRect.anchorMax = new Vector2(0.5f, 1f);
            eventTextRect.pivot = new Vector2(0.5f, 1f);
            eventTextRect.anchoredPosition = new Vector2(0f, -30f);
            eventTextRect.sizeDelta = new Vector2(600f, 60f);

            Text generatedEventText = eventTextObject.GetComponent<Text>();
            generatedEventText.text = string.Empty;
            generatedEventText.alignment = TextAnchor.MiddleCenter;
            generatedEventText.color = new Color(1f, 0.94f, 0.24f, 1f);
            generatedEventText.fontSize = 34;
            generatedEventText.fontStyle = FontStyle.Bold;
            generatedEventText.raycastTarget = false;
            generatedEventText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");

            gameplayEventText = generatedEventText;
        }
    }

    private void ClearRuntimeObstacles()
    {
        for (int i = 0; i < _runtimeObstacles.Count; i++)
        {
            if (_runtimeObstacles[i].gameObject != null)
            {
                Destroy(_runtimeObstacles[i].gameObject);
            }
        }

        _runtimeObstacles.Clear();
    }

    private bool IsSceneObject(Component component)
    {
        if (component == null)
        {
            return false;
        }

        return component.gameObject.scene.IsValid() && component.gameObject.scene.isLoaded;
    }

    private void EnsurePlayFabLogin()
    {
        if (!submitResultToPlayFab || _playFabLoggedIn || _playFabLoginInProgress)
        {
            return;
        }

        // Nếu đã đăng nhập từ scene khác (ví dụ minigame/main game), dùng lại session hiện có.
        if (PlayFabSessionState.IsLoggedIn)
        {
            _playFabLoggedIn = true;

            if (_pendingPlayFabSubmission.HasValue)
            {
                PendingPlayFabSubmission pendingSubmission = _pendingPlayFabSubmission.Value;
                _pendingPlayFabSubmission = null;
                SubmitScoreToPlayFab(pendingSubmission.score, pendingSubmission.statisticName);
            }

            return;
        }

        if (PlayFabSessionState.IsLoginInProgress)
        {
            PlayFabSessionState.OnSessionLoggedIn -= HandleSharedSessionLoggedIn;
            PlayFabSessionState.OnSessionLoggedIn += HandleSharedSessionLoggedIn;
            return;
        }

        _playFabLoginInProgress = true;
        PlayFabSessionState.BeginLogin();
        LoginWithCustomId(true);
    }

    private void HandleSharedSessionLoggedIn(string playFabId)
    {
        PlayFabSessionState.OnSessionLoggedIn -= HandleSharedSessionLoggedIn;
        _playFabLoggedIn = true;

        if (_pendingPlayFabSubmission.HasValue)
        {
            PendingPlayFabSubmission pendingSubmission = _pendingPlayFabSubmission.Value;
            _pendingPlayFabSubmission = null;
            SubmitScoreToPlayFab(pendingSubmission.score, pendingSubmission.statisticName);
        }
    }

    private void LoginWithCustomId(bool createAccount)
    {
        var request = new LoginWithCustomIDRequest
        {
            CustomId = SystemInfo.deviceUniqueIdentifier,
            CreateAccount = createAccount
        };

        PlayFabClientAPI.LoginWithCustomID(request, result =>
        {
            _playFabLoginInProgress = false;
            _playFabLoggedIn = true;
            PlayFabSessionState.SetLoggedIn(result.PlayFabId);

            if (_pendingPlayFabSubmission.HasValue)
            {
                PendingPlayFabSubmission pendingSubmission = _pendingPlayFabSubmission.Value;
                _pendingPlayFabSubmission = null;
                SubmitScoreToPlayFab(pendingSubmission.score, pendingSubmission.statisticName);
            }
        }, error =>
        {
            // Trường hợp nhiều luồng cùng CreateAccount=true có thể trả về 409. Retry đăng nhập tài khoản đã có.
            if (createAccount && error != null && error.HttpCode == 409)
            {
                LoginWithCustomId(false);
                return;
            }

            _playFabLoginInProgress = false;
            PlayFabSessionState.EndLogin();
            Debug.LogError("[UIManager] PlayFab login failed: " + error.GenerateErrorReport());
        });
    }

    private string GetPolySwipeStatisticNameByCurrentMode()
    {
        switch (GameRules.SelectedMode)
        {
            case GameRuleMode.TimeLimit3Min:
                return playFabStatisticNameTime3Min;
            case GameRuleMode.TimeLimit5Min:
                return playFabStatisticNameTime5Min;
            case GameRuleMode.SpeedRunMax5Min:
                return playFabStatisticNameSpeedRun;
            default:
                return playFabStatisticNameTime5Min;
        }
    }

    private void SubmitResultToPlayFab(int score, string statisticName)
    {
        if (!submitResultToPlayFab)
        {
            return;
        }

        int clampedScore = Mathf.Max(0, score);

        if (!_playFabLoggedIn)
        {
            _pendingPlayFabSubmission = new PendingPlayFabSubmission
            {
                statisticName = statisticName,
                score = clampedScore
            };

            EnsurePlayFabLogin();
            return;
        }

        SubmitScoreToPlayFab(clampedScore, statisticName);
    }

    private void SubmitScoreToPlayFab(int score, string statisticName)
    {
        var request = new UpdatePlayerStatisticsRequest
        {
            Statistics = new List<StatisticUpdate>
            {
                new StatisticUpdate
                {
                    StatisticName = statisticName,
                    Value = score
                }
            }
        };

        PlayFabClientAPI.UpdatePlayerStatistics(request, _ =>
        {
            Debug.Log("[UIManager] Đã gửi kết quả main game lên PlayFab.");
        }, error =>
        {
            Debug.LogError("[UIManager] Gửi kết quả PlayFab thất bại: " + error.GenerateErrorReport());
        });
    }

    public void OnRestartButtonClicked()
    {
        GameEvents.OnRestartRequested?.Invoke();
    }

    public void OnMainMenuButtonClicked()
    {
        GameEvents.OnMainMenuRequested?.Invoke();
    }

    public void OnMinigameButtonClicked()
    {
        if (string.IsNullOrWhiteSpace(minigameSceneName))
        {
            Debug.LogError("[UIManager] Minigame scene name is empty.");
            return;
        }

        SceneManager.LoadScene(minigameSceneName);
    }

    public void ShowLeaderboardPanel()
    {
        EnsurePlayFabLogin();
        ResolveLeaderboardReferences();

        if (leaderboardManager != null)
        {
            leaderboardManager.SetStatisticName(GetPolySwipeStatisticNameByCurrentMode());
        }

        if (leaderboardUIToolkitController != null)
        {
            leaderboardUIToolkitController.ShowPanel();
            return;
        }

        Debug.LogWarning("[UIManager] Missing PlayFabLeaderboardUIToolkitController in scene.");
    }

    public void HideLeaderboardPanel()
    {
        ResolveLeaderboardReferences();

        if (leaderboardUIToolkitController != null)
        {
            leaderboardUIToolkitController.HidePanel();
        }
    }

    private void ResolveLeaderboardReferences()
    {
        if (leaderboardManager == null)
        {
            leaderboardManager = FindObjectOfType<PlayFabLeaderboard>();
        }

        if (leaderboardUIToolkitController == null)
        {
            leaderboardUIToolkitController = FindObjectOfType<PlayFabLeaderboardUIToolkitController>();
        }
    }
}