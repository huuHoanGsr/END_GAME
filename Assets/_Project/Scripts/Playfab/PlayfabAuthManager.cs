using PlayFab;
using PlayFab.ClientModels;
using UnityEngine;
using System.Collections.Generic;
using System;

public class PlayFabAuthManager : MonoBehaviour
{
    private const int MinDisplayNameLength = 3;
    private const int MaxDisplayNameLength = 25;
    private const string DeviceIdKey = "DeviceId";

    [Header("UI References (Gán từ Inspector)")]
    public string studentName;
    public string phoneNumber;
    public string email;

    public bool IsLoggedIn { get; private set; }
    public string PlayFabId { get; private set; }

    public event Action OnLoginSucceeded;
    public event Action<string> OnLoginFailed;
    public event Action OnProfileSubmitted;
    public event Action<string> OnProfileSubmitFailed;

    void Start()
    {
        if (PlayFabSessionState.IsLoggedIn)
        {
            IsLoggedIn = true;
            PlayFabId = PlayFabSessionState.PlayFabId;
            OnLoginSucceeded?.Invoke();
            return;
        }

        Login();
    }

    // 1. Đăng nhập ẩn danh
    public void Login()
    {
        if (PlayFabSessionState.IsLoggedIn)
        {
            IsLoggedIn = true;
            PlayFabId = PlayFabSessionState.PlayFabId;
            OnLoginSucceeded?.Invoke();
            return;
        }

        if (PlayFabSessionState.IsLoginInProgress)
        {
            PlayFabSessionState.OnSessionLoggedIn -= HandleSharedSessionLoggedIn;
            PlayFabSessionState.OnSessionLoggedIn += HandleSharedSessionLoggedIn;
            return;
        }

        PlayFabSessionState.BeginLogin();

        LoginWithCustomId(true);
    }

    private void HandleSharedSessionLoggedIn(string playFabId)
    {
        PlayFabSessionState.OnSessionLoggedIn -= HandleSharedSessionLoggedIn;
        IsLoggedIn = true;
        PlayFabId = playFabId;
        OnLoginSucceeded?.Invoke();
    }

    private void LoginWithCustomId(bool createAccount)
    {
        var request = new LoginWithCustomIDRequest
        {
            CustomId = SystemInfo.deviceUniqueIdentifier, // Lấy ID máy làm tài khoản
            CreateAccount = createAccount
        };

        PlayFabClientAPI.LoginWithCustomID(request, OnLoginSuccess, error =>
        {
            // Trường hợp nhiều luồng cùng CreateAccount=true có thể trả về 409. Retry đăng nhập tài khoản đã có.
            if (createAccount && error != null && error.HttpCode == 409)
            {
                LoginWithCustomId(false);
                return;
            }

            IsLoggedIn = false;
            PlayFabSessionState.EndLogin();
            string message = error.GenerateErrorReport();
            OnLoginFailed?.Invoke(message);
            OnError(error);
        });
    }

    void OnLoginSuccess(LoginResult result)
    {
        IsLoggedIn = true;
        PlayFabId = result.PlayFabId;
        PlayFabSessionState.SetLoggedIn(result.PlayFabId);
        Debug.Log("Đã đăng nhập vào PlayFab!");
        OnLoginSucceeded?.Invoke();
        // Nếu là tài khoản mới, bạn có thể gọi hàm cập nhật thông tin ngay
        // SubmitProfile("Nguyen Van A", "0905123456", "anv@fpt.edu.vn");
    }

    // 2. Cập nhật thông tin Sinh viên (Yêu cầu số 5)
    public void SubmitProfile(string name, string phone, string mail)
    {
        if (!IsLoggedIn)
        {
            OnProfileSubmitFailed?.Invoke("Chưa đăng nhập PlayFab.");
            return;
        }

        string sanitizedName = SanitizeName(name);
        string sanitizedPhone = phone == null ? string.Empty : phone.Trim();
        string sanitizedMail = mail == null ? string.Empty : mail.Trim();

        if (sanitizedName.Length < MinDisplayNameLength || sanitizedName.Length > MaxDisplayNameLength)
        {
            OnProfileSubmitFailed?.Invoke($"Tên hiển thị phải từ {MinDisplayNameLength} đến {MaxDisplayNameLength} ký tự.");
            return;
        }

        UpdateDisplayNameOrKeepExisting(sanitizedName, sanitizedPhone, sanitizedMail);
    }

    void OnError(PlayFabError error)
    {
        Debug.LogError("Lỗi: " + error.GenerateErrorReport());
    }

    private string SanitizeName(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        string trimmed = value.Trim();
        while (trimmed.Contains("  "))
        {
            trimmed = trimmed.Replace("  ", " ");
        }

        return trimmed;
    }

    private void UpdateDisplayNameOrKeepExisting(string desiredName, string phone, string mail)
    {
        var nameRequest = new UpdateUserTitleDisplayNameRequest { DisplayName = desiredName };
        PlayFabClientAPI.UpdateUserTitleDisplayName(nameRequest, _ =>
        {
            Debug.Log("Đã cập nhật Họ Tên");
            UpdateUserData(phone, mail);
        }, error =>
        {
            if (error != null && error.Error == PlayFabErrorCode.NameNotAvailable)
            {
                // Tên đã tồn tại: nếu Email + SĐT đúng thì chỉ cập nhật dữ liệu, giữ nguyên tên hiện tại.
                Debug.Log("Tên đã tồn tại, giữ nguyên tên cũ và cập nhật dữ liệu.");
                UpdateUserData(phone, mail);
                return;
            }

            string message = error.GenerateErrorReport();
            OnProfileSubmitFailed?.Invoke(message);
            OnError(error);
        });
    }

    private void UpdateUserData(string phone, string mail)
    {
        var dataRequest = new UpdateUserDataRequest
        {
            Data = new Dictionary<string, string> {
                { "Phone", phone },
                { "Email", mail },
                { "Campus", "FPT Poly Da Nang" },
                { DeviceIdKey, SystemInfo.deviceUniqueIdentifier }
            },
            Permission = UserDataPermission.Public
        };

        PlayFabClientAPI.UpdateUserData(dataRequest, _ =>
        {
            Debug.Log("Đã lưu SĐT và Email lên Server!");
            OnProfileSubmitted?.Invoke();
        }, error =>
        {
            string message = error.GenerateErrorReport();
            OnProfileSubmitFailed?.Invoke(message);
            OnError(error);
        });
    }

}