using System;

public static class PlayFabSessionState
{
    public static bool IsLoggedIn { get; private set; }
    public static string PlayFabId { get; private set; }
    public static bool IsLoginInProgress { get; private set; }

    public static event Action<string> OnSessionLoggedIn;

    public static void BeginLogin()
    {
        IsLoginInProgress = true;
    }

    public static void EndLogin()
    {
        IsLoginInProgress = false;
    }

    public static void SetLoggedIn(string playFabId)
    {
        IsLoggedIn = true;
        IsLoginInProgress = false;
        PlayFabId = playFabId;
        OnSessionLoggedIn?.Invoke(playFabId);
    }

    public static void Clear()
    {
        IsLoggedIn = false;
        IsLoginInProgress = false;
        PlayFabId = null;
    }
}
