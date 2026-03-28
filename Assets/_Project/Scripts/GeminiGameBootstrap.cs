using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class GeminiGameBootstrap : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private GeminiAIService geminiAIService;
    [SerializeField] private GameManager gameManager;
    [SerializeField] private QuestionDatabaseSO offlineQuestionDatabase;

    [Header("Generation Config")]
    [SerializeField, Min(1)] private int questionCount = 20;
    [SerializeField] private bool autoGenerateOnPlay;
    [SerializeField] private bool fallbackToLocalQuestionsOnError = true;

    [Header("Hybrid Offline + AI")]
    [SerializeField] private bool useOfflineFewShotExamples = true;
    [SerializeField, Min(0)] private int fewShotExampleCount = 3;
    [SerializeField, Min(0)] private int offlineQuestionsToMix = 4;
    [SerializeField] private bool randomizeMixedDeck = true;
    [SerializeField] private bool verboseLogs = true;

    private List<QuestionCardSO> _offlinePoolSnapshot = new List<QuestionCardSO>();

    private void Start()
    {
        if (!autoGenerateOnPlay)
        {
            return;
        }

        GenerateAndStart();
    }

    public void GenerateAndStart()
    {
        if (geminiAIService == null)
        {
            Debug.LogError("[GeminiGameBootstrap] Thiếu reference GeminiAIService.");
            return;
        }

        if (gameManager == null)
        {
            Debug.LogError("[GeminiGameBootstrap] Thiếu reference GameManager.");
            return;
        }

        _offlinePoolSnapshot = GetOfflinePool();
        List<QuestionData> fewShot = BuildFewShotExamples(_offlinePoolSnapshot);

        if (verboseLogs)
        {
            int fewShotCount = fewShot == null ? 0 : fewShot.Count;
            Debug.Log($"[GeminiGameBootstrap] Generate start. Target={questionCount}, offlinePool={_offlinePoolSnapshot.Count}, fewShot={fewShotCount}, offlineMix={offlineQuestionsToMix}");
        }

        geminiAIService.GenerateQuestions(questionCount, fewShot, OnGenerateSuccess, OnGenerateError);
    }

    private void OnGenerateSuccess(List<QuestionData> questions)
    {
        List<QuestionData> selectedAI = SelectQuestions(questions, questionCount);
        List<QuestionCardSO> aiCards = GeminiQuestionCardAdapter.Convert(selectedAI);
        int offlineMixCount = Mathf.Clamp(offlineQuestionsToMix, 0, _offlinePoolSnapshot.Count);
        List<QuestionCardSO> selectedOffline = CloneOfflineCardsForMix(_offlinePoolSnapshot, offlineMixCount);

        List<QuestionCardSO> finalDeck = new List<QuestionCardSO>(selectedOffline.Count + aiCards.Count);
        finalDeck.AddRange(selectedOffline);
        finalDeck.AddRange(aiCards);

        if (finalDeck.Count > questionCount)
        {
            finalDeck = SelectCards(finalDeck, questionCount);
        }

        if (randomizeMixedDeck)
        {
            Shuffle(finalDeck);
        }

        if (verboseLogs)
        {
            Debug.Log($"[GeminiGameBootstrap] AI success. AI cards={aiCards.Count}, offline cards mixed={selectedOffline.Count}, final deck={finalDeck.Count}");
        }

        gameManager.SetRuntimeQuestionCards(finalDeck);
        gameManager.StartGame();
    }

    private void OnGenerateError(string error)
    {
        Debug.LogError($"[GeminiGameBootstrap] Generate thất bại: {error}");

        if (!fallbackToLocalQuestionsOnError)
        {
            return;
        }

        if (verboseLogs)
        {
            Debug.LogWarning("[GeminiGameBootstrap] Fallback to local questions is ON. Game will continue with offline deck.");
        }

        gameManager.ClearRuntimeQuestionCards();
        gameManager.StartGame();
    }

    private List<QuestionCardSO> GetOfflinePool()
    {
        if (offlineQuestionDatabase != null && offlineQuestionDatabase.defaultQuestions != null && offlineQuestionDatabase.defaultQuestions.Count > 0)
        {
            return offlineQuestionDatabase.defaultQuestions.Where(card => card != null).ToList();
        }

        return new List<QuestionCardSO>();
    }

    private List<QuestionData> BuildFewShotExamples(List<QuestionCardSO> offlinePool)
    {
        if (!useOfflineFewShotExamples || fewShotExampleCount <= 0 || offlinePool == null || offlinePool.Count == 0)
        {
            return null;
        }

        List<QuestionCardSO> selected = SelectCards(offlinePool, fewShotExampleCount);
        return GeminiQuestionCardAdapter.ConvertToQuestionData(selected, fewShotExampleCount);
    }

    private List<QuestionCardSO> CloneOfflineCardsForMix(List<QuestionCardSO> offlinePool, int count)
    {
        if (count <= 0 || offlinePool == null || offlinePool.Count == 0)
        {
            return new List<QuestionCardSO>();
        }

        List<QuestionCardSO> selected = SelectCards(offlinePool, count);
        return GeminiQuestionCardAdapter.CloneCards(selected, count);
    }

    private List<QuestionData> SelectQuestions(List<QuestionData> source, int count)
    {
        if (source == null)
        {
            return new List<QuestionData>();
        }

        List<QuestionData> filtered = source.Where(question => question != null && !string.IsNullOrWhiteSpace(question.questionText)).ToList();
        if (filtered.Count <= count)
        {
            return filtered;
        }

        if (!randomizeMixedDeck)
        {
            return filtered.Take(count).ToList();
        }

        Shuffle(filtered);
        return filtered.Take(count).ToList();
    }

    private List<QuestionCardSO> SelectCards(List<QuestionCardSO> source, int count)
    {
        if (source == null)
        {
            return new List<QuestionCardSO>();
        }

        List<QuestionCardSO> filtered = source.Where(card => card != null).ToList();
        if (filtered.Count <= count)
        {
            return filtered;
        }

        if (!randomizeMixedDeck)
        {
            return filtered.Take(count).ToList();
        }

        Shuffle(filtered);
        return filtered.Take(count).ToList();
    }

    private void Shuffle<T>(List<T> list)
    {
        if (list == null)
        {
            return;
        }

        for (int i = list.Count - 1; i > 0; i--)
        {
            int swapIndex = Random.Range(0, i + 1);
            T temp = list[i];
            list[i] = list[swapIndex];
            list[swapIndex] = temp;
        }
    }
}