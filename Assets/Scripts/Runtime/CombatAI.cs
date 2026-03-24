using UnityEngine;
using UnityEngine.AI;

public class CombatAI : MonoBehaviour
{
    [Header("Team & Stats")]
    public string team = "Red"; 
    public float detectionRange = 40f;
    public float fireRate = 0.5f;
    public float startDelay = 5.5f;

    [Header("Movement")]
    public float normalSpeed = 4.5f;
    public float sprintSpeed = 8.5f;
    public float patrolRadius = 30f;

    public GameObject bulletPrefab;
    public Transform shootPoint;

    private float _startTime;
    private float nextFireTime;
    private Transform target;
    private Vector3 enemyHomeBase;
    private NavMeshAgent agent;
    private enum State { StartDelay, PushingCenter, Infiltrating, Attacking }
    private State currentState = State.StartDelay;
    private Vector3 currentDestination = Vector3.one * 10000f;

    void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
    }

    void OnEnable()
    {
        ResetForNewRound();
    }

    public void ResetForNewRound()
    {
        _startTime = Time.time;
        currentState = State.StartDelay;
        currentDestination = Vector3.one * 10000f;
        target = null;
        
        if (agent != null)
        {
            agent.speed = normalSpeed;
            if (agent.isActiveAndEnabled && agent.isOnNavMesh)
            {
                agent.ResetPath();
                agent.isStopped = false;
            }
        }

        string lowerTeam = team.Trim().ToLower();
        bool isGreen = lowerTeam.Contains("green");
        enemyHomeBase = new Vector3(isGreen ? 230f : -230f, 0, 0);
        
        Debug.Log($"[AI] {name} (Team:{team}) state reset to StartDelay.");
    }

    void Start()
    {
        if (shootPoint == null)
        {
            GameObject sp = new GameObject("AI_ShootPoint");
            sp.transform.SetParent(transform);
            sp.transform.localPosition = new Vector3(0, 0.5f, 1.2f);
            shootPoint = sp.transform;
        }
    }

    void Update()
    {
        switch (currentState)
        {
            case State.StartDelay:
                if (Time.time >= _startTime + startDelay)
                {
                    currentState = State.PushingCenter;
                    UpdateMoveTarget(Vector3.zero);
                    Debug.Log($"[AI] {name} starting push to center.");
                }
                break;

            case State.PushingCenter:
                FindTarget();
                if (target != null) { currentState = State.Attacking; break; }
                
                if (Vector3.Distance(transform.position, Vector3.zero) < 10f)
                {
                    currentState = State.Infiltrating;
                    UpdateMoveTarget(enemyHomeBase);
                }
                else
                {
                    UpdateMoveTarget(Vector3.zero);
                }
                break;

            case State.Infiltrating:
                FindTarget();
                if (target != null) { currentState = State.Attacking; break; }

                if (!agent.pathPending && agent.remainingDistance <= agent.stoppingDistance + 1.2f)
                {
                    SetNewPatrolTargetInsideEnemyBase();
                }
                else
                {
                    UpdateMoveTarget(enemyHomeBase);
                }
                break;

            case State.Attacking:
                FindTarget();
                if (target == null) 
                { 
                    currentState = State.PushingCenter; 
                    if(agent != null && agent.isOnNavMesh) agent.isStopped = false;
                    break; 
                }
                AttackTarget();
                break;
        }

        HandleSprint();
    }

    void FindTarget()
    {
        float closestDist = detectionRange;
        target = null;
        SearchForTargets("Player", ref closestDist);
        SearchForTargets("AI", ref closestDist);
    }

    void SearchForTargets(string tagToSearch, ref float closestDist)
    {
        GameObject[] targets = GameObject.FindGameObjectsWithTag(tagToSearch);
        string myTeamLower = team.Trim().ToLower();

        foreach (GameObject t in targets)
        {
            if (t == gameObject) continue;
            Health h = t.GetComponent<Health>();
            if (h == null || h.isDead) continue;

            string otherTeamLower = h.team.Trim().ToLower();

            if (otherTeamLower != myTeamLower && otherTeamLower != "none")
            {
                float dist = Vector3.Distance(transform.position, t.transform.position);
                if (dist < closestDist) { closestDist = dist; target = t.transform; }
            }
        }
    }

    void AttackTarget()
    {
        if (agent != null && agent.isOnNavMesh && !agent.isStopped) agent.isStopped = true;

        Vector3 targetDir = (target.position - transform.position).normalized;
        targetDir.y = 0;
        if (targetDir != Vector3.zero)
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(targetDir), Time.deltaTime * 10f);

        if (Time.time >= nextFireTime)
        {
            if (CanSeeTarget())
            {
                Shoot();
                nextFireTime = Time.time + fireRate;
            }
        }
    }

    void UpdateMoveTarget(Vector3 targetPos)
    {
        if (agent == null || !agent.isActiveAndEnabled || !agent.isOnNavMesh) return;
        if (agent.isStopped) agent.isStopped = false;

        if (Vector3.Distance(currentDestination, targetPos) < 1.5f) return;

        currentDestination = targetPos;
        agent.SetDestination(targetPos);
    }

    void SetNewPatrolTargetInsideEnemyBase()
    {
        Vector3 randomPoint = enemyHomeBase + Random.insideUnitSphere * patrolRadius;
        randomPoint.y = 0;

        NavMeshHit hit;
        if (NavMesh.SamplePosition(randomPoint, out hit, patrolRadius, NavMesh.AllAreas))
        {
            UpdateMoveTarget(hit.position);
        }
    }

    void HandleSprint()
    {
        if (agent == null || !agent.isActiveAndEnabled || !agent.isOnNavMesh || currentState == State.Attacking) 
        { 
            if (agent != null && agent.isActiveAndEnabled) agent.speed = normalSpeed;
            return; 
        }

        if (agent.remainingDistance > 10f)
        {
            agent.speed = sprintSpeed;
        }
        else
        {
            agent.speed = normalSpeed;
        }
    }

    bool CanSeeTarget()
    {
        RaycastHit hit;
        Vector3 origin = transform.position + Vector3.up;
        Vector3 dir = (target.position - origin).normalized;
        if (Physics.Raycast(origin, dir, out hit, detectionRange))
        {
            if (hit.transform.root == target.root) return true;
        }
        return false;
    }

    void Shoot()
    {
        if (bulletPrefab != null && shootPoint != null)
        {
            GameObject bullet = Instantiate(bulletPrefab, shootPoint.position, shootPoint.rotation);
            bullet.SetActive(true);
            Bullet b = bullet.GetComponent<Bullet>();
            if (b != null) b.team = team;
        }
    }
}
