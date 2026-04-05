using System;

[Serializable]
public struct GameResultData
{
    public int finalScore;
}

public enum GameplayRandomEventType
{
    None = 0,
    BonusScoreX2 = 1,
    SpeedBoost = 2,
    TimeReduction = 3,
    ObstacleBurst = 4
}

[Serializable]
public struct GameplayRandomEventState
{
    public GameplayRandomEventType eventType;
    public string eventName;
    public bool isActive;
    public float durationSeconds;
    public int obstacleCount;
}

public static class GameEvents
{
    // Quản lý Trạng thái Game (State Pattern cơ bản)
    public static Action OnMainMenuEntered;
    public static Action OnGameplayEntered;
    public static Action<GameResultData> OnGameEnded;

    // Quản lý Gameplay
    public static Action<QuestionCardSO> OnCardSpawned; // Khi một thẻ mới được tạo
    public static Action<int, int> OnProgressUpdated;   // Cập nhật text "1/20"
    public static Action<int> OnScoreUpdated;           // Cập nhật điểm realtime trong gameplay
    public static Action<float, float> OnTimerUpdated;  // remainingSeconds, totalSeconds
    public static Action<GameplayRandomEventState> OnRandomEventStateChanged; // Trạng thái event runtime
    
    // Sự kiện từ UI gửi về Logic
    public static Action<bool> OnCardSwiped; // true = Phải (Thích), false = Trái (Bỏ qua)
    public static Action OnRestartRequested;
    public static Action OnMainMenuRequested;
}