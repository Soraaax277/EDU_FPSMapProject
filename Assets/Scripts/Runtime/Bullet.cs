using UnityEngine;

public class Bullet : MonoBehaviour
{
    public float speed = 100f;
    public float damage = 25f; 
    public float lifetime = 10f;
    public string team = "None";

    private bool isOriginal = false;

    void Awake()
    {
        if (gameObject.name == "Bullet_Prefab" || (transform.parent != null && transform.parent.name == "Map_TheConduit"))
        {
            isOriginal = true;
        }

        if (!isOriginal && GetComponent<Rigidbody>() == null)
        {
            Rigidbody rb = gameObject.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;
        }
    }

    void Start()
    {
        if (isOriginal) { gameObject.SetActive(false); return; }
        Destroy(gameObject, lifetime);
    }

    void Update()
    {
        if (isOriginal) return;
        transform.position += transform.forward * speed * Time.deltaTime;
        
        RaycastHit hit;
        if (Physics.Raycast(transform.position, transform.forward, out hit, speed * Time.deltaTime * 1.1f))
        {
            HandleHit(hit.collider);
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (isOriginal) return;
        HandleHit(other);
    }

    private void HandleHit(Collider other)
    {
        Health h = other.GetComponent<Health>();
        if (h == null) h = other.GetComponentInParent<Health>();

        if (h != null)
        {
            if (h.team != team)
            {
                h.TakeDamage(damage, team);
                Debug.Log(team + " projectile dealt damage to " + other.name);
                Destroy(gameObject);
            }
        }
        else if (!other.isTrigger) 
        {
            Destroy(gameObject);
        }
    }
}
