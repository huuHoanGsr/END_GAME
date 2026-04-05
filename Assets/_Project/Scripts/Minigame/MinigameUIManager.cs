using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Minigame
{
    public class UIManager : MonoBehaviour
    {
        public GameObject menuPanel;
        public GameObject gamePanel;
        public GameObject resultPanel;
        [Header("Menu Background")]
        [SerializeField] private GameObject menuBackgroundPanel;
        [Header("Scene Navigation")]
        [SerializeField] private string mainGameSceneName = "MainGameplay";
        [Header("PlayFab Minigame")]
        [SerializeField] private PlayFabLeaderboard leaderboardManager;
        [SerializeField] private PlayFabLeaderboardUIToolkitController leaderboardUIToolkit;
        [SerializeField] private bool autoSubmitScoreToLeaderboard = true;
        public Text scoreText;
        public Text timeText;
        public Text finalScoreText;
        [Header("HP UI")]
        [SerializeField] private Slider hpSlider;

        private int _lastResultScore;

        private void Awake()
        {
            if (leaderboardManager == null)
            {
                leaderboardManager = FindObjectOfType<PlayFabLeaderboard>();
            }

            if (leaderboardUIToolkit == null)
            {
                leaderboardUIToolkit = FindObjectOfType<PlayFabLeaderboardUIToolkitController>();
            }

            EnsureHpSlider();
        }

        // Legacy API cho nút cũ: giữ lại để không vỡ nút cũ
        public void OnStartGameFromDropdown()
        {
            GameManager.Instance.StartGame();
        }

        public void StartGame()
        {
            if (menuBackgroundPanel != null)
            {
                menuBackgroundPanel.SetActive(false);
            }

            if (menuPanel != null)
            {
                menuPanel.SetActive(false);
            }

            GameManager.Instance.StartGame();
        }

        public void ShowMenu()
        {
            menuPanel.SetActive(true);
            gamePanel.SetActive(false);
            resultPanel.SetActive(false);
            if (leaderboardUIToolkit != null)
            {
                leaderboardUIToolkit.HidePanel();
            }
        }
        public void ShowGame()
        {
            menuPanel.SetActive(false);
            gamePanel.SetActive(true);
            resultPanel.SetActive(false);
            if (leaderboardUIToolkit != null)
            {
                leaderboardUIToolkit.HidePanel();
            }
            UpdateScore(0);
        }


        public void ShowResult(int score)
        {
            _lastResultScore = score;
            menuPanel.SetActive(false);
            gamePanel.SetActive(false);
            resultPanel.SetActive(true);
            if (finalScoreText != null)
            {
                finalScoreText.text = "Điểm: " + score;
            }

            if (autoSubmitScoreToLeaderboard)
            {
                SubmitScoreToLeaderboard(score);
            }

            if (leaderboardUIToolkit != null)
            {
                leaderboardUIToolkit.HidePanel();
            }
        }

        // Gọi từ nút trên result panel
        public void ShowLeaderboardPanel()
        {
            if (leaderboardUIToolkit != null)
            {
                leaderboardUIToolkit.ShowPanel();
            }

            resultPanel.SetActive(false);
        }

        public void HideLeaderboardPanel()
        {
            if (leaderboardUIToolkit != null)
            {
                leaderboardUIToolkit.HidePanel();
            }

            resultPanel.SetActive(true);
        }

        public void SubmitScoreToLeaderboard(int score)
        {
            if (leaderboardManager != null)
            {
                leaderboardManager.SubmitScore(score);
            }
        }

        public void RetrySubmitLastScore()
        {
            SubmitScoreToLeaderboard(_lastResultScore);
        }

        public void UpdateScore(int score)
        {
            if (scoreText != null)
            {
                scoreText.text = "Điểm: " + score;
            }
        }

        public void UpdateTime(float timeLeft)
        {
            int t = Mathf.CeilToInt(Mathf.Max(0f, timeLeft));
            if (timeText != null)
            {
                timeText.text = "Thời gian: " + t + "s";
            }
        }

        public void UpdateHp(int current, int max)
        {
            EnsureHpSlider();
            if (hpSlider == null)
            {
                return;
            }

            hpSlider.minValue = 0;
            hpSlider.maxValue = Mathf.Max(1, max);
            hpSlider.value = Mathf.Clamp(current, 0, max);
        }

        private void EnsureHpSlider()
        {
            if (hpSlider != null)
            {
                return;
            }

            if (gamePanel == null)
            {
                return;
            }

            // Create a simple top-right HP slider under gamePanel.
            GameObject sliderRoot = new GameObject("HpSlider", typeof(RectTransform), typeof(Slider));
            sliderRoot.transform.SetParent(gamePanel.transform, false);

            RectTransform sliderRect = sliderRoot.GetComponent<RectTransform>();
            sliderRect.anchorMin = new Vector2(1f, 1f);
            sliderRect.anchorMax = new Vector2(1f, 1f);
            sliderRect.pivot = new Vector2(1f, 1f);
            sliderRect.anchoredPosition = new Vector2(-16f, -16f);
            sliderRect.sizeDelta = new Vector2(200f, 20f);

            GameObject background = new GameObject("Background", typeof(RectTransform), typeof(Image));
            background.transform.SetParent(sliderRoot.transform, false);
            Image backgroundImage = background.GetComponent<Image>();
            backgroundImage.color = new Color(0f, 0f, 0f, 0.4f);
            RectTransform backgroundRect = background.GetComponent<RectTransform>();
            backgroundRect.anchorMin = new Vector2(0f, 0f);
            backgroundRect.anchorMax = new Vector2(1f, 1f);
            backgroundRect.offsetMin = Vector2.zero;
            backgroundRect.offsetMax = Vector2.zero;

            GameObject fillArea = new GameObject("Fill Area", typeof(RectTransform));
            fillArea.transform.SetParent(sliderRoot.transform, false);
            RectTransform fillAreaRect = fillArea.GetComponent<RectTransform>();
            fillAreaRect.anchorMin = new Vector2(0f, 0f);
            fillAreaRect.anchorMax = new Vector2(1f, 1f);
            fillAreaRect.offsetMin = new Vector2(4f, 4f);
            fillAreaRect.offsetMax = new Vector2(-4f, -4f);

            GameObject fill = new GameObject("Fill", typeof(RectTransform), typeof(Image));
            fill.transform.SetParent(fillArea.transform, false);
            Image fillImage = fill.GetComponent<Image>();
            fillImage.color = new Color(0.9f, 0.2f, 0.2f, 1f);
            RectTransform fillRect = fill.GetComponent<RectTransform>();
            fillRect.anchorMin = new Vector2(0f, 0f);
            fillRect.anchorMax = new Vector2(1f, 1f);
            fillRect.offsetMin = Vector2.zero;
            fillRect.offsetMax = Vector2.zero;

            Slider slider = sliderRoot.GetComponent<Slider>();
            slider.transition = Selectable.Transition.None;
            slider.targetGraphic = fillImage;
            slider.fillRect = fillRect;
            slider.direction = Slider.Direction.LeftToRight;
            slider.minValue = 0;
            slider.maxValue = 3;
            slider.value = 3;

            hpSlider = slider;
        }

        // Gọi từ nút menu
        public void OnStart3Min()
        {
            GameManager.Instance.StartGame();
        }
        public void OnStart5Min()
        {
            GameManager.Instance.StartGame();
        }
        public void OnStartFinishEarly()
        {
            GameManager.Instance.StartGame();
        }
        public void OnBackToMenu()
        {
            ShowMenu();
        }

        public void OnBackToMainGame()
        {
            if (string.IsNullOrWhiteSpace(mainGameSceneName))
            {
                Debug.LogWarning("[MinigameUIManager] Main game scene name is empty.");
                return;
            }

            SceneManager.LoadScene(mainGameSceneName);
        }
    }
}