using UnityEngine;
using System.Collections.Generic;
public class CapturePoint : MonoBehaviour
{
    [Header("Capture Settings")]
    public float captureTime = 10f;

    public string controllingTeam  { get; private set; } = "None";
    public float  captureCountdown { get; private set; } = 10f;
    public bool   isContested      { get; private set; }
    public bool   isCapturing      { get; private set; }
    public string capturingTeam    { get; private set; } = "";
    public bool   justCaptured     { get; private set; }

    public System.Action<string>        OnCaptured;
    public System.Action                OnContested;
    public System.Action<string, float> OnCountdownTick;

    private readonly HashSet<GameObject> _occupants = new HashSet<GameObject>();

    private Renderer _rend;
    private bool     _lastContested;
    private bool     _alreadyCaptured;  

    void Start()
    {
        _rend            = GetComponent<Renderer>();
        captureCountdown = captureTime;
    }

    void Update()
    {
        justCaptured = false;

        _occupants.RemoveWhere(go => go == null || !go.activeInHierarchy);

        bool greenIn = false, redIn = false;
        foreach (var go in _occupants)
        {
            string t = GetTeamFrom(go);
            if (t == "Green") greenIn = true;
            else if (t == "Red") redIn = true;
        }

        isContested = greenIn && redIn;
        isCapturing = (greenIn || redIn) && !isContested;

        if (isContested)
        {
            captureCountdown = captureTime;
            capturingTeam    = "";
            _alreadyCaptured = false;

            if (!_lastContested) OnContested?.Invoke();
        }
        else if (isCapturing)
        {
            capturingTeam = greenIn ? "Green" : "Red";

            if (controllingTeam == capturingTeam)
            {
                captureCountdown = captureTime;
            }
            else
            {
                captureCountdown -= Time.deltaTime;
                captureCountdown  = Mathf.Max(captureCountdown, 0f);
                OnCountdownTick?.Invoke(capturingTeam, captureCountdown);

                if (captureCountdown <= 0f && !_alreadyCaptured)
                {
                    _alreadyCaptured = true;
                    CaptureBy(capturingTeam);
                }
            }
        }
        else
        {
            captureCountdown = captureTime;
            capturingTeam    = "";
            _alreadyCaptured = false;
        }

        _lastContested = isContested;
    }

    private void CaptureBy(string team)
    {
        controllingTeam  = team;
        captureCountdown = captureTime;
        justCaptured     = true;

        if (_rend != null)
            _rend.material.color = team == "Green"
                ? new Color(0.1f, 0.8f, 0.2f, 0.5f)
                : new Color(0.9f, 0.1f, 0.1f, 0.5f);

        Debug.Log($"[CapturePoint] {team} CAPTURED THE CORE!");
        OnCaptured?.Invoke(team);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (GetTeamFrom(other.gameObject) == null) return;
        _occupants.Add(other.gameObject);
        Debug.Log($"[CapturePoint] {other.name} entered. Occupants: {_occupants.Count}");
    }

    private void OnTriggerExit(Collider other)
    {
        bool removed = _occupants.Remove(other.gameObject);
        if (removed)
            Debug.Log($"[CapturePoint] {other.name} exited. Occupants: {_occupants.Count}");
    }

    public void ResetZone()
    {
        _occupants.Clear();
        captureCountdown = captureTime;
        capturingTeam    = "";
        controllingTeam  = "None";
        isContested      = false;
        isCapturing      = false;
        justCaptured     = false;
        _lastContested   = false;
        _alreadyCaptured = false;

        if (_rend != null) _rend.material.color = new Color(0.5f, 0.5f, 0.5f, 0.5f);
        Debug.Log("[CapturePoint] Zone reset for new round.");
    }

    private static string GetTeamFrom(GameObject go)
    {
        PlayerCharacter pc = go.GetComponent<PlayerCharacter>();
        if (pc != null) return pc.team;

        BaseCharacter bc = go.GetComponent<BaseCharacter>();
        if (bc != null) return bc.team;

        return null;
    }
    private static string GetTeamFrom(Collider c) => GetTeamFrom(c.gameObject);
}
