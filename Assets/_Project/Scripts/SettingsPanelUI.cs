using UnityEngine;
using UnityEngine.UI;

public class SettingsPanelUI : MonoBehaviour
{
    [Header("Panel")]
    [SerializeField] private CanvasGroup panel;
    [SerializeField] private bool hideOnStart = true;

    [Header("Volume")]
    [SerializeField] private Slider musicSlider;
    [SerializeField] private SoundManager soundManager;

    private void Awake()
    {
        if (panel == null)
        {
            panel = GetComponent<CanvasGroup>();
        }

        if (soundManager == null)
        {
            soundManager = FindObjectOfType<SoundManager>();
        }

        if (hideOnStart)
        {
            SetVisible(false);
        }

        BindMusicSlider();
        SyncVolumeSlider();
    }

    private void OnEnable()
    {
        SyncVolumeSlider();
    }

    public void Open()
    {
        SetVisible(true);
        SyncVolumeSlider();
    }

    public void Close()
    {
        SetVisible(false);
    }

    public void OnVolumeChanged(float value)
    {
        SetMusicVolume(value);
    }

    public void OnMusicSliderChanged(float value)
    {
        SetMusicVolume(value);
    }

    private void SyncVolumeSlider()
    {
        if (musicSlider == null || soundManager == null)
        {
            return;
        }

        musicSlider.SetValueWithoutNotify(soundManager.GetUserVolume());
    }

    private void SetVisible(bool isVisible)
    {
        if (panel == null)
        {
            return;
        }

        panel.alpha = isVisible ? 1f : 0f;
        panel.interactable = isVisible;
        panel.blocksRaycasts = isVisible;
    }

    private void BindMusicSlider()
    {
        if (musicSlider == null)
        {
            return;
        }

        musicSlider.onValueChanged.RemoveListener(OnMusicSliderChanged);
        musicSlider.onValueChanged.AddListener(OnMusicSliderChanged);
    }

    private void SetMusicVolume(float value)
    {
        if (soundManager == null)
        {
            return;
        }

        soundManager.SetUserVolume(value);
    }
}
