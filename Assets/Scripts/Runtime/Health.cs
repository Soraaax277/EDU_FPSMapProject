using UnityEngine;

public class Health : MonoBehaviour
{
    public float maxHealth    = 100f;
    public float currentHealth;
    public string team        = "None";
    public bool isDead        = false;

    void Start()
    {
        currentHealth = maxHealth;
        InitializeTeam();

        BaseCharacter bc = GetComponent<BaseCharacter>();
        if (bc == null)
        {
            bc      = gameObject.AddComponent<BaseCharacter>();
            bc.team = team;
        }
    }

    void InitializeTeam()
    {
        PlayerCharacter pc = GetComponent<PlayerCharacter>();
        if (pc != null) { team = pc.team; return; }

        CombatAI ai = GetComponent<CombatAI>();
        if (ai != null) { team = ai.team; return; }
    }

    public void TakeDamage(float amount, string attackerTeam)
    {
        if (isDead) return;
        if (attackerTeam.Trim().ToLower() == team.Trim().ToLower()) return;

        currentHealth -= amount;
        if (currentHealth <= 0f) Die();
    }

    void Die()
    {
        if (isDead) return;
        isDead = true;

        Debug.Log($"[Health] {name} ({team}) DIED.");

        Renderer r = GetComponent<Renderer>();
        if (r != null) r.material.color = new Color(0.08f, 0.08f, 0.08f);

        var pc = GetComponent<PlayerCharacter>(); if (pc != null) pc.enabled = false;
        var ai = GetComponent<CombatAI>();        if (ai != null) ai.enabled = false;

        if (CompareTag("Player"))
        {
            if (SpectatorController.Instance != null)
                SpectatorController.Instance.StartSpectating();
        }
        else
        {
            if (SpectatorController.Instance != null)
                SpectatorController.Instance.OnTeammateDied();
        }

        gameObject.SetActive(false);
    }

    public void ResetPlayer()
    {
        isDead        = false;
        currentHealth = maxHealth;

        gameObject.SetActive(true);

        Renderer r = GetComponent<Renderer>();
        if (r != null)
            r.material.color = team.Trim().ToLower().Contains("green") ? Color.green : Color.red;

        var pc = GetComponent<PlayerCharacter>(); if (pc != null) pc.enabled = true;
        var ai = GetComponent<CombatAI>();        if (ai != null) ai.enabled = true;

        if (CompareTag("Player"))
        {
            if (SpectatorController.Instance != null)
                SpectatorController.Instance.StopSpectating();
        }
    }
}
