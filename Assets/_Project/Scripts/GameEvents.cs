using System;
using UnityEngine;

[Serializable]
public struct GameResultData
{
    public string topMajorName;
    public int topMajorPercent;
    public Sprite topMajorImage;
}

public static class GameEvents
{
    // Quản lý Trạng thái Game (State Pattern cơ bản)
    public static Action OnMainMenuEntered;
    public static Action OnGameplayEntered;
    public static Action<GameResultData> OnGameEnded; // Truyền vào dữ liệu kết quả Top 1
    public static Action OnGameLost;

    // Quản lý Gameplay
    public static Action<QuestionCardSO> OnCardSpawned; // Khi một thẻ mới được tạo
    public static Action<int, int> OnProgressUpdated;   // Cập nhật text "1/20"
    public static Action<float, float> OnTimerUpdated;  // remainingSeconds, totalSeconds
    
    // Sự kiện từ UI gửi về Logic
    public static Action<bool> OnCardSwiped; // true = Phải (Thích), false = Trái (Bỏ qua)
    public static Action OnRestartRequested;
    public static Action OnMainMenuRequested;
}