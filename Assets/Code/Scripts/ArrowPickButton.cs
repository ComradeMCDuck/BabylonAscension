using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// An arrow icon in the altar window. Clicking it makes the player's bow fire this arrow.
/// Every button except the picked arrow's is greyed out (all of them before the first pick),
/// but they stay clickable so the player can change their mind.
/// </summary>
[RequireComponent(typeof(Button))]
public class ArrowPickButton : MonoBehaviour
{
    [Tooltip("The arrow prefab the bow fires after this button is clicked.")]
    [SerializeField] private GameObject arrowPrefab;
    [Tooltip("Multiplied into the color of every image and text on the button while another arrow is picked.")]
    [SerializeField] private Color greyedOutTint = new Color(0.35f, 0.35f, 0.35f, 1f);

    private Button button;
    private BowCombat bow;
    private Graphic[] graphics;
    private Color[] originalColors;

    private void Awake()
    {
        button = GetComponent<Button>();
        graphics = GetComponentsInChildren<Graphic>(true);
        originalColors = new Color[graphics.Length];
        for (int i = 0; i < graphics.Length; i++)
            originalColors[i] = graphics[i].color;

        bow = FindAnyObjectByType<BowCombat>();
        if (bow == null)
            Debug.LogError("ArrowPickButton can't find the player's BowCombat.", this);
        if (arrowPrefab == null)
            Debug.LogError("Assign an arrow prefab to ArrowPickButton.", this);
    }

    private void OnEnable()
    {
        button.onClick.AddListener(Pick);
        if (bow != null) bow.ArrowChanged += OnArrowChanged;
        Refresh();
    }

    private void OnDisable()
    {
        button.onClick.RemoveListener(Pick);
        if (bow != null) bow.ArrowChanged -= OnArrowChanged;
    }

    private void Pick()
    {
        if (bow != null && arrowPrefab != null) bow.SetArrow(arrowPrefab);
    }

    private void OnArrowChanged(GameObject newArrow) => Refresh();

    private void Refresh()
    {
        if (bow == null) return;
        // Only the picked arrow is lit; while the player still has the basic arrow, every button is greyed out.
        bool greyedOut = bow.CurrentArrowPrefab != arrowPrefab;
        for (int i = 0; i < graphics.Length; i++)
            graphics[i].color = greyedOut ? originalColors[i] * greyedOutTint : originalColors[i];
    }
}
