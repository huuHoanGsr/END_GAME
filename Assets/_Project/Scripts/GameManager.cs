using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using PlayFab;
using PlayFab.ClientModels;
using UnityEngine;
using UnityEngine.Serialization;

public class GameManager : MonoBehaviour
{
    [Header("Data Source")]
    [SerializeField] private QuestionDatabaseSO questionDatabase;

    [Header("Gameplay Config")]
    [SerializeField, Min(1)] private int _questionsTime3Min = 20;
    [SerializeField, Min(1)] private int _questionsTime5Min = 33;
    [SerializeField, Min(1)] private int _questionsSpeedRun = 100;
    [Header("Runtime Random Events")]
    [SerializeField] private bool enableRuntimeEvents = true;
    [SerializeField] private bool debugRuntimeEvents = false;
    [SerializeField] private bool usePerModeFixedQuestionInterval = true;
    [SerializeField, Min(1)] private int fixedIntervalTimeLimit3Min = 2;
    [SerializeField, Min(1)] private int fixedIntervalTimeLimit5Min = 3;
    [SerializeField, Min(1)] private int fixedIntervalSpeedRun = 4;
    [Tooltip("He so tan suat event. >1: event xuat hien day hon, <1: event hiem hon.")]
    [SerializeField, Range(0.25f, 3f)] private float eventFrequencyMultiplier = 1f;
    [SerializeField, Min(1)] private int firstEventAfterAnsweredQuestions = 3;
    [SerializeField, Min(1)] private int minAnsweredQuestionsBetweenEvents = 2;
    [SerializeField, Min(1)] private int maxAnsweredQuestionsBetweenEvents = 4;
    [SerializeField] private bool guaranteeEventOnQuestionMilestone = true;
    [FormerlySerializedAs("eventTriggerChance")]
    [SerializeField, Range(0f, 1f)] private float baseEventTriggerChance = 0.8f;
    [SerializeField, Min(1f)] private float defaultEventDurationSeconds = 6f;
    [SerializeField, Min(1f)] private float obstacleEventDurationSeconds = 5f;
    [SerializeField, Min(1.1f)] private float bonusScoreMultiplier = 2f;
    [SerializeField, Min(1.1f)] private float speedBoostMultiplier = 1.5f;
    [SerializeField, Min(1.1f)] private float timeReductionDrainMultiplier = 1.7f;
    [SerializeField, Min(1)] private int obstacleSpawnCount = 3;
    [Header("PlayFab Playtime Gate")]
    [SerializeField] private bool usePlayFabTimeGate = true;
    [SerializeField] private string playStartKey = "play_start";
    [SerializeField] private string playEndKey = "play_end";
    [SerializeField] private int timezoneOffsetHours = 7;
    [SerializeField] private UIManager uiManager;

    private List<QuestionCardSO> _deckOfCards = new List<QuestionCardSO>();
    private List<QuestionCardSO> _runtimeQuestionCards = new List<QuestionCardSO>();

    private int _currentCardIndex;
    private int _totalCardsPlayed;
    private int _currentSessionCardCount;
    private int _currentScore;

    private float _timeLimitSeconds;
    private float _elapsedSeconds;
    private int _lastReportedSeconds;
    private float _scoreGainMultiplier = 1f;
    private float _timerDrainMultiplier = 1f;

    private int _nextEventAnsweredQuestions;
    private int _runtimeMinQuestionsBetweenEvents;
    private int _runtimeMaxQuestionsBetweenEvents;
    private bool _isRuntimeEventActive;
    private float _activeEventRemainingSeconds;
    private GameplayRandomEventType _activeRuntimeEventType;

    private bool _isTimerActive;
    private bool _hasEnded;
    private bool _isFetchingPlayWindow;

    private void OnEnable()
    {
        GameEvents.OnCardSwiped += HandleCardSwiped;
        GameEvents.OnRestartRequested += StartGame;
        GameEvents.OnMainMenuRequested += ReturnToMainMenu;
    }

    private void OnDisable()
    {
        GameEvents.OnCardSwiped -= HandleCardSwiped;
        GameEvents.OnRestartRequested -= StartGame;
        GameEvents.OnMainMenuRequested -= ReturnToMainMenu;
    }

    private void OnDestroy()
    {
        ClearRuntimeQuestionCards();
    }

    private void Start()
    {
        GameEvents.OnMainMenuEntered?.Invoke();
    }

    public void StartGame()
    {
        if (usePlayFabTimeGate)
        {
            RequestPlayWindowAndMaybeStart();
            return;
        }

        StartGameInternal();
    }

    private void StartGameInternal()
    {
        _hasEnded = false;
        _elapsedSeconds = 0f;
        _timeLimitSeconds = GameRules.GetTimeLimitSeconds();
        _lastReportedSeconds = Mathf.CeilToInt(_timeLimitSeconds);
        _isTimerActive = true;

        _currentScore = 0;
        GameEvents.OnScoreUpdated?.Invoke(_currentScore);
        ResetRuntimeEventSystem();

        if (_runtimeQuestionCards != null && _runtimeQuestionCards.Count > 0)
        {
            _deckOfCards = new List<QuestionCardSO>(_runtimeQuestionCards);
        }
        else
        {
            _deckOfCards = questionDatabase != null && questionDatabase.defaultQuestions != null
                ? new List<QuestionCardSO>(questionDatabase.defaultQuestions)
                : new List<QuestionCardSO>();
        }

        _deckOfCards = _deckOfCards.Where(card => card != null).ToList();

        if (_deckOfCards.Count == 0)
        {
            GameEvents.OnGameEnded?.Invoke(new GameResultData
            {
                finalScore = 0
            });
            return;
        }

        int requestedCount = GetTargetCardCountForMode();
        _currentSessionCardCount = Mathf.Clamp(requestedCount, 1, _deckOfCards.Count);
        ConfigureRuntimeEventScheduleForCurrentMode();

        ShuffleDeck(_deckOfCards);
        if (_deckOfCards.Count > _currentSessionCardCount)
        {
            _deckOfCards.RemoveRange(_currentSessionCardCount, _deckOfCards.Count - _currentSessionCardCount);
        }

        _totalCardsPlayed = 0;
        _currentCardIndex = 0;

        GameEvents.OnGameplayEntered?.Invoke();
        GameEvents.OnTimerUpdated?.Invoke(_timeLimitSeconds, _timeLimitSeconds);
        SpawnNextCard();
    }

    private void RequestPlayWindowAndMaybeStart()
    {
        if (_isFetchingPlayWindow)
        {
            return;
        }

        _isFetchingPlayWindow = true;
        var request = new GetTitleDataRequest
        {
            Keys = new List<string> { playStartKey, playEndKey }
        };

        PlayFabClientAPI.GetTitleData(request, OnTitleDataSuccess, OnPlayfabError);
    }

    private void OnTitleDataSuccess(GetTitleDataResult result)
    {
        _isFetchingPlayWindow = false;
        if (result == null || result.Data == null)
        {
            ShowPlaytimeBlocked("Khong the kiem tra gio choi. Vui long thu lai sau.");
            return;
        }

        if (!result.Data.TryGetValue(playStartKey, out string startValue) ||
            !result.Data.TryGetValue(playEndKey, out string endValue))
        {
            ShowPlaytimeBlocked("Chua cau hinh gio choi tren PlayFab.");
            return;
        }

        if (!TryParseTime(startValue, out TimeSpan start) || !TryParseTime(endValue, out TimeSpan end))
        {
            ShowPlaytimeBlocked("Dinh dang gio khong hop le. Dung HH:mm.");
            return;
        }

        PlayFabClientAPI.GetTime(new GetTimeRequest(),
            timeResult =>
            {
                DateTime serverUtc = timeResult.Time.ToUniversalTime();
                if (IsWithinAllowedWindow(serverUtc, start, end))
                {
                    StartGameInternal();
                    return;
                }

                ShowPlaytimeBlocked(BuildBlockedMessage(start, end));
            },
            OnPlayfabError);
    }

    private void OnPlayfabError(PlayFabError error)
    {
        _isFetchingPlayWindow = false;
        ShowPlaytimeBlocked("Khong the kiem tra gio choi. Vui long thu lai sau.");
    }

    private void ShowPlaytimeBlocked(string message)
    {
        if (uiManager == null)
        {
            uiManager = FindObjectOfType<UIManager>();
        }

        GameEvents.OnMainMenuEntered?.Invoke();
        if (uiManager != null)
        {
            uiManager.ShowPlaytimeBlocked(message);
        }
    }

    private string BuildBlockedMessage(TimeSpan start, TimeSpan end)
    {
        string startText = string.Format("{0:00}:{1:00}", start.Hours, start.Minutes);
        string endText = string.Format("{0:00}:{1:00}", end.Hours, end.Minutes);
        return "Chua den gio choi. Vui long quay lai tu " + startText + " den " + endText + ".";
    }

    private bool TryParseTime(string value, out TimeSpan time)
    {
        return TimeSpan.TryParseExact(value, "hh\\:mm", CultureInfo.InvariantCulture, out time) ||
               TimeSpan.TryParseExact(value, "h\\:mm", CultureInfo.InvariantCulture, out time);
    }

    private bool IsWithinAllowedWindow(DateTime serverUtc, TimeSpan start, TimeSpan end)
    {
        DateTime localTime = serverUtc.AddHours(timezoneOffsetHours);
        int currentMinutes = (int)localTime.TimeOfDay.TotalMinutes;
        int startMinutes = (int)start.TotalMinutes;
        int endMinutes = (int)end.TotalMinutes;

        if (startMinutes == endMinutes)
        {
            return false;
        }

        if (endMinutes > startMinutes)
        {
            return currentMinutes >= startMinutes && currentMinutes <= endMinutes;
        }

        return currentMinutes >= startMinutes || currentMinutes <= endMinutes;
    }

    public void SetRuntimeQuestionCards(List<QuestionCardSO> runtimeQuestionCards)
    {
        ClearRuntimeQuestionCards();

        if (runtimeQuestionCards == null)
        {
            _runtimeQuestionCards = new List<QuestionCardSO>();
            return;
        }

        _runtimeQuestionCards = runtimeQuestionCards.Where(card => card != null).ToList();
    }

    public void ClearRuntimeQuestionCards()
    {
        if (_runtimeQuestionCards == null)
        {
            return;
        }

        foreach (QuestionCardSO card in _runtimeQuestionCards)
        {
            if (card != null)
            {
                Destroy(card);
            }
        }

        _runtimeQuestionCards.Clear();
    }

    public void ReturnToMainMenu()
    {
        _currentCardIndex = 0;
        _totalCardsPlayed = 0;
        _isTimerActive = false;
        ResetRuntimeEventSystem();
        GameEvents.OnMainMenuEntered?.Invoke();
    }

    private void SpawnNextCard()
    {
        if (_hasEnded)
        {
            return;
        }

        if (_currentCardIndex >= _deckOfCards.Count)
        {
            EndGame();
            return;
        }

        GameEvents.OnProgressUpdated?.Invoke(_totalCardsPlayed + 1, _currentSessionCardCount);
        GameEvents.OnCardSpawned?.Invoke(_deckOfCards[_currentCardIndex]);
    }

    private void HandleCardSwiped(bool isRightSwipe)
    {
        if (_hasEnded || _currentCardIndex >= _deckOfCards.Count)
        {
            return;
        }

        QuestionCardSO currentCard = _deckOfCards[_currentCardIndex];
        List<SwipeEffect> effectsToApply = isRightSwipe ? currentCard.rightSwipeEffects : currentCard.leftSwipeEffects;

        if (effectsToApply != null)
        {
            for (int i = 0; i < effectsToApply.Count; i++)
            {
                SwipeEffect effect = effectsToApply[i];
                int delta = effect.GetSignedPoints();
                if (delta > 0)
                {
                    delta = Mathf.RoundToInt(delta * _scoreGainMultiplier);
                }

                _currentScore += delta;
            }
        }

        _totalCardsPlayed++;
        _currentCardIndex++;

        GameEvents.OnScoreUpdated?.Invoke(_currentScore);
        TryRollRuntimeEventByAnsweredQuestions();

        if (_totalCardsPlayed < _currentSessionCardCount)
        {
            SpawnNextCard();
            return;
        }

        EndGame();
    }

    private void ShuffleDeck(List<QuestionCardSO> cards)
    {
        for (int i = cards.Count - 1; i > 0; i--)
        {
            int randomIndex = UnityEngine.Random.Range(0, i + 1);
            (cards[i], cards[randomIndex]) = (cards[randomIndex], cards[i]);
        }
    }

    private int GetTargetCardCountForMode()
    {
        switch (GameRules.SelectedMode)
        {
            case GameRuleMode.TimeLimit3Min:
                return _questionsTime3Min;
            case GameRuleMode.TimeLimit5Min:
                return _questionsTime5Min;
            case GameRuleMode.SpeedRunMax5Min:
            default:
                return _questionsSpeedRun;
        }
    }

    private void EndGame()
    {
        if (_hasEnded)
        {
            return;
        }

        _hasEnded = true;
        _isTimerActive = false;
        ResetRuntimeEventSystem();

        var resultData = new GameResultData
        {
            finalScore = _currentScore
        };

        string playerId = SystemInfo.deviceUniqueIdentifier;
        StartCoroutine(ResultSender.SendResultToServer(playerId, resultData));

        GameEvents.OnGameEnded?.Invoke(resultData);
    }

    private void Update()
    {
        TickRuntimeEvent();

        if (!_isTimerActive || _hasEnded)
        {
            return;
        }

        _elapsedSeconds += Time.deltaTime * _timerDrainMultiplier;
        float remainingSeconds = Mathf.Max(0f, _timeLimitSeconds - _elapsedSeconds);
        int remainingWhole = Mathf.CeilToInt(remainingSeconds);

        if (remainingWhole != _lastReportedSeconds)
        {
            _lastReportedSeconds = remainingWhole;
            GameEvents.OnTimerUpdated?.Invoke(remainingSeconds, _timeLimitSeconds);
        }

        if (remainingSeconds <= 0f)
        {
            HandleTimeExpired();
        }
    }

    private void HandleTimeExpired()
    {
        if (_hasEnded)
        {
            return;
        }

        GameEvents.OnTimerUpdated?.Invoke(0f, _timeLimitSeconds);
        EndGame();
    }

    private void ResetRuntimeEventSystem()
    {
        if (_isRuntimeEventActive)
        {
            EndRuntimeEvent(announceStateChange: true);
        }

        _scoreGainMultiplier = 1f;
        _timerDrainMultiplier = 1f;
        _nextEventAnsweredQuestions = Mathf.Max(1, firstEventAfterAnsweredQuestions);
        _runtimeMinQuestionsBetweenEvents = Mathf.Max(1, minAnsweredQuestionsBetweenEvents);
        _runtimeMaxQuestionsBetweenEvents = Mathf.Max(_runtimeMinQuestionsBetweenEvents, maxAnsweredQuestionsBetweenEvents);
        _isRuntimeEventActive = false;
        _activeEventRemainingSeconds = 0f;
        _activeRuntimeEventType = GameplayRandomEventType.None;
        GameRules.SetSwipeAnimationSpeedMultiplier(1f);
        GameEvents.OnRandomEventStateChanged?.Invoke(new GameplayRandomEventState
        {
            eventType = GameplayRandomEventType.None,
            eventName = string.Empty,
            isActive = false,
            durationSeconds = 0f,
            obstacleCount = 0
        });
    }

    private void TryRollRuntimeEventByAnsweredQuestions()
    {
        if (!enableRuntimeEvents || _isRuntimeEventActive)
        {
            return;
        }

        if (_totalCardsPlayed < _nextEventAnsweredQuestions)
        {
            return;
        }

        int minStep = GetAdjustedEventQuestionStep(_runtimeMinQuestionsBetweenEvents);
        int maxStep = Mathf.Max(minStep, GetAdjustedEventQuestionStep(_runtimeMaxQuestionsBetweenEvents));
        _nextEventAnsweredQuestions += UnityEngine.Random.Range(minStep, maxStep + 1);

        if (!guaranteeEventOnQuestionMilestone && UnityEngine.Random.value > GetAdjustedEventTriggerChance())
        {
            if (debugRuntimeEvents)
            {
                Debug.Log("[GameManager] Runtime event roll missed by chance.");
            }
            return;
        }

        GameplayRandomEventType selectedType = SelectRandomEventType();
        if (debugRuntimeEvents)
        {
            Debug.Log($"[GameManager] Runtime event triggered after answered {_totalCardsPlayed}/{_currentSessionCardCount}. Next at {_nextEventAnsweredQuestions}. Type={selectedType}");
        }
        StartRuntimeEvent(selectedType);
    }

    private void ConfigureRuntimeEventScheduleForCurrentMode()
    {
        if (usePerModeFixedQuestionInterval)
        {
            int fixedInterval = Mathf.Max(1, GetFixedQuestionIntervalByMode());
            _nextEventAnsweredQuestions = fixedInterval;
            _runtimeMinQuestionsBetweenEvents = fixedInterval;
            _runtimeMaxQuestionsBetweenEvents = fixedInterval;

            if (debugRuntimeEvents)
            {
                Debug.Log($"[GameManager] Runtime events fixed interval enabled. Mode={GameRules.SelectedMode}, interval={fixedInterval}");
            }

            return;
        }

        _nextEventAnsweredQuestions = Mathf.Max(1, firstEventAfterAnsweredQuestions);
        _runtimeMinQuestionsBetweenEvents = Mathf.Max(1, minAnsweredQuestionsBetweenEvents);
        _runtimeMaxQuestionsBetweenEvents = Mathf.Max(_runtimeMinQuestionsBetweenEvents, maxAnsweredQuestionsBetweenEvents);

        if (debugRuntimeEvents)
        {
            Debug.Log($"[GameManager] Runtime events random interval enabled. first={_nextEventAnsweredQuestions}, min={_runtimeMinQuestionsBetweenEvents}, max={_runtimeMaxQuestionsBetweenEvents}");
        }
    }

    private int GetFixedQuestionIntervalByMode()
    {
        switch (GameRules.SelectedMode)
        {
            case GameRuleMode.TimeLimit3Min:
                return fixedIntervalTimeLimit3Min;
            case GameRuleMode.TimeLimit5Min:
                return fixedIntervalTimeLimit5Min;
            case GameRuleMode.SpeedRunMax5Min:
            default:
                return fixedIntervalSpeedRun;
        }
    }

    private GameplayRandomEventType SelectRandomEventType()
    {
        int index = UnityEngine.Random.Range(0, 4);
        switch (index)
        {
            case 0:
                return GameplayRandomEventType.BonusScoreX2;
            case 1:
                return GameplayRandomEventType.SpeedBoost;
            case 2:
                return GameplayRandomEventType.TimeReduction;
            case 3:
            default:
                return GameplayRandomEventType.ObstacleBurst;
        }
    }

    private int GetAdjustedEventQuestionStep(int baseStep)
    {
        float frequency = Mathf.Max(0.25f, eventFrequencyMultiplier);
        int adjustedStep = Mathf.RoundToInt(Mathf.Max(1, baseStep) / frequency);
        return Mathf.Max(1, adjustedStep);
    }

    private float GetAdjustedEventTriggerChance()
    {
        float frequency = Mathf.Max(0.25f, eventFrequencyMultiplier);
        return Mathf.Clamp01(baseEventTriggerChance * frequency);
    }

    private void StartRuntimeEvent(GameplayRandomEventType eventType)
    {
        if (eventType == GameplayRandomEventType.None)
        {
            return;
        }

        EndRuntimeEvent(announceStateChange: false);

        _isRuntimeEventActive = true;
        _activeRuntimeEventType = eventType;

        float eventDuration = eventType == GameplayRandomEventType.ObstacleBurst
            ? obstacleEventDurationSeconds
            : defaultEventDurationSeconds;
        _activeEventRemainingSeconds = Mathf.Max(1f, eventDuration);

        switch (eventType)
        {
            case GameplayRandomEventType.BonusScoreX2:
                _scoreGainMultiplier = Mathf.Max(1f, bonusScoreMultiplier);
                break;
            case GameplayRandomEventType.SpeedBoost:
                GameRules.SetSwipeAnimationSpeedMultiplier(speedBoostMultiplier);
                break;
            case GameplayRandomEventType.TimeReduction:
                _timerDrainMultiplier = Mathf.Max(1f, timeReductionDrainMultiplier);
                break;
            case GameplayRandomEventType.ObstacleBurst:
                break;
        }

        GameEvents.OnRandomEventStateChanged?.Invoke(new GameplayRandomEventState
        {
            eventType = eventType,
            eventName = GetRuntimeEventName(eventType),
            isActive = true,
            durationSeconds = _activeEventRemainingSeconds,
            obstacleCount = Mathf.Max(1, obstacleSpawnCount)
        });

        if (debugRuntimeEvents)
        {
            Debug.Log($"[GameManager] Runtime event START: {eventType}, duration={_activeEventRemainingSeconds:0.00}s");
        }
    }

    private void TickRuntimeEvent()
    {
        if (!_isRuntimeEventActive || _hasEnded)
        {
            return;
        }

        _activeEventRemainingSeconds -= Time.deltaTime;
        if (_activeEventRemainingSeconds <= 0f)
        {
            EndRuntimeEvent(announceStateChange: true);
        }
    }

    private void EndRuntimeEvent(bool announceStateChange)
    {
        GameplayRandomEventType previousType = _activeRuntimeEventType;

        _isRuntimeEventActive = false;
        _activeEventRemainingSeconds = 0f;
        _activeRuntimeEventType = GameplayRandomEventType.None;
        _scoreGainMultiplier = 1f;
        _timerDrainMultiplier = 1f;
        GameRules.SetSwipeAnimationSpeedMultiplier(1f);

        if (!announceStateChange)
        {
            return;
        }

        GameEvents.OnRandomEventStateChanged?.Invoke(new GameplayRandomEventState
        {
            eventType = previousType,
            eventName = string.Empty,
            isActive = false,
            durationSeconds = 0f,
            obstacleCount = 0
        });

        if (debugRuntimeEvents)
        {
            Debug.Log($"[GameManager] Runtime event END: {previousType}");
        }
    }

    private string GetRuntimeEventName(GameplayRandomEventType eventType)
    {
        switch (eventType)
        {
            case GameplayRandomEventType.BonusScoreX2:
                return "Bonus diem x2";
            case GameplayRandomEventType.SpeedBoost:
                return "Tang toc";
            case GameplayRandomEventType.TimeReduction:
                return "Giam thoi gian";
            case GameplayRandomEventType.ObstacleBurst:
                return "Obstacle xuat hien";
            default:
                return string.Empty;
        }
    }
}
