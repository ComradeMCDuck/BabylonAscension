using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// Opens and closes the debug panel with the TAB key and builds its contents.
/// Edited values are validated and written into the assigned EntityData; invalid input is rejected.
/// </summary>
public class DebugMenu : MonoBehaviour
{
    // Upper limit for values typed into the menu, so extreme input cannot break the physics.
    private const float MaxValue = 1000f;

    private struct Binding
    {
        public InputField Field;
        public Func<string> Read;
    }

    /// <summary>A copy of an enemy as it was when the level started, and the enemy currently spawned from it.</summary>
    private class EnemySpawn
    {
        public GameObject Template;
        public Transform Parent;
        public string Name;
        public GameObject Instance;
    }

    [Header("References")]
    [Tooltip("The panel on the left side of the screen that holds the debug options.")]
    [SerializeField] private GameObject debugPanel;
    [Tooltip("The data edited by the menu. Assign the same asset as on the player's BasicMovementController2D.")]
    [SerializeField] private EntityData playerData;

    [Header("Settings")]
    [SerializeField] private bool startOpen = false;

    [Header("Layout")]
    [SerializeField] private int fontSize = 16;
    [SerializeField] private float rowHeight = 28f;
    [SerializeField] private float valueFieldWidth = 110f;
    [SerializeField] private Color headerColor = new Color(1f, 0.85f, 0.4f);
    [SerializeField] private Color labelColor = Color.white;
    [SerializeField] private Color fieldColor = new Color(0.15f, 0.15f, 0.15f, 0.9f);

    private Font font;
    private RectTransform content;
    private readonly List<InputField> fields = new List<InputField>();
    private readonly List<Binding> bindings = new List<Binding>();
    private readonly List<EnemySpawn> enemySpawns = new List<EnemySpawn>();
    // Inactive parent of the enemy templates, so the copies never run (Awake, Start, FixedUpdate...).
    private Transform enemyTemplateRoot;

    private void Awake()
    {
        if (debugPanel == null)
        {
            Debug.LogWarning("DebugMenu has no panel assigned");
            return;
        }

        font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        content = CreateScrollView(debugPanel.transform);
        BuildMenu();

        debugPanel.SetActive(startOpen);

        if (startOpen)
        {
            RefreshFields();
        }
    }

    private void Start()
    {
        // In Start, so every enemy in the level has finished Awake and is still where it was placed.
        SnapshotEnemies();
    }

    private void Update()
    {
        if (debugPanel == null || Keyboard.current == null) return;

        if (Keyboard.current.tabKey.wasPressedThisFrame)
        {
            debugPanel.SetActive(!debugPanel.activeSelf);

            if (debugPanel.activeSelf)
            {
                RefreshFields();
            }
        }
    }

    /// <summary>
    /// Describes the sections and rows of the menu. Add new sections here.
    /// </summary>
    private void BuildMenu()
    {
        AddSection("Level");
        AddButton("Respawn Killed Enemies", RespawnKilledEnemies);

        AddSection("Player Data");

        if (playerData == null)
        {
            AddRow("No PlayerData assigned", "-");
            return;
        }

        EntityData d = playerData;

        AddFloatRow("Max Health", () => d.maxHealth, v => d.maxHealth = v, 1f);

        AddSection("Movement Settings");
        AddFloatRow("Speed", () => d.speed, v => d.speed = v);
        AddFloatRow("Accel Time Airborne", () => d.accelerationTimeAirborne, v => d.accelerationTimeAirborne = v);
        AddFloatRow("Accel Time Grounded", () => d.accelerationTimeGrounded, v => d.accelerationTimeGrounded = v);

        AddSection("On Ladder Settings");
        AddBoolRow("Snap To Restricted Area", () => d.snapToRestrictedArea, v => d.snapToRestrictedArea = v);
        AddBoolRow("Exit Ladder On Ground", () => d.exitLadderOnGround, v => d.exitLadderOnGround = v);
        AddFloatRow("On Ladder Speed", () => d.onLadderSpeed, v => d.onLadderSpeed = v);
        AddBoolRow("Lock Facing To Right", () => d.lockFacingToRight, v => d.lockFacingToRight = v);

        AddSection("Jump");
        AddBoolRow("Variable Jump Height", () => d.hasVariableJumpHeight, v => d.hasVariableJumpHeight = v);
        AddIntRow("Air Jump Allowed", () => d.airJumpAllowed, v => d.airJumpAllowed = v, 0, 100);
        AddFloatRow("Max Height", () => d.maxJumpHeight, v => d.maxJumpHeight = v);
        AddFloatRow("Min Height", () => d.minJumpHeight, v => d.minJumpHeight = v);
        AddFloatRow("Time To Apex", () => d.timeToApex, v => d.timeToApex = v, 0.01f);
        AddFloatRow("On Ladder Jump Force", () => d.onLadderJumpForce, v => d.onLadderJumpForce = v);
        AddFloatRow("Falling Jump Padding", () => d.fallingJumpPaddingTime, v => d.fallingJumpPaddingTime = v, 0f, 5f);
        AddFloatRow("Will Jump Padding", () => d.willJumpPaddingTime, v => d.willJumpPaddingTime = v, 0f, 5f);

        AddSection("Wall Interaction");
        AddBoolRow("Can Wall Slide", () => d.canWallSlide, v => d.canWallSlide = v);
        AddFloatRow("Wall Stick Time", () => d.wallStickTime, v => d.wallStickTime = v);
        AddFloatRow("Wall Slide Speed Loss", () => d.wallSlideSpeedLoss, v => d.wallSlideSpeedLoss = v);
        AddFloatRow("Wall Sliding Speed Max", () => d.wallSlidingSpeedMax, v => d.wallSlidingSpeedMax = v);
        AddBoolRow("Can Wall Climb", () => d.canWallClimb, v => d.canWallClimb = v);
        AddFloatRow("Wall Climb Speed", () => d.wallClimbSpeed, v => d.wallClimbSpeed = v);
        AddBoolRow("Can Wall Jump", () => d.canWallJump, v => d.canWallJump = v);
        AddFloatRow("Climb Force X", () => d.wallClimbForce.x, v => d.wallClimbForce.x = v, -MaxValue);
        AddFloatRow("Climb Force Y", () => d.wallClimbForce.y, v => d.wallClimbForce.y = v, -MaxValue);
        AddFloatRow("Off Force X", () => d.wallOffForce.x, v => d.wallOffForce.x = v, -MaxValue);
        AddFloatRow("Off Force Y", () => d.wallOffForce.y, v => d.wallOffForce.y = v, -MaxValue);
        AddFloatRow("Leap Force X", () => d.wallLeapForce.x, v => d.wallLeapForce.x = v, -MaxValue);
        AddFloatRow("Leap Force Y", () => d.wallLeapForce.y, v => d.wallLeapForce.y = v, -MaxValue);
        AddBoolRow("Can Grab Ledge", () => d.canGrabLedge, v => d.canGrabLedge = v);
        AddFloatRow("Ledge Detection Offset", () => d.ledgeDetectionOffset, v => d.ledgeDetectionOffset = v, 0f, 10f);

        AddSection("Recoil");
        AddFloatRow("Ground Acceleration", () => d.recoilGroundAcceleration, v => d.recoilGroundAcceleration = v);
        AddFloatRow("Ground Deceleration", () => d.recoilGroundDeceleration, v => d.recoilGroundDeceleration = v);
        AddFloatRow("Air Acceleration", () => d.recoilAirAcceleration, v => d.recoilAirAcceleration = v);
        AddFloatRow("Air Deceleration", () => d.recoilAirDeceleration, v => d.recoilAirDeceleration = v);
        AddFloatRow("Max Planar Speed", () => d.recoilMaxPlanarSpeed, v => d.recoilMaxPlanarSpeed = v);
        AddBoolRow("Cancel Falling On Up Impulse", () => d.cancelFallingOnUpwardImpulse, v => d.cancelFallingOnUpwardImpulse = v);

        AddSection("Knockback");
        AddFloatRow("Stun Time", () => d.knockbackStunTime, v => d.knockbackStunTime = v, 0f, 10f);
        AddFloatRow("Lift", () => d.knockbackLift, v => d.knockbackLift = v);
    }

    /// <summary>
    /// Stores an inactive copy of every enemy in the level, so killed ones can be recreated later.
    /// </summary>
    private void SnapshotEnemies()
    {
        GameObject root = new GameObject("DebugMenu_EnemyTemplates");
        root.SetActive(false);
        enemyTemplateRoot = root.transform;

        foreach (EnemyBehavior enemy in FindObjectsByType<EnemyBehavior>())
        {
            GameObject original = enemy.gameObject;

            enemySpawns.Add(new EnemySpawn
            {
                // Keeps the original's local position, rotation and scale, and its references to scene objects (e.g. patrol points).
                Template = Instantiate(original, enemyTemplateRoot, false),
                Parent = original.transform.parent,
                Name = original.name,
                Instance = original
            });
        }
    }

    /// <summary>
    /// Recreates every enemy that was killed, at its starting position with full health.
    /// Enemies that are still alive are left as they are.
    /// </summary>
    private void RespawnKilledEnemies()
    {
        int respawned = 0;

        foreach (EnemySpawn spawn in enemySpawns)
        {
            // Destroyed objects compare equal to null in Unity.
            if (spawn.Instance != null) continue;

            spawn.Instance = Instantiate(spawn.Template, spawn.Parent, false);
            spawn.Instance.name = spawn.Name;
            respawned++;
        }

        Debug.Log("Debug menu: respawned " + respawned + " killed enem" + (respawned == 1 ? "y" : "ies") + ".");
    }

    private void OnDestroy()
    {
        if (enemyTemplateRoot != null) Destroy(enemyTemplateRoot.gameObject);
    }

    /// <summary>
    /// Adds a full-width button to the menu.
    /// </summary>
    public void AddButton(string label, Action onClick)
    {
        GameObject buttonObject = new GameObject("Button_" + label, typeof(RectTransform));
        buttonObject.transform.SetParent(content, false);
        buttonObject.AddComponent<LayoutElement>().preferredHeight = rowHeight;

        Image background = buttonObject.AddComponent<Image>();
        background.color = fieldColor;

        Button button = buttonObject.AddComponent<Button>();
        button.targetGraphic = background;
        button.onClick.AddListener(() =>
        {
            // Deselect, otherwise the UI's submit key (e.g. Space for jumping) would press the button again.
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
            onClick();
        });

        Text text = CreateText("Text", buttonObject.transform, label, labelColor);
        text.alignment = TextAnchor.MiddleCenter;
        Stretch(text.rectTransform, 6f, 0f);
    }

    /// <summary>
    /// Adds a row bound to a float. Input outside [min, max] or not a number is rejected.
    /// </summary>
    public void AddFloatRow(string label, Func<float> get, Action<float> set, float min = 0f, float max = MaxValue)
    {
        AddBoundRow(label, () => Format(get()), input =>
        {
            if (!TryParseFloat(input, out float value))
            {
                return label + ": '" + input + "' is not a number";
            }

            if (value < min || value > max)
            {
                return label + ": must be between " + Format(min) + " and " + Format(max);
            }

            set(value);
            return null;
        });
    }

    /// <summary>
    /// Adds a row bound to an int. Input outside [min, max] or not a whole number is rejected.
    /// </summary>
    public void AddIntRow(string label, Func<int> get, Action<int> set, int min = 0, int max = int.MaxValue)
    {
        AddBoundRow(label, () => get().ToString(CultureInfo.InvariantCulture), input =>
        {
            if (!int.TryParse(input.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int value))
            {
                return label + ": '" + input + "' is not a whole number";
            }

            if (value < min || value > max)
            {
                return label + ": must be between " + min + " and " + max;
            }

            set(value);
            return null;
        });
    }

    /// <summary>
    /// Adds a row bound to a bool. Accepts true/false, 1/0, yes/no, on/off.
    /// </summary>
    public void AddBoolRow(string label, Func<bool> get, Action<bool> set)
    {
        AddBoundRow(label, () => get() ? "true" : "false", input =>
        {
            if (!TryParseBool(input, out bool value))
            {
                return label + ": '" + input + "' is not true or false";
            }

            set(value);
            return null;
        });
    }

    /// <summary>
    /// Adds a row whose field is written back through tryApply when editing ends.
    /// tryApply returns null on success, or an error message when the input is rejected.
    /// The field always ends up showing the value currently stored in the data.
    /// </summary>
    private void AddBoundRow(string label, Func<string> read, Func<string, string> tryApply)
    {
        InputField field = AddRow(label, read());

        field.onEndEdit.AddListener(input =>
        {
            string error = tryApply(input);

            if (error == null)
            {
                playerData.NotifyChanged();
            }
            else
            {
                Debug.LogWarning("Debug menu: " + error + ". Value was not changed.");
            }

            field.SetTextWithoutNotify(read());
        });

        bindings.Add(new Binding { Field = field, Read = read });
    }

    /// <summary>
    /// Adds a section title to the menu.
    /// </summary>
    public void AddSection(string title)
    {
        Text header = CreateText("Section_" + title, content, title, headerColor);
        header.fontStyle = FontStyle.Bold;
        header.fontSize = fontSize + 2;
        header.gameObject.AddComponent<LayoutElement>().preferredHeight = rowHeight + 6f;
    }

    /// <summary>
    /// Adds a row with the variable name on the left and an editable text field on the right.
    /// </summary>
    public InputField AddRow(string label, string value)
    {
        GameObject row = new GameObject("Row_" + label, typeof(RectTransform));
        row.transform.SetParent(content, false);
        row.AddComponent<LayoutElement>().preferredHeight = rowHeight;

        HorizontalLayoutGroup rowLayout = row.AddComponent<HorizontalLayoutGroup>();
        rowLayout.spacing = 8f;
        rowLayout.childControlWidth = true;
        rowLayout.childControlHeight = true;
        rowLayout.childForceExpandWidth = false;
        rowLayout.childForceExpandHeight = true;

        Text labelText = CreateText("Label", row.transform, label, labelColor);
        labelText.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;

        return CreateInputField(row.transform, value);
    }

    private InputField CreateInputField(Transform parent, string value)
    {
        GameObject fieldObject = new GameObject("Value", typeof(RectTransform));
        fieldObject.transform.SetParent(parent, false);

        Image background = fieldObject.AddComponent<Image>();
        background.color = fieldColor;

        LayoutElement layout = fieldObject.AddComponent<LayoutElement>();
        layout.preferredWidth = valueFieldWidth;
        layout.flexibleWidth = 0f;

        Text text = CreateText("Text", fieldObject.transform, string.Empty, labelColor);
        text.supportRichText = false;
        text.alignment = TextAnchor.MiddleRight;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        Stretch(text.rectTransform, 6f, 0f);

        InputField field = fieldObject.AddComponent<InputField>();
        field.targetGraphic = background;
        field.textComponent = text;
        field.text = value;
        fields.Add(field);

        return field;
    }

    private Text CreateText(string name, Transform parent, string value, Color color)
    {
        GameObject textObject = new GameObject(name, typeof(RectTransform));
        textObject.transform.SetParent(parent, false);

        Text text = textObject.AddComponent<Text>();
        text.font = font;
        text.fontSize = fontSize;
        text.color = color;
        text.alignment = TextAnchor.MiddleLeft;
        text.text = value;

        return text;
    }

    /// <summary>
    /// Creates a scrollable area that fills the panel and returns the object rows are added to.
    /// </summary>
    private RectTransform CreateScrollView(Transform parent)
    {
        GameObject scrollObject = new GameObject("ScrollView", typeof(RectTransform));
        scrollObject.transform.SetParent(parent, false);
        Stretch((RectTransform)scrollObject.transform, 12f);

        GameObject viewportObject = new GameObject("Viewport", typeof(RectTransform));
        viewportObject.transform.SetParent(scrollObject.transform, false);
        RectTransform viewport = (RectTransform)viewportObject.transform;
        Stretch(viewport, 0f);
        viewportObject.AddComponent<RectMask2D>();

        GameObject contentObject = new GameObject("Content", typeof(RectTransform));
        contentObject.transform.SetParent(viewport, false);
        RectTransform contentRect = (RectTransform)contentObject.transform;
        contentRect.anchorMin = new Vector2(0f, 1f);
        contentRect.anchorMax = new Vector2(1f, 1f);
        contentRect.pivot = new Vector2(0.5f, 1f);
        contentRect.offsetMin = Vector2.zero;
        contentRect.offsetMax = Vector2.zero;

        VerticalLayoutGroup contentLayout = contentObject.AddComponent<VerticalLayoutGroup>();
        contentLayout.spacing = 4f;
        contentLayout.childControlWidth = true;
        contentLayout.childControlHeight = true;
        contentLayout.childForceExpandWidth = true;
        contentLayout.childForceExpandHeight = false;

        ContentSizeFitter fitter = contentObject.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        ScrollRect scroll = scrollObject.AddComponent<ScrollRect>();
        scroll.viewport = viewport;
        scroll.content = contentRect;
        scroll.horizontal = false;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 20f;

        return contentRect;
    }

    private static void Stretch(RectTransform rect, float padding)
    {
        Stretch(rect, padding, padding);
    }

    private static void Stretch(RectTransform rect, float horizontalPadding, float verticalPadding)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(horizontalPadding, verticalPadding);
        rect.offsetMax = new Vector2(-horizontalPadding, -verticalPadding);
    }

    /// <summary>
    /// Sizes the layout and redraws the field texts. Fields filled before the layout was sized show up empty otherwise.
    /// </summary>
    private void RefreshFields()
    {
        // values may have been changed elsewhere (e.g. the Inspector) while the menu was closed
        foreach (Binding binding in bindings)
        {
            binding.Field.SetTextWithoutNotify(binding.Read());
        }

        LayoutRebuilder.ForceRebuildLayoutImmediate(content);

        foreach (InputField field in fields)
        {
            field.ForceLabelUpdate();
        }
    }

    private static string Format(float value)
    {
        return value.ToString("0.###", CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Parses a float written with either '.' or ',' as the decimal separator. Rejects NaN and infinity.
    /// </summary>
    private static bool TryParseFloat(string input, out float value)
    {
        string normalized = input.Trim().Replace(',', '.');

        if (!float.TryParse(normalized, NumberStyles.Float, CultureInfo.InvariantCulture, out value))
        {
            return false;
        }

        return !float.IsNaN(value) && !float.IsInfinity(value);
    }

    private static bool TryParseBool(string input, out bool value)
    {
        switch (input.Trim().ToLowerInvariant())
        {
            case "true":
            case "1":
            case "yes":
            case "on":
                value = true;
                return true;
            case "false":
            case "0":
            case "no":
            case "off":
                value = false;
                return true;
            default:
                value = false;
                return false;
        }
    }
}
