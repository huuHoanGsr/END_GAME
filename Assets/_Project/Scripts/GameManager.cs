using System.Collections.Generic;
using UnityEngine;
using System.Linq; // Cực kỳ quan trọng để dùng thuật toán sắp xếp tìm Top 1
using System.Globalization;
using System.Text;

public class GameManager : MonoBehaviour
{
    [System.Serializable]
    private struct MajorVisualMapping
    {
        public string majorKey;
        public Sprite majorImage;
    }

    [Header("Data Source")]
    [SerializeField] private QuestionDatabaseSO questionDatabase;

    [Header("Result Visual Mapping")]
    [SerializeField] private List<MajorVisualMapping> majorVisualMappings = new List<MajorVisualMapping>();

    [Header("Gameplay Config")]
    [SerializeField] private int _targetCardCount = 20;

    // Danh sách chuẩn 7 nhóm ngành theo tài liệu của bạn
    private readonly string[] MAIN_MAJORS = {
        "IT", "Kỹ thuật", "Kinh tế", 
        "Du lịch - Khách sạn", "Ngôn ngữ", 
        "Thiết kế đồ họa", "Dược"
    };

    // Bảng điểm hiện tại của người chơi
    private Dictionary<string, int> _scores = new Dictionary<string, int>();
    private Dictionary<string, string> _majorAliasToKey = new Dictionary<string, string>();
    private Dictionary<string, Sprite> _majorSprites = new Dictionary<string, Sprite>();
    private List<QuestionCardSO> deckOfCards = new List<QuestionCardSO>();
    private List<QuestionCardSO> _runtimeQuestionCards = new List<QuestionCardSO>();
    private int _currentCardIndex;
    private int _totalCardsPlayed;
    private float _timeLimitSeconds;
    private float _elapsedSeconds;
    private int _lastReportedSeconds;
    private bool _isTimerActive;
    private bool _hasEnded;

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

    private void Awake()
    {
        // Khởi tạo bảng điểm với giá trị 0 cho tất cả 7 ngành
        foreach (string major in MAIN_MAJORS)
        {
            _scores.Add(major, 0);
        }

        BuildMajorAliasMap();
        BuildMajorSpriteMap();
    }

    private void Start()
    {
        GameEvents.OnMainMenuEntered?.Invoke();
    }

    public void StartGame()
    {
        _hasEnded = false;
        _elapsedSeconds = 0f;
        _timeLimitSeconds = GameRules.GetTimeLimitSeconds();
        _lastReportedSeconds = Mathf.CeilToInt(_timeLimitSeconds);
        _isTimerActive = true;

        foreach (string major in MAIN_MAJORS)
        {
            _scores[major] = 0;
        }

        if (_runtimeQuestionCards != null && _runtimeQuestionCards.Count > 0)
        {
            deckOfCards = new List<QuestionCardSO>(_runtimeQuestionCards);
        }
        else
        {
            deckOfCards = questionDatabase != null && questionDatabase.defaultQuestions != null
                ? new List<QuestionCardSO>(questionDatabase.defaultQuestions)
                : new List<QuestionCardSO>();
        }

        if (deckOfCards.Count == 0)
        {
            Debug.LogWarning("[GameManager] Không có thẻ câu hỏi runtime hoặc trong QuestionDatabaseSO.");
            GameEvents.OnGameEnded?.Invoke(new GameResultData
            {
                topMajorName = "Chưa có dữ liệu câu hỏi",
                topMajorPercent = 0,
                topMajorImage = null
            });
            return;
        }

        _targetCardCount = Mathf.Max(1, Mathf.Min(_targetCardCount, deckOfCards.Count));
        _totalCardsPlayed = 0;
        _currentCardIndex = 0;

        GameEvents.OnGameplayEntered?.Invoke();
        GameEvents.OnTimerUpdated?.Invoke(_timeLimitSeconds, _timeLimitSeconds);
        SpawnNextCard();
    }

    public void UseGeminiQuestions(List<QuestionData> generatedQuestions)
    {
        SetRuntimeQuestionCards(GeminiQuestionCardAdapter.Convert(generatedQuestions));
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
        GameEvents.OnMainMenuEntered?.Invoke();
    }

    private void SpawnNextCard()
    {
        if (_hasEnded)
        {
            return;
        }

        if (_currentCardIndex >= deckOfCards.Count)
        {
            EndGame();
            return;
        }

        GameEvents.OnProgressUpdated?.Invoke(_totalCardsPlayed + 1, _targetCardCount);
        GameEvents.OnCardSpawned?.Invoke(deckOfCards[_currentCardIndex]);
    }

    // Hàm này gọi khi thẻ bị vuốt
    private void HandleCardSwiped(bool isRightSwipe)
    {
        if (_hasEnded)
        {
            return;
        }

        if (_currentCardIndex >= deckOfCards.Count)
        {
            return;
        }

        QuestionCardSO currentCard = deckOfCards[_currentCardIndex]; // Lấy thẻ hiện tại

        // Trích xuất list hiệu ứng tùy theo hướng vuốt
        List<SwipeEffect> effectsToApply = isRightSwipe ? currentCard.rightSwipeEffects : currentCard.leftSwipeEffects;

        // Cộng/Trừ điểm vào bảng điểm tổng
        foreach (var effect in effectsToApply)
        {
            string resolvedMajorKey = ResolveMajorKey(effect.majorID);

            if (!string.IsNullOrEmpty(resolvedMajorKey) && _scores.ContainsKey(resolvedMajorKey))
            {
                _scores[resolvedMajorKey] += effect.GetSignedPoints();
            }
            else
            {
                Debug.LogWarning($"[Data Lỗi] ID ngành '{effect.majorID}' không tồn tại. Yêu cầu Designer check lại lỗi chính tả trong ScriptableObject!");
            }
        }

        _totalCardsPlayed++;
        _currentCardIndex++;

        if (_totalCardsPlayed < _targetCardCount)
        {
            SpawnNextCard();
            return;
        }

        EndGame();
    }

    private void EndGame()
    {
        if (_hasEnded)
        {
            return;
        }

        _hasEnded = true;
        _isTimerActive = false;

        if (_scores.Count == 0)
        {
            GameEvents.OnGameEnded?.Invoke(new GameResultData
            {
                topMajorName = "Chưa có dữ liệu kết quả",
                topMajorPercent = 0,
                topMajorImage = null
            });
            return;
        }

        // 🌟 SỨC MẠNH CỦA LINQ: Sắp xếp bảng điểm từ Cao xuống Thấp
        var sortedScores = _scores.OrderByDescending(x => x.Value).ToList();

        // Ngành Top 1 là phần tử đầu tiên trong danh sách đã xếp
        string top1Major = sortedScores[0].Key;
        // --- TÍNH PHẦN TRĂM (%) CHO UI THANH TRƯỢT ---
        // Tổng điểm của Top 3 ngành (để tránh lỗi chia cho 0, ta dùng Mathf.Max)
        int totalTop3Score = Mathf.Max(1, sortedScores[0].Value + sortedScores[1].Value + sortedScores[2].Value);
        
        int percentTop1 = Mathf.RoundToInt(((float)sortedScores[0].Value / totalTop3Score) * 100f);
        int percentTop2 = Mathf.RoundToInt(((float)sortedScores[1].Value / totalTop3Score) * 100f);
        int percentTop3 = Mathf.RoundToInt(((float)sortedScores[2].Value / totalTop3Score) * 100f);

        Debug.Log($"Bạn sinh ra để làm: {top1Major} ({percentTop1}%)");
        Debug.Log($"Phương án dự phòng 1: {sortedScores[1].Key} ({percentTop2}%)");
        Debug.Log($"Phương án dự phòng 2: {sortedScores[2].Key} ({percentTop3}%)");

        GameEvents.OnGameEnded?.Invoke(new GameResultData
        {
            topMajorName = top1Major,
            topMajorPercent = percentTop1,
            topMajorImage = GetMajorImage(top1Major)
        });
    }

    private void Update()
    {
        if (!_isTimerActive || _hasEnded)
        {
            return;
        }

        _elapsedSeconds += Time.deltaTime;
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

        _hasEnded = true;
        _isTimerActive = false;
        GameEvents.OnTimerUpdated?.Invoke(0f, _timeLimitSeconds);
        GameEvents.OnGameLost?.Invoke();
    }

    private void BuildMajorSpriteMap()
    {
        _majorSprites.Clear();

        foreach (var mapping in majorVisualMappings)
        {
            string majorKey = ResolveMajorKey(mapping.majorKey);
            if (string.IsNullOrEmpty(majorKey))
            {
                continue;
            }

            _majorSprites[majorKey] = mapping.majorImage;
        }
    }

    private Sprite GetMajorImage(string majorKey)
    {
        if (string.IsNullOrEmpty(majorKey))
        {
            return null;
        }

        if (_majorSprites.TryGetValue(majorKey, out Sprite sprite))
        {
            return sprite;
        }

        return null;
    }

    private void BuildMajorAliasMap()
    {
        _majorAliasToKey.Clear();

        AddMajorAlias("1", "IT");
        AddMajorAlias("2", "Kỹ thuật");
        AddMajorAlias("3", "Kinh tế");
        AddMajorAlias("4", "Du lịch - Khách sạn");
        AddMajorAlias("5", "Ngôn ngữ");
        AddMajorAlias("6", "Thiết kế đồ họa");
        AddMajorAlias("7", "Dược");

        AddMajorAlias("it", "IT");
        AddMajorAlias("cntt", "IT");

        AddMajorAlias("ky thuat", "Kỹ thuật");
        AddMajorAlias("engineering", "Kỹ thuật");

        AddMajorAlias("kinh te", "Kinh tế");
        AddMajorAlias("business", "Kinh tế");

        AddMajorAlias("du lich khach san", "Du lịch - Khách sạn");
        AddMajorAlias("hospitality", "Du lịch - Khách sạn");
        AddMajorAlias("tourism", "Du lịch - Khách sạn");

        AddMajorAlias("ngon ngu", "Ngôn ngữ");
        AddMajorAlias("language", "Ngôn ngữ");

        AddMajorAlias("thiet ke do hoa", "Thiết kế đồ họa");
        AddMajorAlias("design", "Thiết kế đồ họa");
        AddMajorAlias("graphic design", "Thiết kế đồ họa");

        AddMajorAlias("duoc", "Dược");
        AddMajorAlias("pharmacy", "Dược");

        foreach (string major in MAIN_MAJORS)
        {
            AddMajorAlias(major, major);
        }
    }

    private void AddMajorAlias(string alias, string majorKey)
    {
        string normalizedAlias = NormalizeKey(alias);
        if (string.IsNullOrEmpty(normalizedAlias))
        {
            return;
        }

        _majorAliasToKey[normalizedAlias] = majorKey;
    }

    private string ResolveMajorKey(string rawMajor)
    {
        string normalized = NormalizeKey(rawMajor);
        if (string.IsNullOrEmpty(normalized))
        {
            return null;
        }

        if (_majorAliasToKey.TryGetValue(normalized, out string majorKey))
        {
            return majorKey;
        }

        return null;
    }

    private string NormalizeKey(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        string trimmed = value.Trim().ToLowerInvariant();
        string decomposed = trimmed.Normalize(NormalizationForm.FormD);
        StringBuilder builder = new StringBuilder(decomposed.Length);

        foreach (char c in decomposed)
        {
            UnicodeCategory category = CharUnicodeInfo.GetUnicodeCategory(c);
            if (category != UnicodeCategory.NonSpacingMark)
            {
                builder.Append(c);
            }
        }

        string normalized = builder
            .ToString()
            .Normalize(NormalizationForm.FormC)
            .Replace('-', ' ')
            .Replace('_', ' ');

        while (normalized.Contains("  "))
        {
            normalized = normalized.Replace("  ", " ");
        }

        return normalized;
    }
}