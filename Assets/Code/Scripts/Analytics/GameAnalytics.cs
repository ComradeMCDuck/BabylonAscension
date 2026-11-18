using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;

/// <summary>
/// Sends gameplay events to Google Analytics 4 over the Measurement Protocol (one HTTP POST per batch), because the
/// Firebase Unity SDK does not support Analytics on desktop. The events are exported to BigQuery by GA4 itself.
///
/// Use it from anywhere: GameAnalytics.Log("player_death", ("level", name), ("x", 12.5f)). It does nothing when
/// the config file is missing, the player has not given consent, or the game runs in the Editor without sendInEditor.
///
/// Config: StreamingAssets/analytics.json (NOT committed, it holds the API secret, see analytics.example.json).
/// Not supported yet: WebGL (see BalanceLoader for the same reason; the POST itself would work, the config loading would not).
/// </summary>
public class GameAnalytics : MonoBehaviour
{
    private const string ConfigFile = "analytics.json";
    private const string ConsentKey = "analytics_consent";
    private const string ClientIdKey = "analytics_client_id";
    private const float FlushInterval = 10f;
    private const int FlushThreshold = 20;
    private const int MaxBatch = 25;          // GA4 accepts at most 25 events per request.
    private const int MaxParams = 25;         // ...and 25 parameters per event.
    private const int MaxQueue = 500;
    private const int MaxAttempts = 3;
    private const int MaxNameLength = 40;     // Event and parameter names.
    private const int MaxStringLength = 100;  // Parameter values on standard properties.
    private const long MaxAgeMicros = 70L * 3600L * 1000000L; // GA4 accepts events backdated up to 72 hours; 70 leaves a margin.
    private const double MaxEngagementMs = 60000;

    [Serializable]
    private class Config
    {
        public bool enabled = true;
        public string measurementId = "";
        public string apiSecret = "";
        [Tooltip("Use the EU collection endpoint.")]
        public bool euEndpoint = true;
        [Tooltip("Events are dropped until the player says yes (GameAnalytics.SetConsent).")]
        public bool requireConsent = true;
        [Tooltip("Editor play mode only prints the events, unless this is true.")]
        public bool sendInEditor = false;
        [Tooltip("Print every event to the console.")]
        public bool logEvents = true;
        [Tooltip("Post to GA4's validation endpoint (/debug/mp/collect) and print its validation messages. Such events never reach reports or BigQuery; use it to check the payload, then turn it off.")]
        public bool validateOnly = false;
        [Tooltip("Adds debug_mode=1 to every event, so GA4 shows them in DebugView. Turn off for real playtests.")]
        public bool debugMode = false;
    }

    private class Pending
    {
        public string json;
        public long timestampMicros;
        public int attempts;
    }

    private static GameAnalytics instance;
    private static Config config;

    private readonly List<Pending> queue = new List<Pending>();
    private bool canSend;
    private bool sending;
    // Dropped events are silent otherwise, which looks exactly like "analytics is broken".
    private bool warnedNoConsent;
    private long sessionId;
    private long lastMicros;
    private int seq;
    private double lastEventTime;
    private float nextFlush;

    /// <summary>True when the player has agreed (or the config does not require it).</summary>
    public static bool HasConsent => config == null || !config.requireConsent || PlayerPrefs.GetInt(ConsentKey, 0) == 1;

    /// <summary>True once the player has answered the consent question, either way. Show the prompt while this is false.</summary>
    public static bool ConsentAnswered => PlayerPrefs.HasKey(ConsentKey);

    /// <summary>False when no config file was found or analytics is disabled in it.</summary>
    public static bool IsActive => instance != null;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Boot()
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        return;
#else
        // With "Enter Play Mode > Reload Domain" off the statics survive between plays; start clean.
        instance = null;
        config = null;

        string path = Path.Combine(Application.streamingAssetsPath, ConfigFile);
        if (!File.Exists(path))
        {
            Debug.Log($"GameAnalytics: {ConfigFile} not found in StreamingAssets, analytics is off.");
            return;
        }

        try
        {
            config = JsonUtility.FromJson<Config>(File.ReadAllText(path));
        }
        catch (ArgumentException ex)
        {
            Debug.LogWarning($"GameAnalytics: {ConfigFile} is not valid JSON, analytics is off. {ex.Message}");
            return;
        }
        if (config == null || !config.enabled) return;

        bool allowedHere = !Application.isEditor || config.sendInEditor;
        bool configured = !string.IsNullOrEmpty(config.measurementId) && !string.IsNullOrEmpty(config.apiSecret);
        if (allowedHere && !configured) Debug.LogWarning($"GameAnalytics: measurementId or apiSecret is missing in {ConfigFile}, events are only logged.");

        GameObject host = new GameObject("GameAnalytics") { hideFlags = HideFlags.HideInHierarchy };
        DontDestroyOnLoad(host);
        instance = host.AddComponent<GameAnalytics>();
        instance.canSend = allowedHere && configured;
        instance.sessionId = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        instance.lastEventTime = Time.realtimeSinceStartupAsDouble;
        // Ids from earlier test builds were GUIDs, which a Web stream rejects. Drop them, so a valid id and a
        // first_launch are created instead.
        if (PlayerPrefs.HasKey(ClientIdKey) && !IsValidClientId(PlayerPrefs.GetString(ClientIdKey)))
            PlayerPrefs.DeleteKey(ClientIdKey);
        instance.EnsureInstallEvent();

        // One line that says what will happen to events in this session, so a silent setup problem is visible.
        string sending = instance.canSend
            ? (config.validateOnly ? "to the GA4 validation endpoint only (validateOnly, nothing is recorded)" : "to GA4")
            : (allowedHere ? "nowhere (measurementId or apiSecret missing)" : "nowhere, console only (Editor without sendInEditor)");
        string consent = HasConsent ? "given" : ConsentAnswered ? "refused (events are dropped)" : "not answered yet (events are dropped until the player clicks Yes)";
        Debug.Log($"GameAnalytics: on. Events go {sending}. Consent: {consent}.{(config.debugMode ? " debugMode is on (DebugView)." : "")}");
#endif
    }

    /// <summary>Remembers the player's answer. Saying no also deletes the queued events and the anonymous id.</summary>
    public static void SetConsent(bool granted)
    {
        PlayerPrefs.SetInt(ConsentKey, granted ? 1 : 0);
        if (!granted)
        {
            PlayerPrefs.DeleteKey(ClientIdKey);
            if (instance != null) instance.queue.Clear();
        }
        PlayerPrefs.Save();
        if (granted && instance != null) instance.EnsureInstallEvent();
    }

    /// <summary>
    /// Creates the anonymous id the first time analytics is allowed and logs a one-time first_launch event with device info
    /// (the desktop counterpart of app_install on mobile; app_install and first_open are reserved GA4 names). Runs at start
    /// and right after consent is granted, so the event is sent exactly once per id: a player who withdraws consent and
    /// agrees again gets a new id and a new first_launch.
    /// Deliberately no device name or unique device id: those can identify a person.
    /// </summary>
    private void EnsureInstallEvent()
    {
        if (PlayerPrefs.HasKey(ClientIdKey) || !HasConsent) return;
        PlayerPrefs.SetString(ClientIdKey, NewClientId());
        PlayerPrefs.Save();

        Resolution resolution = Screen.currentResolution;
        Enqueue("first_launch", new (string key, object value)[]
        {
            // The BigQuery export always says WEB (Web data stream), so the real platform is its own parameter.
            ("platform", Application.platform.ToString()),
            ("os", SystemInfo.operatingSystem),
            ("language", Application.systemLanguage.ToString()),
            // On desktop this most likely only tells reachable from not reachable, not Wi-Fi from cable.
            ("connection", Application.internetReachability.ToString()),
            ("screen", resolution.width + "x" + resolution.height),
            ("gpu", SystemInfo.graphicsDeviceName),
            ("ram_mb", SystemInfo.systemMemorySize),
            ("cpu_cores", SystemInfo.processorCount),
        });
    }

    /// <summary>
    /// Queues an event. Names: letters, digits and underscores, at most 40 characters, not a GA4 reserved name.
    /// Values: string, number, bool (sent as 1/0) or enum. Never put personal data in them.
    /// </summary>
    public static void Log(string eventName, params (string key, object value)[] parameters)
    {
        if (instance != null) instance.Enqueue(eventName, parameters);
    }

    /// <summary>Sends the queued events now (they are also sent every few seconds and on scene loads).</summary>
    public static void Flush()
    {
        if (instance != null) instance.FlushQueue();
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        FlushQueue();
    }

    private void Update()
    {
        if (Time.unscaledTime < nextFlush) return;
        nextFlush = Time.unscaledTime + FlushInterval;
        FlushQueue();
    }

    private void Enqueue(string eventName, (string key, object value)[] parameters)
    {
        if (string.IsNullOrEmpty(eventName)) return;
        if (!HasConsent)
        {
            if (!warnedNoConsent)
            {
                warnedNoConsent = true;
                Debug.LogWarning($"GameAnalytics: '{eventName}' and all further events are dropped: the player has not agreed to analytics. Answer the consent prompt with \"Yes\".");
            }
            return;
        }

        double now = Time.realtimeSinceStartupAsDouble;
        long engagementMs = (long)Math.Max(1.0, Math.Min(MaxEngagementMs, (now - lastEventTime) * 1000.0));
        lastEventTime = now;
        seq++;

        // Every event gets its own timestamp, so BigQuery can order a player's path exactly.
        // seq is a backup for ordering if timestamps ever collide.
        long micros = NextTimestamp();
        StringBuilder sb = new StringBuilder(200);
        sb.Append("{\"name\":").Append(Quote(Truncate(eventName, MaxNameLength)));
        sb.Append(",\"timestamp_micros\":").Append(micros.ToString(CultureInfo.InvariantCulture));
        sb.Append(",\"params\":{");

        int count = 0;
        AppendParam(sb, "session_id", sessionId, ref count);
        AppendParam(sb, "engagement_time_msec", engagementMs, ref count);
        AppendParam(sb, "seq", seq, ref count);
        AppendParam(sb, "balance_version", BalanceLoader.Version, ref count);
        AppendParam(sb, "build", Application.version, ref count);
        if (config.debugMode) AppendParam(sb, "debug_mode", 1, ref count);
        if (parameters != null)
            foreach ((string key, object value) in parameters) AppendParam(sb, key, value, ref count);
        sb.Append("}}");

        string json = sb.ToString();
        if (config.logEvents) Debug.Log("[Analytics] " + json);

        queue.Add(new Pending { json = json, timestampMicros = micros });
        if (queue.Count > MaxQueue) queue.RemoveRange(0, queue.Count - MaxQueue);
        if (queue.Count >= FlushThreshold) FlushQueue();
    }

    private void FlushQueue()
    {
        if (sending || queue.Count == 0) return;
        if (!canSend)
        {
            queue.Clear();
            return;
        }

        long cutoff = NowMicros() - MaxAgeMicros;
        queue.RemoveAll(pending => pending.timestampMicros < cutoff);
        if (queue.Count == 0) return;

        int size = Math.Min(MaxBatch, queue.Count);
        List<Pending> batch = queue.GetRange(0, size);
        queue.RemoveRange(0, size);
        StartCoroutine(Send(batch));
    }

    private IEnumerator Send(List<Pending> batch)
    {
        sending = true;

        string host = config.euEndpoint ? "region1.google-analytics.com" : "www.google-analytics.com";
        // The real endpoint answers 2xx whatever the payload; only the debug endpoint reports what is wrong.
        string path = config.validateOnly ? "/debug/mp/collect" : "/mp/collect";
        string url = "https://" + host + path + "?measurement_id=" + UnityWebRequest.EscapeURL(config.measurementId)
                     + "&api_secret=" + UnityWebRequest.EscapeURL(config.apiSecret);

        StringBuilder body = new StringBuilder(512);
        body.Append("{\"client_id\":").Append(Quote(ClientId));
        if (config.validateOnly) body.Append(",\"validation_behavior\":\"ENFORCE_RECOMMENDATIONS\"");
        body.Append(",\"events\":[");
        for (int i = 0; i < batch.Count; i++)
        {
            if (i > 0) body.Append(',');
            body.Append(batch[i].json);
        }
        body.Append("]}");

        using (UnityWebRequest request = new UnityWebRequest(url, UnityWebRequest.kHttpVerbPOST))
        {
            request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(body.ToString()));
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                if (config.validateOnly)
                    Debug.Log($"GameAnalytics: validated {batch.Count} event(s) (not sent to reports). Response: {request.downloadHandler.text}");
                else if (config.logEvents)
                    Debug.Log($"GameAnalytics: sent {batch.Count} event(s), HTTP {request.responseCode}.");
            }
            else if (request.result == UnityWebRequest.Result.ConnectionError || request.responseCode >= 500)
            {
                // Network trouble: try again later, but give up after a few attempts.
                List<Pending> again = new List<Pending>();
                foreach (Pending pending in batch)
                {
                    pending.attempts++;
                    if (pending.attempts < MaxAttempts) again.Add(pending);
                }
                queue.InsertRange(0, again);
            }
            else
            {
                // GA4 says a rejected request must be fixed, not retried unchanged.
                Debug.LogWarning($"GameAnalytics: request rejected (HTTP {request.responseCode}), {batch.Count} event(s) dropped.");
            }
        }

        sending = false;
    }

    private static string ClientId
    {
        get
        {
            string id = PlayerPrefs.GetString(ClientIdKey, "");
            if (string.IsNullOrEmpty(id))
            {
                id = NewClientId();
                PlayerPrefs.SetString(ClientIdKey, id);
                PlayerPrefs.Save();
            }
            return id;
        }
    }

    /// <summary>
    /// Random anonymous id in the "number.number" format that GA4 requires for Web streams (the validator rejects other
    /// formats, GUIDs included): a random 32-bit number, a dot, and the creation time in Unix seconds, like the _ga cookie.
    /// The random part comes from a GUID, not UnityEngine.Random, so a seeded game RNG can never give two players the same id.
    /// </summary>
    private static string NewClientId()
    {
        uint random = BitConverter.ToUInt32(Guid.NewGuid().ToByteArray(), 0);
        return random.ToString(CultureInfo.InvariantCulture) + "." + DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture);
    }

    private static bool IsValidClientId(string id)
    {
        int dot = id.IndexOf('.');
        return dot > 0 && dot < id.Length - 1
            && ulong.TryParse(id.Substring(0, dot), NumberStyles.None, CultureInfo.InvariantCulture, out _)
            && ulong.TryParse(id.Substring(dot + 1), NumberStyles.None, CultureInfo.InvariantCulture, out _);
    }

    private static long NowMicros()
    {
        return DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() * 1000L;
    }

    /// <summary>Strictly increasing, so two events in the same millisecond keep their order.</summary>
    private long NextTimestamp()
    {
        lastMicros = Math.Max(NowMicros(), lastMicros + 1);
        return lastMicros;
    }

    private static void AppendParam(StringBuilder sb, string key, object value, ref int count)
    {
        if (count >= MaxParams || string.IsNullOrEmpty(key) || value == null) return;
        string json = FormatValue(value);
        if (json == null) return;
        if (count > 0) sb.Append(',');
        sb.Append(Quote(Truncate(key, MaxNameLength))).Append(':').Append(json);
        count++;
    }

    private static string FormatValue(object value)
    {
        switch (value)
        {
            case string text: return Quote(Truncate(text, MaxStringLength));
            case bool flag: return flag ? "1" : "0";
            case int i: return i.ToString(CultureInfo.InvariantCulture);
            case long l: return l.ToString(CultureInfo.InvariantCulture);
            case float f: return float.IsNaN(f) || float.IsInfinity(f) ? null : f.ToString("R", CultureInfo.InvariantCulture);
            case double d: return double.IsNaN(d) || double.IsInfinity(d) ? null : d.ToString("R", CultureInfo.InvariantCulture);
            default: return Quote(Truncate(value.ToString(), MaxStringLength));
        }
    }

    private static string Truncate(string text, int max)
    {
        return text.Length <= max ? text : text.Substring(0, max);
    }

    private static string Quote(string text)
    {
        StringBuilder sb = new StringBuilder(text.Length + 2);
        sb.Append('"');
        foreach (char c in text)
        {
            switch (c)
            {
                case '"': sb.Append("\\\""); break;
                case '\\': sb.Append("\\\\"); break;
                case '\n': sb.Append("\\n"); break;
                case '\r': sb.Append("\\r"); break;
                case '\t': sb.Append("\\t"); break;
                default:
                    if (c < 0x20) sb.Append("\\u").Append(((int)c).ToString("x4"));
                    else sb.Append(c);
                    break;
            }
        }
        return sb.Append('"').ToString();
    }
}
