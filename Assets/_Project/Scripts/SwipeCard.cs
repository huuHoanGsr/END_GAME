using UnityEngine;
using UnityEngine.EventSystems; // Bắt buộc phải có để dùng UI Events
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine.Networking;

public class SwipeCard : MonoBehaviour, IDragHandler, IBeginDragHandler, IEndDragHandler
{
    private static readonly Dictionary<string, Sprite> RemoteSpriteCache = new Dictionary<string, Sprite>();
    private static readonly HashSet<string> FailedRemoteUrls = new HashSet<string>();
    private static readonly Dictionary<string, Sprite[]> ResourcesSpriteCache = new Dictionary<string, Sprite[]>();

    [Header("Settings")]
    [SerializeField] private float swipeThreshold = 150f; // Kéo bao xa thì được tính là quẹt
    [SerializeField] private float rotationMultiplier = 5f; // Tốc độ xoay khi kéo
    [SerializeField] private float maxRotation = 18f;
    [SerializeField] private float resetDuration = 0.18f;
    [SerializeField] private float swipeOutDuration = 0.2f;
    [SerializeField] private float swipeOutDistance = 1200f;

    [Header("Swipe Feedback (Optional)")]
    [SerializeField] private CanvasGroup likeHint;
    [SerializeField] private CanvasGroup skipHint;
    [SerializeField] private bool vibrateOnSuccessfulSwipe = true;
    [SerializeField] private RectTransform likeStampTransform;
    [SerializeField] private RectTransform skipStampTransform;
    [SerializeField] private float stampPopScale = 1.22f;
    [SerializeField] private float stampPopDuration = 0.14f;

    [Header("Card UI")]
    [SerializeField] private Text questionText;
    [SerializeField] private Image backgroundImage;
    [Header("Answer Feedback")]
    [SerializeField] private AudioSource sfxSource;
    [SerializeField] private AudioClip correctSfx;
    [SerializeField] private AudioClip wrongSfx;
    [SerializeField] private Image screenBorderImage;
    [SerializeField] private float borderFlashDuration = 0.2f;
    [SerializeField] private Color correctBorderColor = new Color(0.2f, 0.9f, 0.3f, 1f);
    [SerializeField] private Color wrongBorderColor = new Color(0.95f, 0.2f, 0.2f, 1f);

    [Header("Image Fallback")]
    [SerializeField] private bool useLocalFallbackWhenRemoteFails = true;
    [SerializeField] private List<Sprite> localFallbackSprites = new List<Sprite>();
    [SerializeField] private bool randomFallbackSprite = true;
    [SerializeField] private bool autoLoadFallbackFromResources = true;
    [SerializeField] private string fallbackResourcesFolder = "QuestionFallbacks";
    
    private Vector2 _startPosition;
    private RectTransform _rectTransform;
    private Coroutine _activeAnimation;
    private Coroutine _likePopCoroutine;
    private Coroutine _skipPopCoroutine;
    private Coroutine _borderFlashCoroutine;
    private bool _isAnimating;
    private bool _likeThresholdTriggered;
    private bool _skipThresholdTriggered;
    private Vector3 _likeBaseScale;
    private Vector3 _skipBaseScale;
    private Coroutine _loadImageCoroutine;
    private string _activeImageUrl;
    private QuestionCardSO _cardData;

    private void Awake()
    {
        _rectTransform = GetComponent<RectTransform>();
        CacheHintReferences();
        ResetStampVisuals();
        SetHintAlpha(0f, 0f);
        EnsureFeedbackComponents();
    }

    public void ConfigureSwipeFeedback(
        CanvasGroup likeHintGroup,
        CanvasGroup skipHintGroup,
        RectTransform likeStampRect,
        RectTransform skipStampRect)
    {
        likeHint = likeHintGroup;
        skipHint = skipHintGroup;
        likeStampTransform = likeStampRect;
        skipStampTransform = skipStampRect;

        CacheHintReferences();
        ResetStampVisuals();
        SetHintAlpha(0f, 0f);
    }

    public void Setup(QuestionCardSO cardData)
    {
        if (cardData == null)
        {
            return;
        }

        _cardData = cardData;

        if (questionText != null)
        {
            string normalizedQuestion = string.IsNullOrWhiteSpace(cardData.questionText)
                ? string.Empty
                : cardData.questionText.Normalize(NormalizationForm.FormC);
            questionText.text = normalizedQuestion;
        }

        if (backgroundImage != null)
        {
            backgroundImage.sprite = cardData.backgroundImage;
            backgroundImage.enabled = cardData.backgroundImage != null;

            // Nếu card không có sprite local thì thử tải ảnh runtime từ URL.
            if (cardData.backgroundImage == null)
            {
                LoadRemoteImage(cardData.backgroundImageUrl);
            }
            else
            {
                StopRemoteImageLoad();
            }
        }
    }

    private void OnDestroy()
    {
        StopRemoteImageLoad();
    }

    private void StopRemoteImageLoad()
    {
        if (_loadImageCoroutine != null)
        {
            StopCoroutine(_loadImageCoroutine);
            _loadImageCoroutine = null;
        }

        _activeImageUrl = null;
    }

    private void LoadRemoteImage(string imageUrl)
    {
        StopRemoteImageLoad();

        if (backgroundImage == null)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(imageUrl))
        {
            ApplyLocalFallbackSprite();
            return;
        }

        string normalizedUrl = imageUrl.Trim();
        if (!normalizedUrl.StartsWith("http://") && !normalizedUrl.StartsWith("https://"))
        {
            ApplyLocalFallbackSprite();
            return;
        }

        if (RemoteSpriteCache.TryGetValue(normalizedUrl, out Sprite cachedSprite) && cachedSprite != null)
        {
            backgroundImage.sprite = cachedSprite;
            backgroundImage.enabled = true;
            return;
        }

        if (FailedRemoteUrls.Contains(normalizedUrl))
        {
            ApplyLocalFallbackSprite();
            return;
        }

        _activeImageUrl = normalizedUrl;
        _loadImageCoroutine = StartCoroutine(LoadRemoteImageCoroutine(normalizedUrl));
    }

    private IEnumerator LoadRemoteImageCoroutine(string imageUrl)
    {
        using (UnityWebRequest request = UnityWebRequestTexture.GetTexture(imageUrl))
        {
            request.timeout = 15;
            yield return request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
            {
                Debug.LogWarning($"[SwipeCard] Không tải được ảnh URL: {imageUrl}. Error: {request.error}");
                FailedRemoteUrls.Add(imageUrl);
                ApplyLocalFallbackSprite();
                _loadImageCoroutine = null;
                yield break;
            }

            if (_activeImageUrl != imageUrl || backgroundImage == null)
            {
                _loadImageCoroutine = null;
                yield break;
            }

            Texture2D texture = DownloadHandlerTexture.GetContent(request);
            if (texture == null)
            {
                FailedRemoteUrls.Add(imageUrl);
                ApplyLocalFallbackSprite();
                _loadImageCoroutine = null;
                yield break;
            }

            Sprite sprite = Sprite.Create(
                texture,
                new Rect(0f, 0f, texture.width, texture.height),
                new Vector2(0.5f, 0.5f));

            RemoteSpriteCache[imageUrl] = sprite;
            backgroundImage.sprite = sprite;
            backgroundImage.enabled = true;
        }

        _loadImageCoroutine = null;
    }

    private void ApplyLocalFallbackSprite()
    {
        if (!useLocalFallbackWhenRemoteFails || backgroundImage == null)
        {
            return;
        }

        List<Sprite> candidateSprites = new List<Sprite>();
        if (localFallbackSprites != null)
        {
            candidateSprites.AddRange(localFallbackSprites.Where(sprite => sprite != null));
        }

        if (autoLoadFallbackFromResources)
        {
            string folder = string.IsNullOrWhiteSpace(fallbackResourcesFolder)
                ? "QuestionFallbacks"
                : fallbackResourcesFolder.Trim();

            if (!ResourcesSpriteCache.TryGetValue(folder, out Sprite[] resourcesSprites) || resourcesSprites == null)
            {
                resourcesSprites = Resources.LoadAll<Sprite>(folder);
                ResourcesSpriteCache[folder] = resourcesSprites;
            }

            if (resourcesSprites != null && resourcesSprites.Length > 0)
            {
                candidateSprites.AddRange(resourcesSprites.Where(sprite => sprite != null));
            }
        }

        if (candidateSprites.Count == 0)
        {
            return;
        }

        int index = randomFallbackSprite
            ? Random.Range(0, candidateSprites.Count)
            : 0;

        backgroundImage.sprite = candidateSprites[index];
        backgroundImage.enabled = true;
    }

    // Khi ngón tay BẮT ĐẦU chạm và di chuyển
    public void OnBeginDrag(PointerEventData eventData)
    {
        if (_isAnimating)
        {
            return;
        }

        if (_activeAnimation != null)
        {
            StopCoroutine(_activeAnimation);
            _activeAnimation = null;
        }

        // Lưu lại vị trí ban đầu để nếu không quẹt tới ngưỡng, thẻ bật ngược lại
        _startPosition = _rectTransform.anchoredPosition;
        _likeThresholdTriggered = false;
        _skipThresholdTriggered = false;
        ResetStampVisuals();
    }

    // Khi ngón tay ĐANG kéo
    public void OnDrag(PointerEventData eventData)
    {
        if (_isAnimating)
        {
            return;
        }

        // 1. Di chuyển thẻ theo ngón tay (chỉ di chuyển trục X cho mượt, hoặc cả XY tùy bạn)
        Vector2 currentPosition = _rectTransform.anchoredPosition;
        currentPosition.x += eventData.delta.x; 
        currentPosition.y += eventData.delta.y; // Nếu muốn thẻ có thể bị kéo lên xuống
        _rectTransform.anchoredPosition = currentPosition;

        // 2. Xoay thẻ dựa trên khoảng cách đã kéo
        float distanceX = currentPosition.x - _startPosition.x;
        float rotationAngle = Mathf.Clamp(distanceX * -rotationMultiplier * 0.01f, -maxRotation, maxRotation);
        _rectTransform.localEulerAngles = new Vector3(0, 0, rotationAngle);

        float normalizedSwipe = Mathf.Clamp01(Mathf.Abs(distanceX) / swipeThreshold);
        if (distanceX > 0f)
        {
            SetHintAlpha(normalizedSwipe, 0f);

            if (normalizedSwipe >= 1f && !_likeThresholdTriggered)
            {
                _likeThresholdTriggered = true;
                PlayStampPop(true);
            }

            if (normalizedSwipe < 0.7f)
            {
                _likeThresholdTriggered = false;
            }

            _skipThresholdTriggered = false;
        }
        else if (distanceX < 0f)
        {
            SetHintAlpha(0f, normalizedSwipe);

            if (normalizedSwipe >= 1f && !_skipThresholdTriggered)
            {
                _skipThresholdTriggered = true;
                PlayStampPop(false);
            }

            if (normalizedSwipe < 0.7f)
            {
                _skipThresholdTriggered = false;
            }

            _likeThresholdTriggered = false;
        }
        else
        {
            SetHintAlpha(0f, 0f);
            _likeThresholdTriggered = false;
            _skipThresholdTriggered = false;
        }

        // TODO (Tùy chọn): Dựa vào distanceX để làm hiện dần (Lerp Alpha) chữ THÍCH hoặc BỎ QUA
    }

    // Khi ngón tay BUÔNG ra
    public void OnEndDrag(PointerEventData eventData)
    {
        if (_isAnimating)
        {
            return;
        }

        float distanceX = _rectTransform.anchoredPosition.x - _startPosition.x;

        if (distanceX > swipeThreshold)
        {
            // Quẹt Phải (Thích)
            ProcessSwipe(true);
        }
        else if (distanceX < -swipeThreshold)
        {
            // Quẹt Trái (Bỏ qua)
            ProcessSwipe(false);
        }
        else
        {
            // Kéo chưa đủ xa -> Hủy thao tác, bật ngược về giữa
            ResetCard();
        }
    }

    private void ProcessSwipe(bool isRight)
    {
        if (_activeAnimation != null)
        {
            StopCoroutine(_activeAnimation);
        }

        PlayAnswerFeedback(isRight);

        _activeAnimation = StartCoroutine(AnimateSwipeOut(isRight));
    }

    private void PlayAnswerFeedback(bool isRight)
    {
        bool isCorrect = IsSwipeCorrect(isRight);

        FlashScreenBorder(isCorrect ? correctBorderColor : wrongBorderColor);

        if (sfxSource == null)
        {
            return;
        }

        AudioClip clip = isCorrect ? correctSfx : wrongSfx;
        if (clip != null)
        {
            sfxSource.PlayOneShot(clip);
        }
    }

    private bool IsSwipeCorrect(bool isRight)
    {
        if (_cardData == null)
        {
            return true;
        }

        List<SwipeEffect> effects = isRight ? _cardData.rightSwipeEffects : _cardData.leftSwipeEffects;
        if (effects == null || effects.Count == 0)
        {
            return false;
        }

        int delta = 0;
        for (int i = 0; i < effects.Count; i++)
        {
            delta += effects[i].GetSignedPoints();
        }

        return delta > 0;
    }

    private void ResetCard()
    {
        if (_activeAnimation != null)
        {
            StopCoroutine(_activeAnimation);
        }

        _activeAnimation = StartCoroutine(AnimateReset());
    }

    private IEnumerator AnimateReset()
    {
        _isAnimating = true;

        Vector2 fromPosition = _rectTransform.anchoredPosition;
        Quaternion fromRotation = _rectTransform.localRotation;
        Quaternion toRotation = Quaternion.identity;
        float elapsed = 0f;
        float adjustedResetDuration = GetAdjustedDuration(resetDuration);

        while (elapsed < adjustedResetDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / adjustedResetDuration);
            float eased = EaseOutCubic(t);

            _rectTransform.anchoredPosition = Vector2.LerpUnclamped(fromPosition, _startPosition, eased);
            _rectTransform.localRotation = Quaternion.LerpUnclamped(fromRotation, toRotation, eased);
            SetHintAlpha(0f, 0f);

            yield return null;
        }

        _rectTransform.anchoredPosition = _startPosition;
        _rectTransform.localEulerAngles = Vector3.zero;
        ResetStampVisuals();
        SetHintAlpha(0f, 0f);

        _isAnimating = false;
        _activeAnimation = null;
    }

    private IEnumerator AnimateSwipeOut(bool isRight)
    {
        _isAnimating = true;

        if (vibrateOnSuccessfulSwipe)
        {
            TriggerHapticFeedback();
        }

        Vector2 fromPosition = _rectTransform.anchoredPosition;
        float direction = isRight ? 1f : -1f;
        Vector2 toPosition = fromPosition + new Vector2(swipeOutDistance * direction, 120f);
        float targetZRotation = isRight ? -maxRotation : maxRotation;
        Quaternion fromRotation = _rectTransform.localRotation;
        Quaternion toRotation = Quaternion.Euler(0f, 0f, targetZRotation);
        float elapsed = 0f;
        float adjustedSwipeOutDuration = GetAdjustedDuration(swipeOutDuration);

        while (elapsed < adjustedSwipeOutDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / adjustedSwipeOutDuration);
            float eased = EaseInCubic(t);

            _rectTransform.anchoredPosition = Vector2.LerpUnclamped(fromPosition, toPosition, eased);
            _rectTransform.localRotation = Quaternion.LerpUnclamped(fromRotation, toRotation, eased);

            if (isRight)
            {
                SetHintAlpha(1f, 0f);
            }
            else
            {
                SetHintAlpha(0f, 1f);
            }

            yield return null;
        }

        // Chỉ bắn event sau khi animation vuốt hoàn tất để chuyển card tự nhiên hơn.
        GameEvents.OnCardSwiped?.Invoke(isRight);
        Destroy(gameObject);
    }

    private void TriggerHapticFeedback()
    {
        // Handheld API có thể không tồn tại ở một số target/platform profile.
        // Giữ no-op để đảm bảo build đa nền tảng luôn an toàn.
    }

    private void SetHintAlpha(float likeAlpha, float skipAlpha)
    {
        if (likeHint != null)
        {
            likeHint.alpha = likeAlpha;
        }

        if (skipHint != null)
        {
            skipHint.alpha = skipAlpha;
        }
    }

    private void CacheHintReferences()
    {
        if (likeStampTransform == null && likeHint != null)
        {
            likeStampTransform = likeHint.GetComponent<RectTransform>();
        }

        if (skipStampTransform == null && skipHint != null)
        {
            skipStampTransform = skipHint.GetComponent<RectTransform>();
        }

        _likeBaseScale = likeStampTransform != null ? likeStampTransform.localScale : Vector3.one;
        _skipBaseScale = skipStampTransform != null ? skipStampTransform.localScale : Vector3.one;
    }

    private void EnsureFeedbackComponents()
    {
        if (sfxSource == null)
        {
            sfxSource = GetComponent<AudioSource>();
            if (sfxSource == null)
            {
                sfxSource = gameObject.AddComponent<AudioSource>();
            }
        }

        EnsureScreenBorder();
    }

    private void EnsureScreenBorder()
    {
        if (screenBorderImage != null)
        {
            return;
        }

        Canvas rootCanvas = GetComponentInParent<Canvas>();
        if (rootCanvas == null)
        {
            return;
        }

        GameObject borderRoot = new GameObject("AnswerScreenBorder", typeof(RectTransform), typeof(Image), typeof(Outline));
        borderRoot.transform.SetParent(rootCanvas.transform, false);
        borderRoot.transform.SetAsLastSibling();

        RectTransform rect = borderRoot.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        Image image = borderRoot.GetComponent<Image>();
        image.color = new Color(0f, 0f, 0f, 0f);
        image.raycastTarget = false;

        Outline outline = borderRoot.GetComponent<Outline>();
        outline.effectDistance = new Vector2(8f, 8f);
        outline.enabled = false;

        screenBorderImage = image;
    }

    private void FlashScreenBorder(Color color)
    {
        if (screenBorderImage == null)
        {
            return;
        }

        Outline outline = screenBorderImage.GetComponent<Outline>();
        if (outline == null)
        {
            return;
        }

        outline.effectColor = color;
        outline.enabled = true;

        if (_borderFlashCoroutine != null)
        {
            StopCoroutine(_borderFlashCoroutine);
        }

        _borderFlashCoroutine = StartCoroutine(HideScreenBorderAfterDelay());
    }

    private IEnumerator HideScreenBorderAfterDelay()
    {
        yield return new WaitForSeconds(GetAdjustedDuration(Mathf.Max(0.05f, borderFlashDuration)));

        if (screenBorderImage != null)
        {
            Outline outline = screenBorderImage.GetComponent<Outline>();
            if (outline != null)
            {
                outline.enabled = false;
            }
        }

        _borderFlashCoroutine = null;
    }

    private void PlayStampPop(bool isLikeStamp)
    {
        if (isLikeStamp)
        {
            if (likeStampTransform == null)
            {
                return;
            }

            if (_likePopCoroutine != null)
            {
                StopCoroutine(_likePopCoroutine);
            }

            _likePopCoroutine = StartCoroutine(AnimateStampPop(likeStampTransform, _likeBaseScale, true));
            return;
        }

        if (skipStampTransform == null)
        {
            return;
        }

        if (_skipPopCoroutine != null)
        {
            StopCoroutine(_skipPopCoroutine);
        }

        _skipPopCoroutine = StartCoroutine(AnimateStampPop(skipStampTransform, _skipBaseScale, false));
    }

    private IEnumerator AnimateStampPop(RectTransform target, Vector3 baseScale, bool isLikeStamp)
    {
        float duration = GetAdjustedDuration(Mathf.Max(0.01f, stampPopDuration));
        float elapsed = 0f;
        Vector3 peakScale = baseScale * Mathf.Max(1f, stampPopScale);

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);

            float phase = t < 0.5f ? t / 0.5f : (t - 0.5f) / 0.5f;
            float eased = EaseOutCubic(phase);

            if (t < 0.5f)
            {
                target.localScale = Vector3.LerpUnclamped(baseScale, peakScale, eased);
            }
            else
            {
                target.localScale = Vector3.LerpUnclamped(peakScale, baseScale, eased);
            }

            yield return null;
        }

        target.localScale = baseScale;

        if (isLikeStamp)
        {
            _likePopCoroutine = null;
        }
        else
        {
            _skipPopCoroutine = null;
        }
    }

    private void ResetStampVisuals()
    {
        if (_likePopCoroutine != null)
        {
            StopCoroutine(_likePopCoroutine);
            _likePopCoroutine = null;
        }

        if (_skipPopCoroutine != null)
        {
            StopCoroutine(_skipPopCoroutine);
            _skipPopCoroutine = null;
        }

        if (likeStampTransform != null)
        {
            likeStampTransform.localScale = _likeBaseScale;
        }

        if (skipStampTransform != null)
        {
            skipStampTransform.localScale = _skipBaseScale;
        }
    }

    private float EaseOutCubic(float t)
    {
        float p = 1f - t;
        return 1f - (p * p * p);
    }

    private float EaseInCubic(float t)
    {
        return t * t * t;
    }

    private float GetAdjustedDuration(float baseDuration)
    {
        float speedMultiplier = Mathf.Max(0.1f, GameRules.SwipeAnimationSpeedMultiplier);
        return Mathf.Max(0.01f, baseDuration / speedMultiplier);
    }
}