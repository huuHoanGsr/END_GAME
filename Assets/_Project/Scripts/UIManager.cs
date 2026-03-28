using UnityEngine;
using UnityEngine.UI;

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

    [Header("Result UI")]
    [SerializeField] private Text categoryResultText;
    [SerializeField] private Image topMajorImage;
    [SerializeField] private Slider topMajorPercentSlider;

    private void OnEnable()
    {
        GameEvents.OnMainMenuEntered += ShowMainMenu;
        GameEvents.OnGameplayEntered += ShowGameplay;
        GameEvents.OnGameEnded += ShowResult;
        
        GameEvents.OnCardSpawned += SpawnNewCard;
    }

    private void OnDisable()
    {
        GameEvents.OnMainMenuEntered -= ShowMainMenu;
        GameEvents.OnGameplayEntered -= ShowGameplay;
        GameEvents.OnGameEnded -= ShowResult;
        
        GameEvents.OnCardSpawned -= SpawnNewCard;
    }

    // Các hàm xử lý chuyển State UI
    private void ShowMainMenu() { SwitchPanel(mainMenuPanel); }
    private void ShowGameplay() { SwitchPanel(gameplayPanel); }
    private void ShowResult(GameResultData result) 
    { 
        SwitchPanel(resultPanel); 

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
        panel.alpha = isActive ? 1f : 0f;
        panel.interactable = isActive;
        panel.blocksRaycasts = isActive;
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