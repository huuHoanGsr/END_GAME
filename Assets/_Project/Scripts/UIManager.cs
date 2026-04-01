using System.Runtime.Serialization;
using UnityEngine;
using UnityEngine.UI;

public class UIManager : MonoBehaviour
{
    [Header("Panels - Dùng CanvasGroup để tối ưu")]
    [SerializeField] private CanvasGroup mainMenuPanel;
    [SerializeField] private CanvasGroup gameplayPanel;
    [SerializeField] private CanvasGroup resultPanel;
    [SerializeField] private CanvasGroup losePanel;

    [Header("Gameplay UI")]
    [SerializeField] private Transform cardArea; // Nơi thẻ bài sinh ra
    [SerializeField] private GameObject cardPrefab; // Nhớ gắn script SwipeCard vào prefab này
    [SerializeField] private CanvasGroup likeHint;
    [SerializeField] private CanvasGroup skipHint;
    [SerializeField] private RectTransform likeStampTransform;
    [SerializeField] private RectTransform skipStampTransform;
    [SerializeField] private Text countdownText;

    [Header("Result UI")]
    [SerializeField] private Text categoryResultText;
    [SerializeField] private Image topMajorImage;
    [SerializeField] private Slider topMajorPercentSlider;
    [SerializeField] public Button settingButton;
    [Header("Lose UI")]
    [SerializeField] private Text loseMessageText;

    private void OnEnable()
    {
        GameEvents.OnMainMenuEntered += ShowMainMenu;
        GameEvents.OnGameplayEntered += ShowGameplay;
        GameEvents.OnGameEnded += ShowResult;
        GameEvents.OnGameLost += ShowLose;
        GameEvents.OnTimerUpdated += UpdateTimerText;
        
        GameEvents.OnCardSpawned += SpawnNewCard;
    }

    private void OnDisable()
    {
        GameEvents.OnMainMenuEntered -= ShowMainMenu;
        GameEvents.OnGameplayEntered -= ShowGameplay;
        GameEvents.OnGameEnded -= ShowResult;
        GameEvents.OnGameLost -= ShowLose;
        GameEvents.OnTimerUpdated -= UpdateTimerText;
        
        GameEvents.OnCardSpawned -= SpawnNewCard;
    }

    // Các hàm xử lý chuyển State UI
    private void ShowMainMenu()
    {
        SetSettingButtonActive(true);
        SwitchPanel(mainMenuPanel);
        SetTimerText(string.Empty);
    }

    private void ShowGameplay()
    {
        SetSettingButtonActive(false);
        SwitchPanel(gameplayPanel);
    }
    private void ShowResult(GameResultData result) 
    { 
        SetSettingButtonActive(false);
        SwitchPanel(resultPanel); 
        SetTimerText(string.Empty);

        if (categoryResultText != null)
        {
            categoryResultText.text = result.topMajorName;
        }

        if (topMajorImage != null)
        {
            topMajorImage.sprite = result.topMajorImage;
            topMajorImage.enabled = result.topMajorImage != null;
        }

        if (topMajorPercentSlider != null)
        {
            topMajorPercentSlider.minValue = 0f;
            topMajorPercentSlider.maxValue = 100f;
            topMajorPercentSlider.value = Mathf.Clamp(result.topMajorPercent, 0, 100);
        }
    }

    private void ShowLose()
    {
        SetSettingButtonActive(false);
        SwitchPanel(losePanel);
        SetTimerText(string.Empty);

        if (loseMessageText != null)
        {
            loseMessageText.text = "Het gio. Ban da thua!";
        }
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
        SetPanelActive(losePanel, false);

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

    public void OnRestartButtonClicked()
    {
        GameEvents.OnRestartRequested?.Invoke();
    }

    public void OnMainMenuButtonClicked()
    {
        GameEvents.OnMainMenuRequested?.Invoke();
    }
}