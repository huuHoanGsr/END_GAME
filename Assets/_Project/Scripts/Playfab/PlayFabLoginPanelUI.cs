using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Minigame;

public class PlayFabLoginPanelUI : MonoBehaviour
{
    [Header("Dependencies")]
    [SerializeField] private PlayFabAuthManager authManager;
    [SerializeField] private Minigame.UIManager uiManager;
    [SerializeField] private GameObject loginPanelRoot;

    [Header("Login/Profile UI")]
    [SerializeField] private TMP_InputField nameInput;
    [SerializeField] private TMP_InputField phoneInput;
    [SerializeField] private TMP_InputField emailInput;
    [SerializeField] private Button loginButton;
    [SerializeField] private Button submitProfileButton;
    [SerializeField] private Text statusText;

    private void Awake()
    {
        if (authManager == null)
        {
            authManager = FindObjectOfType<PlayFabAuthManager>();
        }

        if (uiManager == null)
        {
            uiManager = FindObjectOfType<Minigame.UIManager>();
        }

        if (loginPanelRoot == null)
        {
            loginPanelRoot = gameObject;
        }

        if (loginButton != null)
        {
            loginButton.onClick.RemoveListener(HandleLoginClicked);
            loginButton.onClick.AddListener(HandleLoginClicked);
        }

        if (submitProfileButton != null)
        {
            submitProfileButton.onClick.RemoveListener(HandleSubmitProfileClicked);
            submitProfileButton.onClick.AddListener(HandleSubmitProfileClicked);
        }

        SetStatus("Sẵn sàng đăng nhập.");
        RefreshButtonState();
    }

    private void OnEnable()
    {
        if (authManager == null)
        {
            return;
        }

        authManager.OnLoginSucceeded += HandleLoginSucceeded;
        authManager.OnLoginFailed += HandleLoginFailed;
        authManager.OnProfileSubmitted += HandleProfileSubmitted;
        authManager.OnProfileSubmitFailed += HandleProfileSubmitFailed;
    }

    private void OnDisable()
    {
        if (authManager == null)
        {
            return;
        }

        authManager.OnLoginSucceeded -= HandleLoginSucceeded;
        authManager.OnLoginFailed -= HandleLoginFailed;
        authManager.OnProfileSubmitted -= HandleProfileSubmitted;
        authManager.OnProfileSubmitFailed -= HandleProfileSubmitFailed;
    }

    private void HandleLoginClicked()
    {
        if (authManager == null)
        {
            SetStatus("Thiếu PlayFabAuthManager trong scene.");
            return;
        }

        SetStatus("Đang đăng nhập PlayFab...");
        authManager.Login();
    }

    private void HandleSubmitProfileClicked()
    {
        if (authManager == null)
        {
            SetStatus("Thiếu PlayFabAuthManager trong scene.");
            return;
        }

        string studentName = nameInput != null ? nameInput.text.Trim() : string.Empty;
        string phone = phoneInput != null ? phoneInput.text.Trim() : string.Empty;
        string email = emailInput != null ? emailInput.text.Trim() : string.Empty;

        if (string.IsNullOrEmpty(studentName) || string.IsNullOrEmpty(phone) || string.IsNullOrEmpty(email))
        {
            SetStatus("Vui lòng nhập đủ Họ tên, SĐT, Email.");
            return;
        }

        SetStatus("Đang gửi thông tin sinh viên...");
        authManager.SubmitProfile(studentName, phone, email);
    }

    private void HandleLoginSucceeded()
    {
        SetStatus("Đăng nhập thành công.");
        RefreshButtonState();
    }

    private void HandleLoginFailed(string error)
    {
        SetStatus("Đăng nhập thất bại: " + error);
        RefreshButtonState();
    }

    private void HandleProfileSubmitted()
    {
        SetStatus("Đã cập nhật hồ sơ sinh viên.");
        if (uiManager != null)
        {
            uiManager.ShowMenu();
        }
        if (loginPanelRoot != null)
        {
            loginPanelRoot.SetActive(false);
        }
    }

    private void HandleProfileSubmitFailed(string error)
    {
        SetStatus("Gửi hồ sơ thất bại: " + error);
    }

    private void RefreshButtonState()
    {
        bool loggedIn = authManager != null && authManager.IsLoggedIn;

        if (submitProfileButton != null)
        {
            submitProfileButton.interactable = loggedIn;
        }
    }

    private void SetStatus(string value)
    {
        if (statusText != null)
        {
            statusText.text = value;
        }

        Debug.Log("[PlayFabLoginPanelUI] " + value);
    }
}
