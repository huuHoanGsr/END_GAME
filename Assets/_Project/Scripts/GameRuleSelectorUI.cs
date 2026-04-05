using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class MinigameModeSelectorUI : MonoBehaviour
{
    [Header("BG Panel")]
    [SerializeField] private GameObject bgPanel;

    [Header("UI")]
    [SerializeField] private TMP_Dropdown modeDropdown;
    [SerializeField] private Text descriptionText;

    private void Awake()
    {
        if (modeDropdown == null)
            modeDropdown = GetComponentInChildren<TMP_Dropdown>();

        if (modeDropdown != null)
        {
            modeDropdown.onValueChanged.RemoveListener(OnDropdownChanged);
            modeDropdown.onValueChanged.AddListener(OnDropdownChanged);
            ApplyFromDropdown();
        }
    }

    private void OnDestroy()
    {
        if (modeDropdown != null)
            modeDropdown.onValueChanged.RemoveListener(OnDropdownChanged);
    }

    private void OnDropdownChanged(int index)
    {
        ApplySelectedMode(index);
        UpdateDescription();
    }

    public void ApplyFromDropdown()
    {
        if (modeDropdown != null)
        {
            ApplySelectedMode(modeDropdown.value);
        }

        UpdateDescription();
    }

    public void StartGameWithSelectedMode()
    {
        if (modeDropdown != null)
        {
            ApplySelectedMode(modeDropdown.value);
        }

        if (bgPanel != null)
        {
            bgPanel.SetActive(false);
        }

        GameEvents.OnRestartRequested?.Invoke();
    }

    private void UpdateDescription()
    {
        if (descriptionText == null) return;

        switch (GameRules.SelectedMode)
        {
            case GameRuleMode.TimeLimit3Min:
                descriptionText.text = "3 phút: số câu ít hơn, nhịp nhanh.";
                break;
            case GameRuleMode.TimeLimit5Min:
                descriptionText.text = "5 phút: chế độ tiêu chuẩn, cân bằng nhất.";
                break;
            case GameRuleMode.SpeedRunMax5Min:
                descriptionText.text = "Speed Run: tối đa số câu trong 5 phút.";
                break;
            default:
                descriptionText.text = "Chọn chế độ trước khi bắt đầu.";
                break;
        }
    }

    private void ApplySelectedMode(int index)
    {
        GameRuleMode mode;
        switch (index)
        {
            case 0:
                mode = GameRuleMode.TimeLimit3Min;
                break;
            case 1:
                mode = GameRuleMode.TimeLimit5Min;
                break;
            case 2:
                mode = GameRuleMode.SpeedRunMax5Min;
                break;
            default:
                mode = GameRuleMode.TimeLimit5Min;
                break;
        }

        GameRules.SetMode(mode);
    }
}
