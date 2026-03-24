using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections.Generic;

/// <summary>
/// Manages the spectator camera system when the player is dead.
///
/// Self-discovers all spectator cameras at Start() so there is no
/// dependency on serialized inspector references that get wiped during
/// Unity's domain reload when entering Play mode.
///
/// Press C while dead → cycle through alive teammate cameras.
/// All teammates dead  → show the high Overview_Camera.
/// Round resets        → return to first-person view.
/// </summary>
public class SpectatorController : MonoBehaviour
{
    public static SpectatorController Instance;

    private readonly List<(Camera cam, Health health, string label)> _entries
        = new List<(Camera, Health, string)>();

    private Camera _overviewCam;
    private Camera _mainCam;

    private bool _spectating  = false;
    private int  _cycleIndex  = -1;

    // ── Lifecycle ─────────────────────────────────────────────────
    void Awake() { Instance = this; }

    void Start()
    {
        // ── Self-discover cameras by name (survives domain reload) ──

        // Main FPS camera
        _mainCam = Camera.main;

        // Overview fallback camera
        GameObject overviewGO = GameObject.Find("Overview_Camera");
        if (overviewGO != null)
        {
            _overviewCam = overviewGO.GetComponent<Camera>();
            if (_overviewCam != null) _overviewCam.enabled = false;
        }

        // Teammate spectator cameras — named "SpectatorCam_Teammate_X"
        _entries.Clear();
        Camera[] allCams = FindObjectsOfType<Camera>(true);
        foreach (Camera cam in allCams)
        {
            if (!cam.gameObject.name.StartsWith("SpectatorCam_")) continue;

            SimpleCameraFollow follow = cam.GetComponent<SimpleCameraFollow>();
            if (follow == null || follow.target == null)
            {
                Debug.LogWarning($"[Spectator] {cam.name} missing SimpleCameraFollow or target — skipped.");
                continue;
            }

            Health h = follow.target.GetComponent<Health>();
            if (h == null)
            {
                Debug.LogWarning($"[Spectator] {cam.name} target has no Health — skipped.");
                continue;
            }

            cam.enabled = false;   // start disabled
            _entries.Add((cam, h, cam.gameObject.name));
            Debug.Log($"[Spectator] Discovered: {cam.gameObject.name} → follows {follow.target.name}");
        }

        Debug.Log($"[Spectator] Ready. {_entries.Count} teammate cams registered.");
    }

    // ── Input ─────────────────────────────────────────────────────
    void Update()
    {
        if (!_spectating) return;
        if (Keyboard.current != null && Keyboard.current.cKey.wasPressedThisFrame)
            CycleNext();
    }

    // ── Public API ────────────────────────────────────────────────

    /// Called by Health.cs when the player dies.
    public void StartSpectating()
    {
        _spectating = true;
        _cycleIndex = -1;

        if (_mainCam != null) _mainCam.enabled = false;

        CycleNext();
    }

    /// Called by Health.cs on round reset.
    public void StopSpectating()
    {
        _spectating = false;
        DisableAll();

        if (_mainCam != null) _mainCam.enabled = true;
    }

    /// Called when a teammate dies — auto-skips their camera if currently shown.
    public void OnTeammateDied()
    {
        if (!_spectating) return;

        // If the camera currently shown belongs to the just-died teammate, move on
        foreach (var e in _entries)
        {
            if (e.cam != null && e.cam.enabled && e.health != null && e.health.isDead)
            {
                Debug.Log($"[Spectator] Watched teammate died — auto-cycling.");
                CycleNext();
                return;
            }
        }
    }

    // ── Internal ──────────────────────────────────────────────────
    void CycleNext()
    {
        DisableAll();

        var alive = new List<(Camera cam, Health health, string label)>();
        foreach (var e in _entries)
            if (e.cam != null && e.health != null && !e.health.isDead)
                alive.Add(e);

        if (alive.Count == 0)
        {
            // All teammates dead — show high overview camera
            if (_overviewCam != null)
            {
                _overviewCam.enabled = true;
                Debug.Log("[Spectator] All teammates dead — showing Overview.");
            }
            return;
        }

        _cycleIndex = (_cycleIndex + 1) % alive.Count;
        alive[_cycleIndex].cam.enabled = true;
        Debug.Log($"[Spectator] Watching: {alive[_cycleIndex].label} ({_cycleIndex + 1}/{alive.Count} alive)");
    }

    void DisableAll()
    {
        foreach (var e in _entries)
            if (e.cam != null) e.cam.enabled = false;
        if (_overviewCam != null) _overviewCam.enabled = false;
    }
}
