using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Self-building HUD. Attach to any GameObject — builds its own Canvas.
///
/// States:
///   IDLE       — nobody in zone       → "CAPTURE POINT: NEUTRAL"
///   COUNTDOWN  — one team in zone     → "GREEN IS CAPTURING... 10 9 8..."
///   CONTESTED  — both teams in zone   → "⚡ BLOODBATH !!!"
///   CAPTURED   — timer hit 0          → "[Team] CAPTURED!" fade banner  +  WIN SCREEN
///   OWNER      — zone owned           → "GREEN controls the core"
///   WIN        — full-screen overlay  → "RED TEAM WINS!"
/// </summary>
public class CapturePointUI : MonoBehaviour
{
    public static CapturePointUI Instance;

    [Header("Set by generator or auto-found")]
    public CapturePoint capturePoint;

    // ── Timing ────────────────────────────────────────────────────
    private const float BANNER_TIME = 3f;
    private const float PULSE_SPD   = 3f;

    // ── UI objects ────────────────────────────────────────────────
    private GameObject panelIdle, panelCountdown, panelBloodbath,
                       panelCaptured, panelOwner, panelWin;

    private Text txtIdle, txtCountLabel, txtCountNum,
                 txtBlood, txtCaptured, txtOwner,
                 txtWinMain, txtWinSub;

    // ── State ─────────────────────────────────────────────────────
    private float bannerTimer   = 0f;
    private bool  bannerActive  = false;
    private int   lastSec       = -1;
    private float pulseT        = 0f;
    private float bloodT        = 0f;
    private bool  winActive     = false;

    // ── Palette ───────────────────────────────────────────────────
    static readonly Color CG  = new Color(0.15f, 0.95f, 0.35f);
    static readonly Color CR  = new Color(1.00f, 0.20f, 0.20f);
    static readonly Color CY  = new Color(1.00f, 0.85f, 0.10f);
    static readonly Color CN  = new Color(0.70f, 0.75f, 0.85f);
    static readonly Color CBg = new Color(0.04f, 0.04f, 0.08f, 0.88f);

    // ─────────────────────────────────────────────────────────────
    void Awake()
    {
        Instance = this;
        BuildUI();
    }

    void Start()
    {
        // Find CapturePoint here in Start() so it's definitely spawned by now
        if (capturePoint == null)
            capturePoint = FindObjectOfType<CapturePoint>();

        if (capturePoint != null)
        {
            capturePoint.OnCaptured  += OnZoneCaptured;
            capturePoint.OnContested += OnZoneContested;
        }
        else
        {
            Debug.LogWarning("[CapturePointUI] No CapturePoint found! UI won't update.");
        }
    }

    void OnDestroy()
    {
        if (capturePoint != null)
        {
            capturePoint.OnCaptured  -= OnZoneCaptured;
            capturePoint.OnContested -= OnZoneContested;
        }
    }

    // ─────────────────────────────────────────────────────────────
    //  UPDATE
    // ─────────────────────────────────────────────────────────────
    void Update()
    {
        // Banner fade
        if (bannerActive)
        {
            bannerTimer -= Time.deltaTime;
            float a = Mathf.Clamp01(bannerTimer / BANNER_TIME);
            SetAlpha(panelCaptured, a);
            if (bannerTimer <= 0f) { bannerActive = false; panelCaptured.SetActive(false); }
        }

        // Always show win overlay on top
        if (winActive) { ShowOnly(panelWin); return; }

        if (capturePoint == null) { ShowOnly(panelIdle); return; }

        if (capturePoint.isContested)
        {
            ShowOnly(panelBloodbath, keepCapture: bannerActive);
            AnimateBlood();
        }
        else if (capturePoint.isCapturing)
        {
            ShowOnly(panelCountdown, keepCapture: bannerActive);
            DrawCountdown();
        }
        else if (capturePoint.controllingTeam != "None")
        {
            ShowOnly(panelOwner, keepCapture: bannerActive);
            string t = capturePoint.controllingTeam;
            txtOwner.text  = $"⬤  {t.ToUpper()} CONTROLS THE CORE";
            txtOwner.color = t == "Green" ? CG : CR;
        }
        else
        {
            ShowOnly(panelIdle, keepCapture: bannerActive);
        }
    }

    // ─────────────────────────────────────────────────────────────
    //  EVENTS
    // ─────────────────────────────────────────────────────────────
    void OnZoneCaptured(string team)
    {
        // Flash the captured banner
        txtCaptured.text  = $"{team.ToUpper()} CAPTURED THE CORE!";
        txtCaptured.color = team == "Green" ? CG : CR;
        panelCaptured.SetActive(true);
        SetAlpha(panelCaptured, 1f);
        bannerTimer  = BANNER_TIME;
        bannerActive = true;

        // Trigger win screen
        if (RoundManager.Instance != null)
            RoundManager.Instance.WinRound(team);
        else
            ShowWinScreen(team);  // fallback
    }

    void OnZoneContested() { /* Update() handles it */ }

    // ─────────────────────────────────────────────────────────────
    //  PUBLIC — called by RoundManager
    // ─────────────────────────────────────────────────────────────
    public void ShowWinScreen(string team)
    {
        winActive = true;
        txtWinMain.text  = $"{team.ToUpper()} TEAM WINS!";
        txtWinMain.color = team.Contains("Green") ? CG : CR;
        txtWinSub.text   = "Next round starting soon...";
        panelWin.SetActive(true);
    }

    public void ResetWinScreen()
    {
        winActive = false;
        panelWin.SetActive(false);
        lastSec = -1;
    }

    // ─────────────────────────────────────────────────────────────
    //  ANIMATIONS
    // ─────────────────────────────────────────────────────────────
    void DrawCountdown()
    {
        float secs  = capturePoint.captureCountdown;
        int   whole = Mathf.CeilToInt(secs);
        string team = capturePoint.capturingTeam;
        Color  col  = team == "Green" ? CG : CR;

        txtCountLabel.text  = $"{team.ToUpper()} IS CAPTURING...";
        txtCountLabel.color = col;

        if (whole != lastSec)
        {
            lastSec            = whole;
            txtCountNum.text   = Mathf.Max(whole, 0).ToString();
            pulseT             = 0f;
        }

        pulseT += Time.deltaTime * PULSE_SPD;
        float brightness = 0.65f + 0.35f * Mathf.Abs(Mathf.Sin(pulseT * Mathf.PI));
        txtCountNum.color = new Color(col.r * brightness, col.g * brightness, col.b * brightness);

        // Urgency: shift to red in last 3 s
        if (secs <= 3f)
            txtCountNum.color = Color.Lerp(CR, col, secs / 3f);
    }

    void AnimateBlood()
    {
        bloodT += Time.deltaTime * 4f;
        float t = Mathf.Abs(Mathf.Sin(bloodT));
        txtBlood.color    = Color.Lerp(CR, CY, t);
        txtBlood.fontSize = 52 + Mathf.RoundToInt(Mathf.Sin(bloodT * 1.5f) * 5f);
    }

    // ─────────────────────────────────────────────────────────────
    //  HELPERS
    // ─────────────────────────────────────────────────────────────
    void ShowOnly(GameObject active, bool keepCapture = false)
    {
        panelIdle.SetActive(panelIdle         == active);
        panelCountdown.SetActive(panelCountdown == active);
        panelBloodbath.SetActive(panelBloodbath == active);
        panelOwner.SetActive(panelOwner       == active);
        panelWin.SetActive(panelWin           == active);
        if (!keepCapture) panelCaptured.SetActive(false);
    }

    static void SetAlpha(GameObject go, float a)
    {
        CanvasGroup cg = go.GetComponent<CanvasGroup>();
        if (cg != null) cg.alpha = a;
    }

    // ─────────────────────────────────────────────────────────────
    //  UI BUILDER
    // ─────────────────────────────────────────────────────────────
    void BuildUI()
    {
        // Root canvas — ScreenSpaceOverlay so it always renders
        GameObject cvGO = new GameObject("CaptureHUD_Canvas");
        DontDestroyOnLoad(cvGO);
        Canvas cv = cvGO.AddComponent<Canvas>();
        cv.renderMode   = RenderMode.ScreenSpaceOverlay;
        cv.sortingOrder = 200;   // above everything
        CanvasScaler cs = cvGO.AddComponent<CanvasScaler>();
        cs.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        cs.referenceResolution = new Vector2(1920, 1080);
        cvGO.AddComponent<GraphicRaycaster>();

        // Top-centre HUD strip  (25 %–75 % wide, top 15 % of screen)
        RectTransform strip = Rect("HUD_Strip", cvGO.transform);
        strip.anchorMin = new Vector2(0.25f, 0.86f);
        strip.anchorMax = new Vector2(0.75f, 1.00f);
        strip.offsetMin = strip.offsetMax = Vector2.zero;

        // ── IDLE ─────────────────────────────────────────────────
        panelIdle = Panel("P_Idle", strip, new Color(0.05f, 0.05f, 0.10f, 0.75f));
        txtIdle   = Txt(panelIdle.transform, "⬤  CAPTURE POINT: NEUTRAL",
                        16, CN, TextAnchor.MiddleCenter, fill: true);

        // ── COUNTDOWN ────────────────────────────────────────────
        panelCountdown = Panel("P_Countdown", strip, CBg); panelCountdown.SetActive(false);

        txtCountLabel = Txt(panelCountdown.transform, "? IS CAPTURING...",
                            18, CG, TextAnchor.UpperCenter);
        R(txtCountLabel).anchorMin = new Vector2(0, 0.52f);
        R(txtCountLabel).anchorMax = new Vector2(1, 1.00f);

        txtCountNum = Txt(panelCountdown.transform, "10", 80, CG, TextAnchor.LowerCenter);
        txtCountNum.fontStyle = FontStyle.Bold;
        R(txtCountNum).anchorMin = new Vector2(0.30f, 0f);
        R(txtCountNum).anchorMax = new Vector2(0.70f, 0.55f);

        // ── BLOODBATH ────────────────────────────────────────────
        panelBloodbath = Panel("P_Bloodbath", strip, new Color(0.10f, 0.01f, 0.01f, 0.95f));
        panelBloodbath.SetActive(false);
        txtBlood = Txt(panelBloodbath.transform,
                       "⚡  BLOODBATH !!!  ⚡\nBOTH TEAMS IN THE ZONE",
                       52, CR, TextAnchor.MiddleCenter, fill: true);
        txtBlood.fontStyle = FontStyle.Bold;

        // ── CAPTURED BANNER (floats above strip, fades) ──────────
        panelCaptured = Panel("P_Captured", strip, new Color(0.02f, 0.02f, 0.04f, 0.95f));
        panelCaptured.SetActive(false);
        panelCaptured.AddComponent<CanvasGroup>();
        RectTransform cr = R(panelCaptured);
        cr.anchorMin = new Vector2(0f, 1.08f);
        cr.anchorMax = new Vector2(1f, 1.60f);
        cr.offsetMin = cr.offsetMax = Vector2.zero;
        txtCaptured = Txt(panelCaptured.transform,
                          "?? CAPTURED THE CORE!", 30, CG, TextAnchor.MiddleCenter, fill: true);
        txtCaptured.fontStyle = FontStyle.Bold;

        // ── OWNER TAG ────────────────────────────────────────────
        panelOwner = Panel("P_Owner", strip, new Color(0.03f, 0.03f, 0.06f, 0.75f));
        panelOwner.SetActive(false);
        txtOwner = Txt(panelOwner.transform, "⬤  ? CONTROLS THE CORE",
                       16, CG, TextAnchor.MiddleCenter, fill: true);

        // ── WIN OVERLAY (full-screen) ─────────────────────────────
        panelWin = Panel("P_Win", cvGO.transform, new Color(0f, 0f, 0f, 0.94f));
        panelWin.SetActive(false);
        RectTransform wr = R(panelWin);
        wr.anchorMin = Vector2.zero; wr.anchorMax = Vector2.one;
        wr.offsetMin = wr.offsetMax = Vector2.zero;

        txtWinMain = Txt(panelWin.transform, "? TEAM WINS!", 90, CG, TextAnchor.MiddleCenter);
        txtWinMain.fontStyle = FontStyle.Bold;
        RectTransform wm = R(txtWinMain);
        wm.anchorMin = new Vector2(0.1f, 0.40f);
        wm.anchorMax = new Vector2(0.9f, 0.70f);
        wm.offsetMin = wm.offsetMax = Vector2.zero;

        txtWinSub = Txt(panelWin.transform, "Next round starting soon...",
                        28, CN, TextAnchor.MiddleCenter);
        RectTransform ws = R(txtWinSub);
        ws.anchorMin = new Vector2(0.2f, 0.30f);
        ws.anchorMax = new Vector2(0.8f, 0.42f);
        ws.offsetMin = ws.offsetMax = Vector2.zero;
    }

    // ── Factory helpers ───────────────────────────────────────────
    static RectTransform R(Component c)       => c.GetComponent<RectTransform>();
    static RectTransform R(GameObject go)     => go.GetComponent<RectTransform>();

    static RectTransform Rect(string n, Transform parent)
    {
        var go = new GameObject(n); go.transform.SetParent(parent, false);
        return go.AddComponent<RectTransform>();
    }

    static GameObject Panel(string n, Transform parent, Color col)
    {
        var go = new GameObject(n); go.transform.SetParent(parent, false);
        Image img = go.AddComponent<Image>(); img.color = col;
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        return go;
    }

    static Text Txt(Transform parent, string content, int size,
                    Color col, TextAnchor align, bool fill = false)
    {
        var go = new GameObject("T"); go.transform.SetParent(parent, false);
        Text t = go.AddComponent<Text>();
        t.text             = content;
        t.fontSize         = size;
        t.color            = col;
        t.alignment        = align;
        t.font             = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        t.horizontalOverflow = HorizontalWrapMode.Overflow;
        t.verticalOverflow   = VerticalWrapMode.Overflow;
        Shadow sh = go.AddComponent<Shadow>();
        sh.effectColor    = new Color(0, 0, 0, 0.9f);
        sh.effectDistance = new Vector2(2, -2);
        if (fill)
        {
            RectTransform rt = go.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(8, 4); rt.offsetMax = new Vector2(-8, -4);
        }
        return t;
    }
}
