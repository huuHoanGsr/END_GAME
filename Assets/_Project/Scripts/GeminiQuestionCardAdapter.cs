using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public static class GeminiQuestionCardAdapter
{
    public static List<QuestionCardSO> Convert(List<QuestionData> sourceQuestions)
    {
        List<QuestionCardSO> output = new List<QuestionCardSO>();
        if (sourceQuestions == null)
        {
            return output;
        }

        for (int i = 0; i < sourceQuestions.Count; i++)
        {
            QuestionData source = sourceQuestions[i];
            if (source == null || string.IsNullOrWhiteSpace(source.questionText))
            {
                continue;
            }

            QuestionCardSO card = ScriptableObject.CreateInstance<QuestionCardSO>();
            card.name = $"GeminiQuestion_{i + 1}";
            card.questionText = source.questionText.Trim();
            card.backgroundImage = null;
            card.backgroundImageUrl = source.imageUrl;
            card.rightSwipeEffects = ConvertEffects(source.rightSwipeEffects);
            card.leftSwipeEffects = ConvertEffects(source.leftSwipeEffects);

            output.Add(card);
        }

        return output;
    }

    public static List<QuestionCardSO> CloneCards(List<QuestionCardSO> sourceCards, int maxCount = int.MaxValue)
    {
        List<QuestionCardSO> output = new List<QuestionCardSO>();
        if (sourceCards == null)
        {
            return output;
        }

        int safeMax = Mathf.Max(0, maxCount);
        int count = Mathf.Min(sourceCards.Count, safeMax == 0 ? sourceCards.Count : safeMax);

        for (int i = 0; i < count; i++)
        {
            QuestionCardSO source = sourceCards[i];
            if (source == null || string.IsNullOrWhiteSpace(source.questionText))
            {
                continue;
            }

            QuestionCardSO cloned = ScriptableObject.CreateInstance<QuestionCardSO>();
            cloned.name = $"OfflineRuntime_{i + 1}";
            cloned.questionText = source.questionText.Trim();
            cloned.backgroundImage = source.backgroundImage;
            cloned.backgroundImageUrl = source.backgroundImageUrl;
            cloned.rightSwipeEffects = CloneEffects(source.rightSwipeEffects);
            cloned.leftSwipeEffects = CloneEffects(source.leftSwipeEffects);
            output.Add(cloned);
        }

        return output;
    }

    public static List<QuestionData> ConvertToQuestionData(List<QuestionCardSO> sourceCards, int maxCount = int.MaxValue)
    {
        List<QuestionData> output = new List<QuestionData>();
        if (sourceCards == null)
        {
            return output;
        }

        int safeMax = Mathf.Max(0, maxCount);
        int count = Mathf.Min(sourceCards.Count, safeMax == 0 ? sourceCards.Count : safeMax);

        for (int i = 0; i < count; i++)
        {
            QuestionCardSO card = sourceCards[i];
            if (card == null || string.IsNullOrWhiteSpace(card.questionText))
            {
                continue;
            }

            QuestionData data = new QuestionData
            {
                questionText = card.questionText.Trim(),
                imageUrl = card.backgroundImageUrl,
                rightSwipeEffects = ConvertEffectsToQuestionData(card.rightSwipeEffects),
                leftSwipeEffects = ConvertEffectsToQuestionData(card.leftSwipeEffects)
            };

            if (data.rightSwipeEffects.Count == 0 && data.leftSwipeEffects.Count == 0)
            {
                continue;
            }

            output.Add(data);
        }

        return output;
    }

    private static List<SwipeEffect> ConvertEffects(List<SwipeEffectData> sourceEffects)
    {
        List<SwipeEffect> output = new List<SwipeEffect>();
        if (sourceEffects == null)
        {
            return output;
        }

        foreach (SwipeEffectData source in sourceEffects)
        {
            if (source == null)
            {
                continue;
            }

            int absolutePoints = source.points == int.MinValue ? int.MaxValue : Mathf.Abs(source.points);
            ScoreEffectType effectType = source.points >= 0 ? ScoreEffectType.Cong : ScoreEffectType.Tru;

            output.Add(new SwipeEffect
            {
                majorID = MapMajorTypeToMajorKey(source.majorID),
                effectType = effectType,
                points = absolutePoints
            });
        }

        return output;
    }

    private static List<SwipeEffectData> ConvertEffectsToQuestionData(List<SwipeEffect> sourceEffects)
    {
        List<SwipeEffectData> output = new List<SwipeEffectData>();
        if (sourceEffects == null)
        {
            return output;
        }

        foreach (SwipeEffect source in sourceEffects)
        {
            if (!TryMapMajorKeyToMajorType(source.majorID, out MajorType majorType))
            {
                continue;
            }

            int points = source.GetSignedPoints();
            output.Add(new SwipeEffectData
            {
                majorID = majorType,
                points = points
            });
        }

        return output;
    }

    private static List<SwipeEffect> CloneEffects(List<SwipeEffect> sourceEffects)
    {
        List<SwipeEffect> output = new List<SwipeEffect>();
        if (sourceEffects == null)
        {
            return output;
        }

        output.AddRange(sourceEffects.Select(effect => new SwipeEffect
        {
            majorID = effect.majorID,
            effectType = effect.effectType,
            points = effect.points
        }));

        return output;
    }

    private static bool TryMapMajorKeyToMajorType(string majorKey, out MajorType majorType)
    {
        string normalized = NormalizeMajorText(majorKey);
        switch (normalized)
        {
            case "it":
            case "cntt":
                majorType = MajorType.IT;
                return true;
            case "kỹ thuật":
            case "ky thuat":
            case "engineering":
                majorType = MajorType.KyThuat;
                return true;
            case "kinh tế":
            case "kinh te":
            case "business":
                majorType = MajorType.KinhTe;
                return true;
            case "du lịch - khách sạn":
            case "du lich - khach san":
            case "du lịch khách sạn":
            case "du lich khach san":
            case "tourism":
            case "hospitality":
                majorType = MajorType.DuLich;
                return true;
            case "ngôn ngữ":
            case "ngon ngu":
            case "language":
                majorType = MajorType.NgonNgu;
                return true;
            case "thiết kế đồ họa":
            case "thiet ke do hoa":
            case "design":
            case "graphic design":
                majorType = MajorType.ThietKeDoHoa;
                return true;
            case "dược":
            case "duoc":
            case "pharmacy":
                majorType = MajorType.Duoc;
                return true;
            default:
                majorType = default;
                return false;
        }
    }

    private static string NormalizeMajorText(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        return value.Trim().ToLowerInvariant().Replace("_", " ");
    }

    private static string MapMajorTypeToMajorKey(MajorType majorType)
    {
        switch (majorType)
        {
            case MajorType.IT:
                return "IT";
            case MajorType.KyThuat:
                return "Kỹ thuật";
            case MajorType.KinhTe:
                return "Kinh tế";
            case MajorType.DuLich:
                return "Du lịch - Khách sạn";
            case MajorType.NgonNgu:
                return "Ngôn ngữ";
            case MajorType.ThietKeDoHoa:
                return "Thiết kế đồ họa";
            case MajorType.Duoc:
                return "Dược";
            default:
                return "IT";
        }
    }
}