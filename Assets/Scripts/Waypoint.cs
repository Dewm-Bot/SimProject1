using UnityEngine;
using System.Collections.Generic;

public enum ServiceType {
    None,
    Order,
    Payment,
    Preparation
}

public class Waypoint : MonoBehaviour
{
    [Tooltip("If true, the car will stop and wait at this waypoint.")]
    public bool isStopPoint = false;

    [Tooltip("Mark this waypoint as a specific service point (set to None if not a service point).")]
    public ServiceType serviceType = ServiceType.None;

    [Tooltip("List of next waypoints for this waypoint.")]
    public List<Waypoint> nextWaypoints;
}