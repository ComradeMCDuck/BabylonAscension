using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// First-run question in the main menu: may the game send anonymous gameplay events? Built in code with legacy UGUI
/// (like DebugMenu), so no scene has to be edited. It appears only while analytics is configured
/// (GameAnalytics.IsActive) and the player has not answered yet, and it is destroyed with the answer.
/// The menu scene must have an EventSystem for the buttons to work (MainMenu has one).
/// </summary>
public static class AnalyticsConsentPrompt
{
    // Czech, one sentence, says the data are anonymous gameplay events.
    private const string Question = "Smíme pro výuku game designu sbírat anonymní herní události (úmrtí, výstřely, dokončené úrovně), bez jakýchkoli osobních údajů?";
    private const string YesLabel = "Ano";
    private const string NoLabel = "Ne";

    private const int SortingOrder = 1000;     // Above every menu canvas.
    private const float PanelWidth = 900f;
    private const float PanelHeight = 150f;
    private const float PanelBottomMargin = 40f;
    private const float ButtonWidth = 140f;
    private const float ButtonHeight = 40f;
    private const float ButtonBottomMargin = 15f;
    private const int FontSize = 22;
    private static readonly Vector2 ReferenceResolution = new Vector2(1920f, 1080f);
    private static readonly Color PanelColor = new Color(0f, 0f, 0f, 0.85f);
    private static readonly Color ButtonColor = new Color(0.25f, 0.25f, 0.25f, 1f);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Boot()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (mode != LoadSceneMode.Single || !GameAnalyticsHooks.IsMenuScene(scene.name)) return;
        if (!GameAnalytics.IsActive || GameAnalytics.ConsentAnswered) return;
        Build();
    }

    private static void Build()
    {
        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        GameObject root = new GameObject("AnalyticsConsentPrompt", typeof(RectTransform));
        Canvas canvas = root.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = SortingOrder;
        CanvasScaler scaler = root.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = ReferenceResolution;
        root.AddComponent<GraphicRaycaster>();

        GameObject panel = new GameObject("Panel", typeof(RectTransform));
        panel.transform.SetParent(root.transform, false);
        panel.AddComponent<Image>().color = PanelColor;
        RectTransform panelRect = panel.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.5f, 0f);
        panelRect.anchorMax = new Vector2(0.5f, 0f);
        panelRect.pivot = new Vector2(0.5f, 0f);
        panelRect.anchoredPosition = new Vector2(0f, PanelBottomMargin);
        panelRect.sizeDelta = new Vector2(PanelWidth, PanelHeight);

        Text text = CreateText("Question", panel.transform, font, Question);
        text.alignment = TextAnchor.UpperCenter;
        RectTransform textRect = text.rectTransform;
        textRect.anchorMin = new Vector2(0f, 0.45f);
        textRect.anchorMax = new Vector2(1f, 1f);
        textRect.offsetMin = new Vector2(20f, 0f);
        textRect.offsetMax = new Vector2(-20f, -15f);

        CreateButton(panel.transform, font, YesLabel, -ButtonWidth * 0.6f, () => Answer(root, true));
        CreateButton(panel.transform, font, NoLabel, ButtonWidth * 0.6f, () => Answer(root, false));
    }

    private static void Answer(GameObject root, bool granted)
    {
        GameAnalytics.SetConsent(granted);
        Object.Destroy(root);
    }

    private static Text CreateText(string name, Transform parent, Font font, string value)
    {
        GameObject textObject = new GameObject(name, typeof(RectTransform));
        textObject.transform.SetParent(parent, false);
        Text text = textObject.AddComponent<Text>();
        text.font = font;
        text.fontSize = FontSize;
        text.color = Color.white;
        text.alignment = TextAnchor.MiddleCenter;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.text = value;
        return text;
    }

    private static void CreateButton(Transform parent, Font font, string label, float x, UnityEngine.Events.UnityAction onClick)
    {
        GameObject buttonObject = new GameObject("Button_" + label, typeof(RectTransform));
        buttonObject.transform.SetParent(parent, false);
        buttonObject.AddComponent<Image>().color = ButtonColor;
        Button button = buttonObject.AddComponent<Button>();
        button.onClick.AddListener(onClick);

        RectTransform rect = buttonObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0f);
        rect.anchorMax = new Vector2(0.5f, 0f);
        rect.pivot = new Vector2(0.5f, 0f);
        rect.anchoredPosition = new Vector2(x, ButtonBottomMargin);
        rect.sizeDelta = new Vector2(ButtonWidth, ButtonHeight);

        Text text = CreateText("Label", buttonObject.transform, font, label);
        text.rectTransform.anchorMin = Vector2.zero;
        text.rectTransform.anchorMax = Vector2.one;
        text.rectTransform.offsetMin = Vector2.zero;
        text.rectTransform.offsetMax = Vector2.zero;
    }
}
