using UnityEngine;
using UnityEngine.AI;
using System.Collections.Generic;

public class RoundManager : MonoBehaviour
{
    public static RoundManager Instance;

    private readonly List<BaseCharacter> allCharacters = new List<BaseCharacter>();
    private bool  roundEnding   = false;
    private float lastScanTime  = 0f;
    private float roundStartTime = 0f;         

    void Awake()  { Instance = this; }

    void Start()  { roundStartTime = Time.time; }

    public void RegisterCharacter(BaseCharacter bc)
    {
        if (!allCharacters.Contains(bc))
        {
            allCharacters.Add(bc);
            Debug.Log($"[RoundManager] Registered: {bc.name}  team={bc.team}");
        }
    }

    void Update()
    {
        if (Time.time > lastScanTime + 2f)
        {
            SafetyScan();
            lastScanTime = Time.time;
        }

        if (roundEnding) return;
        if (Time.time < roundStartTime + 3f) return;  

        CheckRoundStatus();
    }

    void SafetyScan()
    {
        foreach (var bc in FindObjectsOfType<BaseCharacter>(true))
            RegisterCharacter(bc);
    }

    void CheckRoundStatus()
    {
        if (allCharacters.Count == 0) return;

        int greenAlive = 0, redAlive = 0;
        int greenFound = 0, redFound = 0;

        foreach (var bc in allCharacters)
        {
            if (bc == null) continue;
            Health h = bc.health;
            if (h == null) continue;

            string t = bc.team.Trim().ToLower();
            if (t.Contains("green"))
            {
                greenFound++;
                if (!h.isDead) greenAlive++;
            }
            else if (t.Contains("red"))
            {
                redFound++;
                if (!h.isDead) redAlive++;
            }
        }

        bool greenWiped = (greenFound > 0 && greenAlive == 0);
        bool redWiped   = (redFound   > 0 && redAlive   == 0);

        if      (greenWiped && !redWiped) WinRound("Red");
        else if (redWiped   && !greenWiped) WinRound("Green");
    }

    public void WinRound(string winningTeam)
    {
        if (roundEnding) return;
        roundEnding = true;

        Debug.Log($"[RoundManager] *** {winningTeam.ToUpper()} TEAM WINS ***");

        if (CapturePointUI.Instance != null)
            CapturePointUI.Instance.ShowWinScreen(winningTeam);

        Invoke(nameof(ResetRound), 5f);
    }

    public void ResetRound()
    {
        if (CapturePointUI.Instance != null)
            CapturePointUI.Instance.ResetWinScreen();

        CapturePoint cp = FindObjectOfType<CapturePoint>();
        if (cp != null) cp.ResetZone();

        SafetyScan();
        foreach (var bc in allCharacters)
            if (bc != null) bc.ResetToSpawn();

        roundEnding    = false;
        roundStartTime = Time.time;
        Debug.Log("[RoundManager] ─── NEW ROUND START ───");
    }

    public Transform GetLivingTeammate(string teamToFind)
    {
        string lower = teamToFind.Trim().ToLower();
        foreach (var bc in allCharacters)
            if (bc != null && bc.team.Trim().ToLower().Contains(lower)
                           && bc.health != null && !bc.health.isDead)
                return bc.transform;
        return null;
    }
}
public class BaseCharacter : MonoBehaviour
{
    public string team;
    public Vector3    spawnPos;
    public Quaternion spawnRot;
    public Health     health;

    void Start()
    {
        spawnPos = transform.position;
        spawnRot = transform.rotation;
        health   = GetComponent<Health>();
        if (RoundManager.Instance != null)
            RoundManager.Instance.RegisterCharacter(this);
    }

    public void ResetToSpawn()
    {
        if (health != null) health.ResetPlayer();

        CharacterController cc    = GetComponent<CharacterController>();
        NavMeshAgent         agent = GetComponent<NavMeshAgent>();

        if (cc    != null) cc.enabled = false;
        if (agent != null) { agent.isStopped = true; agent.enabled = false; }

        transform.position = spawnPos;
        transform.rotation = spawnRot;

        if (cc    != null) cc.enabled = true;
        if (agent != null)
        {
            agent.enabled = true;
            if (agent.isActiveAndEnabled && agent.isOnNavMesh)
            { agent.Warp(spawnPos); agent.isStopped = false; }
        }

        var pc = GetComponent<PlayerCharacter>(); if (pc != null) { pc.enabled = false; pc.enabled = true; }
        var ai = GetComponent<CombatAI>();        if (ai != null) { ai.enabled = false; ai.enabled = true; ai.ResetForNewRound(); }
    }
}
