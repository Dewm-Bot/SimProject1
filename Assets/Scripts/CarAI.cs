using UnityEngine;
using System.Collections.Generic;

public class CarAI : MonoBehaviour
{
    [Header("Movement Settings")]
    public float speed = 2f;
    public float rotationSpeed = 5f;
    public float stopDistance = 0.2f;

    [Header("Collision Avoidance")]
    [Tooltip("Layer for the cars to detect.")]
    public LayerMask carLayer;
    [Tooltip("Distance ahead to check for other cars")]
    public float detectionDistance = 0.5f;

    [Header("Queue Settings")]
    [Tooltip("Gap between waiting cars")]
    public float waitingGap = 1.5f;

    [Header("Waypoint Settings")]
    [Tooltip("Initial waypoint for this car to follow")]
    public Waypoint initialWaypoint;
    private Waypoint currentWaypoint;

    [HideInInspector]
    public TrafficManager trafficManager;
    
    [HideInInspector]
    public bool isWaiting = false;
    private float waitTimer = 0f;
    
    [Tooltip("Remaining time (in seconds) before this car resumes movement.")]
    public float remainingWaitTime = 0f;
    
    private Transform cachedTransform;

    void Start()
    {
        cachedTransform = transform;
        if (!initialWaypoint)
        {
            Debug.LogError("No initial waypoint assigned to CarAI.");
            enabled = false;
        }
        else
        {
            currentWaypoint = initialWaypoint;
        }
    }

    void Update()
    {
        remainingWaitTime = waitTimer;
        
        if (!isWaiting)
        {
            Collider2D hit = Physics2D.OverlapCircle(cachedTransform.position + cachedTransform.up * detectionDistance, 0.1f, carLayer);
            if (hit && hit.gameObject != gameObject)
            {
                isWaiting = true;
                waitTimer = 0.5f;
            }
        }

        if (isWaiting)
        {
            waitTimer -= Time.deltaTime;
            if (waitTimer > 0f)
                return;
            else
                isWaiting = false;
        }

        if (!currentWaypoint)
            return;

        // Calculate direction to the current waypoint.
        Vector2 direction = currentWaypoint.transform.position - cachedTransform.position;
        float distanceSqr = direction.sqrMagnitude;
        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg - 90f;
        Quaternion targetRotation = Quaternion.Euler(0, 0, angle);
        cachedTransform.rotation = Quaternion.Lerp(cachedTransform.rotation, targetRotation, Time.deltaTime * rotationSpeed);

        // If within the stop distance...
        if (distanceSqr < stopDistance * stopDistance)
        {
            // Check if the waypoint is a service/stop point.
            if (currentWaypoint.isStopPoint)
            {
                if (!isWaiting)
                {
                    //Query the TrafficManager for wait time per car at this service point.
                    waitTimer = (trafficManager)
                        ? trafficManager.GetServiceWaitTime(currentWaypoint.serviceType)
                        : 0f;
                    isWaiting = true;
                    return;
                }
                else
                {
                    isWaiting = false;
                }
            }

            //Proceed to the next waypoint.
            if (currentWaypoint.nextWaypoints != null && currentWaypoint.nextWaypoints.Count > 0)
            {
                currentWaypoint = currentWaypoint.nextWaypoints[Random.Range(0, currentWaypoint.nextWaypoints.Count)];
            }
            else
            {
                Destroy(gameObject);
                return;
            }
        }

        //Check for cars ahead within the desired gap.
        Vector2 boxSize = new Vector2(0.5f, waitingGap);
        Vector2 boxCenter = (Vector2)cachedTransform.position + (Vector2)cachedTransform.up * (waitingGap * 0.5f);
        Collider2D blocker = Physics2D.OverlapBox(boxCenter, boxSize, cachedTransform.eulerAngles.z, carLayer);
        if (blocker && blocker.gameObject != gameObject)
        {
            CarAI frontCar = blocker.gameObject.GetComponent<CarAI>();
            if (frontCar && frontCar.isWaiting)
            {
                return;
            }
        }
        
        cachedTransform.position += cachedTransform.up * (speed * Time.deltaTime);
    }

    // Visualize detection areas in the Scene view.
    void OnDrawGizmosSelected()
    {
        if (cachedTransform == null)
            cachedTransform = transform;

        // Detection circle.
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(cachedTransform.position + cachedTransform.up * detectionDistance, 0.1f);

        // Draw OverlapBox for queue checking.
        Gizmos.color = Color.green;
        Vector2 boxSize = new Vector2(0.5f, waitingGap);
        Vector2 boxCenter = (Vector2)cachedTransform.position + (Vector2)cachedTransform.up * (waitingGap * 0.5f);
        Gizmos.DrawWireCube(boxCenter, boxSize);
    }
}