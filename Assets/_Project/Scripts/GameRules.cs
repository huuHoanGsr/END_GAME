using UnityEngine;

public enum GameRuleMode
{
    TimeLimit3Min = 0,
    TimeLimit5Min = 1,
    SpeedRunMax5Min = 2
}

public static class GameRules
{
    public const float TimeLimit3Seconds = 180f;
    public const float TimeLimit5Seconds = 300f;

    private static GameRuleMode _selectedMode = GameRuleMode.TimeLimit5Min;

    public static GameRuleMode SelectedMode => _selectedMode;

    public static void SetMode(GameRuleMode mode)
    {
        _selectedMode = mode;
    }

    public static float GetTimeLimitSeconds()
    {
        switch (_selectedMode)
        {
            case GameRuleMode.TimeLimit3Min:
                return TimeLimit3Seconds;
            case GameRuleMode.TimeLimit5Min:
            case GameRuleMode.SpeedRunMax5Min:
            default:
                return TimeLimit5Seconds;
        }
    }
}
