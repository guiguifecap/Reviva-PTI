using UnityEngine;

public class Bird: MonoBehaviour
{
    public float speed = 3f;
    public float turnSpeed = 5f;
    public float wanderRadius = 15f;
    public float waitTime = 0.5f;

    private Vector3 startPos;
    private Vector3 target;
    private float waitTimer;

    void Start()
    {
        startPos = transform.position;
        PickNewTarget();
    }

    void Update()
    {
        if (waitTimer > 0f)
        {
            waitTimer -= Time.deltaTime;
            return;
        }

        // Face the target (flat, no up/down)
        Vector3 dir = target - transform.position;
        dir.y = 0f;

        if (dir.magnitude < 0.5f)
        {
            waitTimer = waitTime;
            PickNewTarget();
            return;
        }

        Quaternion look = Quaternion.LookRotation(dir);
        transform.rotation = Quaternion.Slerp(transform.rotation, look, turnSpeed * Time.deltaTime);
        transform.position += transform.forward * speed * Time.deltaTime;
    }

    void PickNewTarget()
    {
        Vector2 rand = Random.insideUnitCircle * wanderRadius;
        target = new Vector3(startPos.x + rand.x, startPos.y, startPos.z + rand.y);
    }
}