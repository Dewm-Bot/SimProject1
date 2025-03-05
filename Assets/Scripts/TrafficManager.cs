using System.Collections;
using UnityEngine;

public class TrafficManager : MonoBehaviour
{
    public static TrafficManager Instance;

    [Header("Spawning Settings")]
    public CarAI carPrefab; 
    public Transform spawnPoint;
    public float averageArrivalInterval = 3f;
    public int maxCars = 10;

    [Header("Waypoint Settings")]
    public Waypoint initialWaypoint;

    [Header("Service Time Settings")]
    [Tooltip("Total average service time in seconds (e.g., 180 seconds for 3 minutes)")]
    public float totalServiceTime = 180f;
    [Tooltip("Percentage of total service time for ordering (e.g., 0.2 for 20%)")]
    [Range(0f,1f)]
    public float orderPercentage = 0.2f;
    [Tooltip("Percentage of total service time for payment (e.g., 0.15 for 15%)")]
    [Range(0f,1f)]
    public float paymentPercentage = 0.15f;

    private int currentCarCount = 0;
    private Transform cachedSpawnPointTransform;

    void Awake()
    {
        Instance = this;
        cachedSpawnPointTransform = spawnPoint;
    }

    void Start()
    {
        StartCoroutine(SpawnRoutine());
    }

    IEnumerator SpawnRoutine()
    {
        while (true)
        {
            if (currentCarCount < maxCars)
            {
                SpawnCar();
            }
            float waitTime = Random.Range(averageArrivalInterval * 0.8f, averageArrivalInterval * 1.2f);
            yield return new WaitForSeconds(waitTime);
        }
    }

    void SpawnCar()
    {
        CarAI carAI = Instantiate(carPrefab, cachedSpawnPointTransform.position, cachedSpawnPointTransform.rotation);
        currentCarCount++;

        //Set the car's initial waypoint and reference to this TrafficManager.
        carAI.initialWaypoint = initialWaypoint;
        carAI.trafficManager = this;
    }


    void InitializeCarAI(CarAI carAI)
    {
        if (carAI)
        {
            carAI.initialWaypoint = initialWaypoint;
            carAI.trafficManager = this;
        }
    }

    public void OnCarDestroyed()
    {
        currentCarCount = Mathf.Max(currentCarCount - 1, 0);
    }

    public float GetServiceWaitTime(ServiceType type)
    {
        switch (type)
        {
            case ServiceType.Order:
                return totalServiceTime * orderPercentage;
            case ServiceType.Payment:
                return totalServiceTime * paymentPercentage;
            case ServiceType.Preparation:
                return totalServiceTime * (1f - orderPercentage - paymentPercentage);
            default:
                return 0f;
        }
    }
}
