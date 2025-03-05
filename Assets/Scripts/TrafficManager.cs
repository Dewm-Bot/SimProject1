using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.Serialization;

public class TrafficManager : MonoBehaviour
{
    public static TrafficManager Instance;

    [Header("Queueing Model Parameters")]
    [Tooltip("Average time between car arrivals in seconds")]
    public float averageInterArrivalTime = 190.5333333f;
    [Tooltip("Average total service time in seconds")]
    public float averageServiceTime = 259.6875f;
    [SerializeField] private float trafficIntensity = 0.733702367f;
    [Tooltip("Target customers served per hour")]
    public float customersPerHour = 13.86281588f;

    [Header("Service Simulation")]
    [Tooltip("Random variation in arrival times (0-1)")]
    [Range(0f, 1f)]
    public float arrivalVariation = 0.2f;
    [Tooltip("Random variation in service times (0-1)")]
    [Range(0f, 1f)]
    public float serviceTimeVariation = 0.15f;
    [Tooltip("Percentage of total service time for ordering")]
    [Range(0f, 1f)]
    public float orderPercentage = 0.2f;
    [Tooltip("Percentage of total service time for payment")]
    [Range(0f, 1f)]
    public float paymentPercentage = 0.15f;
    [Tooltip("Percentage of total service time for food preparation")]
    [Range(0f, 1f)]
    private float preparationPercentage = 0.65f; // Computed value

    [Header("Spawning Settings")]
    [Tooltip("List of car prefabs to spawn randomly")]
    public List<CarAI> carPrefabs = new List<CarAI>();
    public Transform spawnPoint;
    public int maxCars = 10;

    [Header("Waypoint Settings")]
    public Waypoint initialWaypoint;

    [Header("Runtime Statistics")]
    [SerializeField] private int currentCarCount = 0;
    [SerializeField] private int totalCarsServed = 0;
    [FormerlySerializedAs("averageTimeInPremises")] [SerializeField] private float averageTimeInDriveThru = 0f;

    private Transform cachedSpawnPointTransform;
    private List<float> entryTimes = new List<float>(20); 
    private Coroutine spawnRoutine;
    private float minInterArrivalTime;
    private float maxInterArrivalTime;

    void Awake()
    {
        Instance = this;
        cachedSpawnPointTransform = spawnPoint;
        CalculateParameters();
    }

    void Start()
    {
        if (carPrefabs.Count == 0)
        {
            Debug.LogError("No car prefabs assigned to TrafficManager!");
            return;
        }
        
        spawnRoutine = StartCoroutine(SpawnRoutine());
    }

    private void OnValidate()
    {
        //Update the preparation percentage when order or payment percentages change
        preparationPercentage = 1f - orderPercentage - paymentPercentage;

        //Ensure consistent calculations when values are modified in inspector
        if (Application.isPlaying)
        {
            CalculateParameters();
        }
    }

    private void CalculateParameters()
    {
        //Update inter-arrival time based on customers per hour
        if (customersPerHour > 0)
            averageInterArrivalTime = 3600f / customersPerHour;

        //Calculate traffic intensity
        trafficIntensity = averageServiceTime / averageInterArrivalTime;

        //Calculate arrival time variation bounds
        minInterArrivalTime = averageInterArrivalTime * (1f - arrivalVariation);
        maxInterArrivalTime = averageInterArrivalTime * (1f + arrivalVariation);
    }

    IEnumerator SpawnRoutine()
    {
        while (true)
        {
            if (currentCarCount < maxCars)
            {
                SpawnCar();
            }

            // More efficient exponential distribution calculation
            float waitTime = -averageInterArrivalTime * Mathf.Log(Random.value);
            waitTime = Mathf.Clamp(waitTime, minInterArrivalTime, maxInterArrivalTime);

            yield return new WaitForSeconds(waitTime);
        }
    }

    void SpawnCar()
    {
        //Check if spawn is clear
        if (IsSpawnAreaOccupied())
        {
            return;
        }

        //Select a random car from the list
        CarAI selectedPrefab = carPrefabs[Random.Range(0, carPrefabs.Count)];
        
        CarAI carAI = Instantiate(selectedPrefab, cachedSpawnPointTransform.position, cachedSpawnPointTransform.rotation);
        currentCarCount++;

        //Set the car's initial waypoint and reference
        carAI.initialWaypoint = initialWaypoint;
        carAI.trafficManager = this;

        //Add car entry time for statistics
        entryTimes.Add(Time.time);

        CarDestroyNotifier notifier = carAI.GetComponent<CarDestroyNotifier>();
        notifier.manager = this;
    }

    private bool IsSpawnAreaOccupied()
    {
        if (carPrefabs.Count == 0)
            return true;
            
        // Get the largest collision radius among all car prefabs
        float maxRadius = 1.0f;
        foreach (CarAI prefab in carPrefabs)
        {
            Collider2D collider = prefab.GetComponent<Collider2D>();
            if (collider != null)
            {
                float radius = collider.bounds.extents.magnitude;
                if (radius > maxRadius)
                    maxRadius = radius;
            }
        }
        
        //Check if the spawn area is clear
        LayerMask carLayer = carPrefabs[0].carLayer; 
        Collider2D[] hits = Physics2D.OverlapCircleAll(cachedSpawnPointTransform.position, maxRadius, carLayer);
        return hits.Length > 0;
    }

    public void OnCarDestroyed()
    {
        if (entryTimes.Count > 0)
        {
            float timeInPremises = Time.time - entryTimes[0];
            entryTimes.RemoveAt(0);

            
            totalCarsServed++;
            averageTimeInDriveThru = ((averageTimeInDriveThru * (totalCarsServed - 1)) + timeInPremises) / totalCarsServed;
        }

        currentCarCount = Mathf.Max(currentCarCount - 1, 0);
    }

    public float GetServiceWaitTime(ServiceType type)
    {
        //Base service time with random variation
        float randomVariation = Random.Range(1f - serviceTimeVariation, 1f + serviceTimeVariation);
        float adjustedServiceTime = averageServiceTime * randomVariation;
        
        switch (type)
        {
            case ServiceType.Order:
                return adjustedServiceTime * orderPercentage;
            case ServiceType.Payment:
                return adjustedServiceTime * paymentPercentage;
            case ServiceType.Preparation:
                return adjustedServiceTime * preparationPercentage;
            default:
                return 0f;
        }
    }

    private void OnDestroy()
    {
        if (spawnRoutine != null)
            StopCoroutine(spawnRoutine);
    }
}