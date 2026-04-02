using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class GameRuleSelectorUI : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private TMP_Dropdown modeDropdown;
    [SerializeField] private TextMeshProUGUI descriptionText;

    private void Awake()
    {
        if (modeDropdown == null)
        {
            modeDropdown = GetComponentInChildren<TMP_Dropdown>();
        }

        if (modeDropdown != null)
        {
            modeDropdown.onValueChanged.RemoveListener(HandleModeChanged);
            modeDropdown.onValueChanged.AddListener(HandleModeChanged);
            ApplyFromDropdown();
        }
    }

    private void OnDestroy()
    {
        if (modeDropdown != null)
        {
            modeDropdown.onValueChanged.RemoveListener(HandleModeChanged);
        }
    }

    private void HandleModeChanged(int index)
    {
        GameRuleMode mode = IndexToMode(index);
        GameRules.SetMode(mode);
        UpdateDescription(mode);
    }

    private void ApplyFromDropdown()
    {
        GameRuleMode mode = IndexToMode(modeDropdown.value);
        GameRules.SetMode(mode);
        UpdateDescription(mode);
    }

    private GameRuleMode IndexToMode(int index)
    {
        switch (index)
        {
            case 0:
                return GameRuleMode.TimeLimit3Min;
            case 1:
                return GameRuleMode.TimeLimit5Min;
            case 2:
            default:
                return GameRuleMode.SpeedRunMax5Min;
        }
    }

    private void UpdateDescription(GameRuleMode mode)
    {
        if (descriptionText == null)
        {
            return;
        }

        switch (mode)
        {
            case GameRuleMode.TimeLimit3Min:
                descriptionText.text = "Gioi han 3 phut. Het gio se thua.";
                break;
            case GameRuleMode.TimeLimit5Min:
                descriptionText.text = "Gioi han 5 phut. Het gio se thua.";
                break;
            case GameRuleMode.SpeedRunMax5Min:
            default:
                descriptionText.text = "Hoan thanh nhanh nhat, toi da 5 phut.";
                break;
        }
    }
}
