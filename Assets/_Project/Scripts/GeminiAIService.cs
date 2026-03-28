using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.Networking;

public enum MajorType
{
    IT,
    KyThuat,
    KinhTe,
    DuLich,
    NgonNgu,
    ThietKeDoHoa,
    Duoc
}

[Serializable]
public class SwipeEffectData
{
    public MajorType majorID;
    public int points;
}

[Serializable]
public class QuestionData
{
    public string questionText;
    public string imageUrl;
    public List<SwipeEffectData> rightSwipeEffects = new List<SwipeEffectData>();
    public List<SwipeEffectData> leftSwipeEffects = new List<SwipeEffectData>();
}

public class GeminiAIService : MonoBehaviour
{
    [Header("Gemini API")]
    [SerializeField] private string apiKey;
    [SerializeField] private string modelName = "gemini-2.5-flash";
    [SerializeField] private List<string> fallbackModelNames = new List<string>
    {
        "gemini-2.5-flash",
        "gemini-2.5-pro"
    };
    [SerializeField, Min(1)] private int defaultQuestionCount = 10;
    [SerializeField] private bool logRawResponse;
    [SerializeField, Min(1)] private int requestTimeoutSeconds = 60;
    [SerializeField, Min(0)] private int maxRetriesOnTimeout = 2;
    [SerializeField, Min(0f)] private float retryDelaySeconds = 1.5f;
    [SerializeField, Range(0, 5)] private int maxFewShotExamplesInPrompt = 3;
    [SerializeField] private bool verboseLogs = true;

    private const string BaseUrlTemplate = "https://generativelanguage.googleapis.com/v1beta/models/{0}:generateContent";
    private static readonly HashSet<string> DeprecatedModels = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "gemini-2.0-flash",
        "gemini-2.0-flash-001",
        "gemini-2.0-flash-lite",
        "gemini-2.0-flash-lite-001"
    };

    public void GenerateQuestions(Action<List<QuestionData>> onSuccess, Action<string> onError)
    {
        GenerateQuestions(defaultQuestionCount, onSuccess, onError);
    }

    public void GenerateQuestions(int questionCount, Action<List<QuestionData>> onSuccess, Action<string> onError)
    {
        GenerateQuestions(questionCount, null, onSuccess, onError);
    }

    public void GenerateQuestions(int questionCount, List<QuestionData> fewShotExamples, Action<List<QuestionData>> onSuccess, Action<string> onError)
    {
        if (questionCount <= 0)
        {
            onError?.Invoke("Số lượng câu hỏi phải lớn hơn 0.");
            return;
        }

        StartCoroutine(GenerateQuestionsCoroutine(questionCount, fewShotExamples, onSuccess, onError));
    }

    private IEnumerator GenerateQuestionsCoroutine(int questionCount, List<QuestionData> fewShotExamples, Action<List<QuestionData>> onSuccess, Action<string> onError)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            onError?.Invoke("Thiếu Gemini API Key. Hãy set apiKey trong Inspector.");
            yield break;
        }

        string prompt = BuildPrompt(questionCount, fewShotExamples);
        GeminiGenerateContentRequest requestPayload = CreateRequestPayload(prompt);
        string requestJson = JsonConvert.SerializeObject(requestPayload);

        List<string> modelsToTry = BuildModelTryList();
        int sampleCount = fewShotExamples == null ? 0 : fewShotExamples.Count;
        string lastError = string.Empty;

        for (int i = 0; i < modelsToTry.Count; i++)
        {
            string currentModel = modelsToTry[i];
            bool hasNextModel = i < modelsToTry.Count - 1;
            int maxAttempts = 1 + Mathf.Max(0, maxRetriesOnTimeout);

            for (int attempt = 1; attempt <= maxAttempts; attempt++)
            {
                if (verboseLogs)
                {
                    Debug.Log($"[GeminiAIService] Sending request. Model={currentModel}, attempt={attempt}/{maxAttempts}, questionCount={questionCount}, fewShotCount={sampleCount}, timeout={requestTimeoutSeconds}s");
                }

                bool requestCompleted = false;
                string rawResponse = string.Empty;
                long responseCode = 0;
                UnityWebRequest.Result requestResult = UnityWebRequest.Result.InProgress;
                string requestError = string.Empty;
                string apiErrorDetail = string.Empty;

                using (UnityWebRequest request = CreateWebRequest(currentModel, requestJson))
                {
                    yield return request.SendWebRequest();

                    rawResponse = request.downloadHandler != null ? request.downloadHandler.text : string.Empty;
                    responseCode = request.responseCode;
                    requestResult = request.result;
                    requestError = request.error;
                    requestCompleted = true;

                    if (requestResult != UnityWebRequest.Result.Success)
                    {
                        apiErrorDetail = TryExtractGeminiApiError(rawResponse, out string parsedApiError)
                            ? parsedApiError
                            : rawResponse;
                    }
                }

                if (!requestCompleted)
                {
                    lastError = "Request Gemini không hoàn tất.";
                    continue;
                }

                if (requestResult != UnityWebRequest.Result.Success)
                {
                    bool isTimeout = IsTimeoutError(responseCode, requestError);

                    if (isTimeout)
                    {
                        bool canRetrySameModel = attempt < maxAttempts;
                        bool canTryNextModel = hasNextModel;

                        if (canRetrySameModel)
                        {
                            float waitSeconds = Mathf.Max(0f, retryDelaySeconds) * attempt;
                            if (verboseLogs)
                            {
                                Debug.LogWarning($"[GeminiAIService] Timeout với model '{currentModel}'. Retry sau {waitSeconds:0.0}s.");
                            }

                            if (waitSeconds > 0f)
                            {
                                yield return new WaitForSeconds(waitSeconds);
                            }

                            lastError = $"Timeout model {currentModel} (attempt {attempt}/{maxAttempts}).";
                            continue;
                        }

                        if (canTryNextModel)
                        {
                            if (verboseLogs)
                            {
                                Debug.LogWarning($"[GeminiAIService] Model '{currentModel}' timeout liên tiếp. Chuyển sang model dự phòng.");
                            }

                            lastError = $"Timeout model {currentModel} sau {maxAttempts} lần thử.";
                            break;
                        }

                        onError?.Invoke($"Gemini bị timeout (HTTP 0). Model={currentModel}. Hãy giảm questionCount/few-shot hoặc tăng timeout. Detail: {requestError}");
                        yield break;
                    }

                    if (responseCode == 429)
                    {
                        onError?.Invoke($"Gemini đang chặn do quota/rate limit (HTTP 429). Detail: {apiErrorDetail}. Hãy kiểm tra quota/billing và thử lại sau.");
                        yield break;
                    }

                    bool shouldTryNextModel = responseCode == 404 && hasNextModel;
                    if (shouldTryNextModel)
                    {
                        if (verboseLogs)
                        {
                            Debug.LogWarning($"[GeminiAIService] Model '{currentModel}' không còn khả dụng. Thử model dự phòng kế tiếp.");
                        }

                        lastError = $"Model {currentModel} không khả dụng: {apiErrorDetail}";
                        break;
                    }

                    onError?.Invoke($"Lỗi gọi Gemini API: HTTP {responseCode} - {requestError}. Detail: {apiErrorDetail}");
                    yield break;
                }

                if (logRawResponse)
                {
                    Debug.Log($"[GeminiAIService] Raw response: {rawResponse}");
                }

                if (!TryParseQuestionList(rawResponse, out List<QuestionData> questions, out string parseError))
                {
                    onError?.Invoke(parseError);
                    yield break;
                }

                if (verboseLogs)
                {
                    Debug.Log($"[GeminiAIService] Parse success. Received {questions.Count} questions from AI. Model={currentModel}");
                }

                onSuccess?.Invoke(questions);
                yield break;
            }
        }

        onError?.Invoke(string.IsNullOrWhiteSpace(lastError)
            ? "Không gọi được Gemini do model không khả dụng. Hãy kiểm tra modelName/fallbackModelNames."
            : lastError);
    }

    private bool IsTimeoutError(long responseCode, string requestError)
    {
        if (responseCode != 0)
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(requestError))
        {
            return true;
        }

        string normalized = requestError.Trim().ToLowerInvariant();
        return normalized.Contains("timeout") || normalized.Contains("timed out");
    }

    private UnityWebRequest CreateWebRequest(string model, string requestJson)
    {
        string url = string.Format(BaseUrlTemplate, model);
        UnityWebRequest request = new UnityWebRequest(url, UnityWebRequest.kHttpVerbPOST);
        request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(requestJson));
        request.downloadHandler = new DownloadHandlerBuffer();
        request.SetRequestHeader("Content-Type", "application/json");
        request.SetRequestHeader("X-goog-api-key", apiKey);
        request.timeout = Mathf.Max(1, requestTimeoutSeconds);
        return request;
    }

    private List<string> BuildModelTryList()
    {
        List<string> modelList = new List<string>();

        if (!string.IsNullOrWhiteSpace(modelName))
        {
            modelList.Add(modelName.Trim());
        }

        if (fallbackModelNames != null)
        {
            foreach (string fallback in fallbackModelNames)
            {
                if (!string.IsNullOrWhiteSpace(fallback))
                {
                    modelList.Add(fallback.Trim());
                }
            }
        }

        List<string> filtered = modelList
            .Where(model => !string.IsNullOrWhiteSpace(model))
            .Where(model => !DeprecatedModels.Contains(model.Trim()))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (verboseLogs && filtered.Count != modelList.Count)
        {
            Debug.LogWarning("[GeminiAIService] Đã bỏ qua model deprecated trong danh sách fallback. Hãy kiểm tra lại modelName/fallbackModelNames trong Inspector.");
        }

        return filtered;
    }

    private GeminiGenerateContentRequest CreateRequestPayload(string prompt)
    {
        return new GeminiGenerateContentRequest
        {
            contents = new List<GeminiContent>
            {
                new GeminiContent
                {
                    parts = new List<GeminiPart>
                    {
                        new GeminiPart { text = prompt }
                    }
                }
            },
            generationConfig = new GeminiGenerationConfig
            {
                responseMimeType = "application/json"
            }
        };
    }

    private bool TryParseQuestionList(string rawResponse, out List<QuestionData> questions, out string error)
    {
        questions = null;
        error = string.Empty;

        GeminiGenerateContentResponse geminiResponse;
        try
        {
            geminiResponse = JsonConvert.DeserializeObject<GeminiGenerateContentResponse>(rawResponse);
        }
        catch (Exception ex)
        {
            error = $"Không parse được Gemini response JSON: {ex.Message}";
            return false;
        }

        string modelJson = geminiResponse?.candidates?.FirstOrDefault()?.content?.parts?.FirstOrDefault()?.text;
        if (string.IsNullOrWhiteSpace(modelJson))
        {
            string blockReason = geminiResponse?.promptFeedback?.blockReason;
            error = string.IsNullOrWhiteSpace(blockReason)
                ? "Gemini không trả về nội dung JSON hợp lệ trong candidates[0].content.parts[0].text."
                : $"Gemini không trả về candidates text. BlockReason: {blockReason}";
            return false;
        }

        modelJson = CleanupPossibleCodeFences(modelJson);

        GeminiQuestionsPayload payload;
        try
        {
            payload = JsonConvert.DeserializeObject<GeminiQuestionsPayload>(modelJson);
        }
        catch (Exception ex)
        {
            error = $"Không parse được JSON câu hỏi do model trả về: {ex.Message}. Raw model text: {modelJson}";
            return false;
        }

        if (payload?.questions == null || payload.questions.Count == 0)
        {
            error = "JSON từ Gemini không chứa danh sách questions hoặc danh sách rỗng.";
            return false;
        }

        List<QuestionData> output = new List<QuestionData>();

        foreach (GeminiQuestionItem item in payload.questions)
        {
            if (item == null || string.IsNullOrWhiteSpace(item.questionText))
            {
                continue;
            }

            QuestionData data = new QuestionData
            {
                questionText = NormalizeDisplayText(item.questionText),
                imageUrl = CleanImageUrl(item.imageUrl),
                rightSwipeEffects = ConvertEffects(item.rightSwipeEffects),
                leftSwipeEffects = ConvertEffects(item.leftSwipeEffects)
            };

            output.Add(data);
        }

        if (output.Count == 0)
        {
            error = "Không có câu hỏi hợp lệ nào sau khi map dữ liệu từ Gemini.";
            return false;
        }

        questions = output;
        return true;
    }

    private string CleanupPossibleCodeFences(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return text;
        }

        string trimmed = text.Trim();
        if (!trimmed.StartsWith("```", StringComparison.Ordinal))
        {
            return trimmed;
        }

        int firstLineBreak = trimmed.IndexOf('\n');
        int lastFence = trimmed.LastIndexOf("```");
        if (firstLineBreak < 0 || lastFence <= firstLineBreak)
        {
            return trimmed;
        }

        string inner = trimmed.Substring(firstLineBreak + 1, lastFence - firstLineBreak - 1);
        return inner.Trim();
    }

    private string NormalizeDisplayText(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        return value.Trim().Normalize(NormalizationForm.FormC);
    }

    private string CleanImageUrl(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        return value.Trim();
    }

    private bool TryExtractGeminiApiError(string rawResponse, out string message)
    {
        message = string.Empty;
        if (string.IsNullOrWhiteSpace(rawResponse))
        {
            return false;
        }

        try
        {
            GeminiErrorEnvelope envelope = JsonConvert.DeserializeObject<GeminiErrorEnvelope>(rawResponse);
            if (envelope?.error == null)
            {
                return false;
            }

            message = $"{envelope.error.status} ({envelope.error.code}): {envelope.error.message}";
            return true;
        }
        catch
        {
            return false;
        }
    }

    private List<SwipeEffectData> ConvertEffects(List<GeminiSwipeEffectItem> rawEffects)
    {
        List<SwipeEffectData> effects = new List<SwipeEffectData>();
        if (rawEffects == null)
        {
            return effects;
        }

        foreach (GeminiSwipeEffectItem effect in rawEffects)
        {
            if (effect == null)
            {
                continue;
            }

            if (!TryParseMajor(effect.majorID, out MajorType majorType))
            {
                Debug.LogWarning($"[GeminiAIService] majorID không hợp lệ: '{effect.majorID}'. Bỏ qua effect này.");
                continue;
            }

            effects.Add(new SwipeEffectData
            {
                majorID = majorType,
                points = effect.points
            });
        }

        return effects;
    }

    private bool TryParseMajor(string majorRaw, out MajorType majorType)
    {
        if (Enum.TryParse(majorRaw, true, out majorType))
        {
            return true;
        }

        if (string.IsNullOrWhiteSpace(majorRaw))
        {
            majorType = default;
            return false;
        }

        string normalized = majorRaw.Trim().Replace(" ", string.Empty).Replace("_", string.Empty).Replace("-", string.Empty);

        foreach (string enumName in Enum.GetNames(typeof(MajorType)))
        {
            if (string.Equals(enumName, normalized, StringComparison.OrdinalIgnoreCase))
            {
                majorType = (MajorType)Enum.Parse(typeof(MajorType), enumName);
                return true;
            }
        }

        majorType = default;
        return false;
    }

    private string BuildPrompt(int questionCount, List<QuestionData> fewShotExamples)
    {
        StringBuilder builder = new StringBuilder();
        builder.Append($@"Bạn là chuyên gia tuyển sinh của FPT Polytechnic.
Hãy tạo chính xác {questionCount} câu hỏi trắc nghiệm tính cách dạng quẹt thẻ (Swipe) dành cho học sinh THPT.

MỤC TIÊU:
- Câu hỏi ngắn, đời thường, dễ hình dung, có tính phân loại sở thích nghề nghiệp.
- Dùng tiếng Việt chuẩn, đúng chính tả, đúng dấu, không viết tắt/teencode.
- Mỗi câu hỏi phải có đủ rightSwipeEffects và leftSwipeEffects.
- Mỗi mảng effects nên có ít nhất 2 phần tử.

RÀNG BUỘC BẮT BUỘC:
1) CHỈ trả về DUY NHẤT một object JSON hợp lệ, không markdown, không có ```json, không thêm mô tả.
2) Root bắt buộc là key ""questions"".
3) majorID chỉ được dùng đúng các giá trị enum sau: IT, KyThuat, KinhTe, DuLich, NgonNgu, ThietKeDoHoa, Duoc.
4) points là số nguyên, có thể dương hoặc âm.
5) imageUrl là URL ảnh https hợp lệ (jpg/png/webp), phù hợp nội dung câu hỏi; nếu không có ảnh thì để chuỗi rỗng "".
6) Bắt buộc đúng schema và tên field như mẫu dưới đây.
7) Tuyệt đối tránh lỗi chính tả tiếng Việt.

MẪU JSON BẮT BUỘC:
{{
  ""questions"": [
    {{
      ""questionText"": ""Bạn thích ngồi lỳ trong phòng code Web đến quên ăn, nhưng lại cực kỳ lười ra ngoài giao tiếp với khách hàng?"",
            ""imageUrl"": ""https://images.unsplash.com/photo-1518770660439-4636190af475"",
      ""rightSwipeEffects"": [
        {{ ""majorID"": ""IT"", ""points"": 10 }},
        {{ ""majorID"": ""DuLich"", ""points"": -5 }}
      ],
      ""leftSwipeEffects"": [
        {{ ""majorID"": ""KinhTe"", ""points"": 10 }},
        {{ ""majorID"": ""IT"", ""points"": -5 }}
      ]
    }}
  ]
}}");

        if (fewShotExamples != null)
        {
            List<QuestionData> cleanExamples = fewShotExamples
                .Where(q => q != null && !string.IsNullOrWhiteSpace(q.questionText))
                .Take(Mathf.Max(0, maxFewShotExamplesInPrompt))
                .ToList();

            if (cleanExamples.Count > 0)
            {
                string exampleJson = BuildFewShotExamplesJson(cleanExamples);
                builder.Append("\n\nVÍ DỤ THAM CHIẾU (few-shot) TỪ DATA OFFLINE, HÃY HỌC GIỌNG VĂN VÀ ĐỘ KHÓ:");
                builder.Append("\n");
                builder.Append(exampleJson);
                builder.Append("\nKhông sao chép nguyên văn các câu trong ví dụ, hãy tạo câu mới.");
            }
        }

        return builder.ToString();
    }

    private string BuildFewShotExamplesJson(List<QuestionData> examples)
    {
        var payload = new
        {
            questions = examples.Select(question => new
            {
                questionText = question.questionText,
                imageUrl = question.imageUrl,
                rightSwipeEffects = question.rightSwipeEffects?.Select(effect => new
                {
                    majorID = effect.majorID.ToString(),
                    points = effect.points
                }),
                leftSwipeEffects = question.leftSwipeEffects?.Select(effect => new
                {
                    majorID = effect.majorID.ToString(),
                    points = effect.points
                })
            })
        };

        return JsonConvert.SerializeObject(payload, Formatting.Indented);
    }

    [Serializable]
    private class GeminiGenerateContentRequest
    {
        public List<GeminiContent> contents;
        public GeminiGenerationConfig generationConfig;
    }

    [Serializable]
    private class GeminiGenerationConfig
    {
        public string responseMimeType;
    }

    [Serializable]
    private class GeminiContent
    {
        public List<GeminiPart> parts;
    }

    [Serializable]
    private class GeminiPart
    {
        public string text;
    }

    [Serializable]
    private class GeminiGenerateContentResponse
    {
        public List<GeminiCandidate> candidates;
        public GeminiPromptFeedback promptFeedback;
    }

    [Serializable]
    private class GeminiCandidate
    {
        public GeminiContent content;
    }

    [Serializable]
    private class GeminiQuestionsPayload
    {
        public List<GeminiQuestionItem> questions;
    }

    [Serializable]
    private class GeminiQuestionItem
    {
        public string questionText;
        public string imageUrl;
        public List<GeminiSwipeEffectItem> rightSwipeEffects;
        public List<GeminiSwipeEffectItem> leftSwipeEffects;
    }

    [Serializable]
    private class GeminiSwipeEffectItem
    {
        public string majorID;
        public int points;
    }

    [Serializable]
    private class GeminiPromptFeedback
    {
        public string blockReason;
    }

    [Serializable]
    private class GeminiErrorEnvelope
    {
        public GeminiErrorBody error;
    }

    [Serializable]
    private class GeminiErrorBody
    {
        public int code;
        public string message;
        public string status;
    }
}