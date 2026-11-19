using TMPro;
using UnityEngine;

/// <summary>
/// HUD text for the special arrow: which one is picked, whether it is charged or armed (Q), and the kills
/// still needed to recharge it. Put it on a TMP text in the HUD Canvas.
/// </summary>
public class SpecialArrowUI : MonoBehaviour
{
    [SerializeField] private TMP_Text label;
    [Tooltip("Shown when the special arrow is charged. {0} = arrow name.")]
    [SerializeField] private string readyFormat = "{0}: ready (Q)";
    [Tooltip("Shown while the next shot fires the special arrow. {0} = arrow name.")]
    [SerializeField] private string armedFormat = "{0}: ARMED";
    [Tooltip("Shown while recharging. {0} = arrow name, {1} = kills counted, {2} = kills needed.")]
    [SerializeField] private string chargingFormat = "{0}: {1}/{2} kills";
    [Tooltip("Shown while no special arrow is picked. Empty hides the text.")]
    [SerializeField] private string noArrowText = "";
    [Tooltip("Text color while armed.")]
    [SerializeField] private Color armedColor = new Color(1f, 0.8f, 0.2f);

    private BowCombat bow;
    private Color normalColor;

    private void Awake()
    {
        if (label == null) label = GetComponent<TMP_Text>();
        bow = FindAnyObjectByType<BowCombat>();
        if (label == null || bow == null)
        {
            Debug.LogError("SpecialArrowUI needs a TMP text and a BowCombat on the player.", this);
            enabled = false;
            return;
        }
        normalColor = label.color;
    }

    private void OnEnable()
    {
        if (bow == null) return;
        bow.SpecialArrowChanged += Refresh;
        Refresh();
    }

    private void OnDisable()
    {
        if (bow != null) bow.SpecialArrowChanged -= Refresh;
    }

    private void Refresh()
    {
        GameObject arrow = bow.SpecialArrowPrefab;
        if (arrow == null)
        {
            label.text = noArrowText;
            label.color = normalColor;
            return;
        }

        string arrowName = arrow.name;
        if (bow.IsSpecialArmed)
            label.text = string.Format(armedFormat, arrowName);
        else if (bow.IsSpecialReady)
            label.text = string.Format(readyFormat, arrowName);
        else
            label.text = string.Format(chargingFormat, arrowName, bow.SpecialKills, bow.SpecialKillsNeeded);

        label.color = bow.IsSpecialArmed ? armedColor : normalColor;
    }
}
