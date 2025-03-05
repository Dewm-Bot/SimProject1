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
    [Tooltip("Detection radius for collision avoidance.")]
    public float detectionDistance = 0.5f;
    [Tooltip("Angle for detecting cars ahead.")]
    public float coneAngle = 75f;

    [Header("Queue Settings")]
    [Tooltip("Gap between waiting cars")]
    public float waitingGap = 1.5f;

    [Header("Waypoint Settings")]
    [Tooltip("Initial waypoint for this car to follow")]
    public Waypoint initialWaypoint;
    private Waypoint currentWaypoint;

    [HideInInspector]
    public TrafficManager trafficManager;

    private bool waitingForService = false;
    private float serviceWaitTimer = 0f;
    private bool hasWaited = false;

    private bool waitingForTraffic = false;
    private float trafficWaitTimer = 0f;

    [Tooltip("Remaining time for task")]
    public float remainingWaitTime = 0f;

    private Transform cachedTransform;
    
    private bool isTouchingCar = false;
    private float collisionRecoveryTime = 0.5f;

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
        // Stop immediately if touching another car
        if (isTouchingCar)
        {
            isTouchingCar = false;
            waitingForTraffic = true;
            trafficWaitTimer = collisionRecoveryTime;
            remainingWaitTime = trafficWaitTimer;
            return;
        }
        
        if (waitingForTraffic)
        {
            trafficWaitTimer -= Time.deltaTime;
            remainingWaitTime = trafficWaitTimer;
            if (trafficWaitTimer > 0f)
                return;
                
            waitingForTraffic = false;
            if (IsCarAhead())
                return;
        }
        else
        {
            //DetectionCone
            if (IsCarAhead())
            {
                waitingForTraffic = true;
                trafficWaitTimer = 0.5f;
                remainingWaitTime = trafficWaitTimer;
                return;
            }
        }

        // Process service waiting.
        if (waitingForService)
        {
            serviceWaitTimer -= Time.deltaTime;
            remainingWaitTime = serviceWaitTimer;
            if (serviceWaitTimer > 0f)
                return;
                
            waitingForService = false;
            hasWaited = true;
            
            //Check if there's a car ahead before proceeding
            if (IsCarAhead() || IsCarInQueueBox())
                return;
        }

        if (!currentWaypoint)
            return;
        
        Vector2 direction = currentWaypoint.transform.position - cachedTransform.position;
        float distanceSqr = direction.sqrMagnitude;
        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg - 90f;
        Quaternion targetRotation = Quaternion.Euler(0, 0, angle);
        cachedTransform.rotation = Quaternion.Lerp(cachedTransform.rotation, targetRotation, Time.deltaTime * rotationSpeed);
        
        if (distanceSqr < stopDistance * stopDistance)
        {
            //For service stops, only wait once.
            if (currentWaypoint.isStopPoint && !hasWaited)
            {
                if (!waitingForService)
                {
                    serviceWaitTimer = (trafficManager != null)
                        ? trafficManager.GetServiceWaitTime(currentWaypoint.serviceType)
                        : 0f;
                    waitingForService = true;
                    remainingWaitTime = serviceWaitTimer;
                    return;
                }
            }

            //Proceed to the next waypoint.
            if (currentWaypoint.nextWaypoints != null && currentWaypoint.nextWaypoints.Count > 0)
            {
                currentWaypoint = currentWaypoint.nextWaypoints[Random.Range(0, currentWaypoint.nextWaypoints.Count)];
                hasWaited = false;
            }
            else
            {
                Destroy(gameObject);
                return;
            }
        }

        //Check for cars in a queue using an OverlapBox.
        if (IsCarInQueueBox())
            return;

        //Start driving again.
        cachedTransform.position += cachedTransform.up * (speed * Time.deltaTime);
    }
    
    private bool IsCarAhead()
    {
        Collider2D[] hits = Physics2D.OverlapCircleAll(cachedTransform.position, detectionDistance, carLayer);
        foreach (Collider2D hit in hits)
        {
            if (hit.gameObject == gameObject)
                continue;

            Vector2 directionToOther = (hit.transform.position - cachedTransform.position).normalized;
            float angleBetween = Vector2.Angle(cachedTransform.up, directionToOther);
            if (angleBetween < coneAngle * 0.5f)
            {
                return true;
            }
        }
        return false;
    }
    
    private bool IsCarInQueueBox()
    {
        Vector2 boxSize = new Vector2(0.5f, waitingGap);
        Vector2 boxCenter = (Vector2)cachedTransform.position + (Vector2)cachedTransform.up * (waitingGap * 0.5f);
        float angle = cachedTransform.eulerAngles.z;
        Collider2D blocker = Physics2D.OverlapBox(boxCenter, boxSize, angle, carLayer);
        
        //Debug visualization
#if UNITY_EDITOR
        if (UnityEngine.Application.isEditor && !UnityEngine.Application.isPlaying)
        {
            // Draw the rotated box in editor mode
            UnityEngine.Debug.DrawRay(boxCenter, Quaternion.Euler(0, 0, angle) * Vector3.right * (boxSize.x * 0.5f), Color.blue);
            UnityEngine.Debug.DrawRay(boxCenter, Quaternion.Euler(0, 0, angle) * Vector3.left * (boxSize.x * 0.5f), Color.blue);
            UnityEngine.Debug.DrawRay(boxCenter, Quaternion.Euler(0, 0, angle) * Vector3.up * (boxSize.y * 0.5f), Color.blue);
            UnityEngine.Debug.DrawRay(boxCenter, Quaternion.Euler(0, 0, angle) * Vector3.down * (boxSize.y * 0.5f), Color.blue);
        }
#endif
    
        return (blocker != null && blocker.gameObject != gameObject);
    }
    
    private void OnCollisionEnter2D(Collision2D collision)
    {
        //Check if we collided with another car
        if (((1 << collision.gameObject.layer) & carLayer) != 0)
        {
            isTouchingCar = true;
        }
    }

    private void OnTriggerEnter2D(Collider2D collider)
    {
        //Check if we triggered with another car
        if (((1 << collider.gameObject.layer) & carLayer) != 0 && collider.gameObject != gameObject)
        {
            isTouchingCar = true;
        }
    }

    //Visualize detection areas in the Scene view.
    void OnDrawGizmosSelected()
    {
        if (cachedTransform == null)
            cachedTransform = transform;

        //Draw the cone for collision detection.
        Gizmos.color = Color.red;
        Vector2 origin = cachedTransform.position;
        Vector2 forward = cachedTransform.up * detectionDistance;
        Vector2 rightBoundary = Quaternion.Euler(0, 0, coneAngle * 0.5f) * cachedTransform.up * detectionDistance;
        Vector2 leftBoundary = Quaternion.Euler(0, 0, -coneAngle * 0.5f) * cachedTransform.up * detectionDistance;

        Gizmos.DrawLine(origin, origin + forward);
        Gizmos.DrawLine(origin, origin + rightBoundary);
        Gizmos.DrawLine(origin, origin + leftBoundary);
        Gizmos.DrawWireSphere(origin, detectionDistance);

        //Draw the rotated OverlapBox for queue checking
        Gizmos.color = Color.green;
        Vector2 boxSize = new Vector2(0.5f, waitingGap);
        Vector2 boxCenter = (Vector2)cachedTransform.position + (Vector2)cachedTransform.up * (waitingGap * 0.5f);
    
        // Box rotation handler
        Matrix4x4 originalMatrix = Gizmos.matrix;
        Gizmos.matrix = Matrix4x4.TRS(boxCenter, cachedTransform.rotation, Vector3.one);
        Gizmos.DrawWireCube(Vector3.zero, new Vector3(boxSize.x, boxSize.y, 0.1f));
        Gizmos.matrix = originalMatrix;
    }
}