using UnityEngine;
using System.Collections.Generic;

public enum ScoreEffectType
{
    Cong,
    Tru
}

// Tạo một Struct để lưu trữ tác động của từng cú vuốt
[System.Serializable]
public struct SwipeEffect
{
    [Tooltip("ID hoặc tên ngành từ Excel. Hỗ trợ: 1-7, có/không dấu (VD: Ky thuat), hoặc tên chuẩn (VD: Kỹ thuật)")]
    public string majorID;

    [Tooltip("Chọn Cộng hoặc Trừ điểm")]
    public ScoreEffectType effectType;
    
    [Min(0)]
    [Tooltip("Giá trị điểm (luôn nhập số dương)")]
    public int points;

    public int GetSignedPoints()
    {
        int absolutePoints = Mathf.Abs(points);
        return effectType == ScoreEffectType.Cong ? absolutePoints : -absolutePoints;
    }
}

[CreateAssetMenu(fileName = "Question_ID-", menuName = "PolySwipe/Question Card")]
public class QuestionCardSO : ScriptableObject
{
    [TextArea(3, 5)]
    public string questionText;
    public Sprite backgroundImage;
    [Tooltip("URL ảnh nền tải runtime (http/https). Nếu có Sprite local thì Sprite local được ưu tiên.")]
    public string backgroundImageUrl;
    
    [Header("Khi Vuốt PHẢI (Đồng Ý / Thích)")]
    public List<SwipeEffect> rightSwipeEffects = new List<SwipeEffect>();

    [Header("Khi Vuốt TRÁI (Từ Chối / Bỏ Qua)")]
    public List<SwipeEffect> leftSwipeEffects = new List<SwipeEffect>();
}