using UnityEngine;

public class CarDestroyNotifier : MonoBehaviour
{
    public TrafficManager manager;

    void OnDestroy()
    {
        if (manager)
        {
            manager.OnCarDestroyed();
        }
    }
}