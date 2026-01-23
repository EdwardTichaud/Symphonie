using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Centralise les references aux services runtime pour eviter les recherches repetees.
/// </summary>
public static class ServiceRegistry
{
    private static readonly Dictionary<Type, UnityEngine.Object> Services = new();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetCachesOnDomainReload()
    {
        Services.Clear();
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        Services.Clear();
    }

    public static void Register<T>(T service) where T : UnityEngine.Object
    {
        if (service == null)
            return;

        Type type = typeof(T);
        if (Services.TryGetValue(type, out UnityEngine.Object existing) && existing != null && existing != service)
        {
            Debug.LogWarning($"[ServiceRegistry] Duplicate service for {type.Name} ignored.", service);
            return;
        }

        Services[type] = service;
    }

    public static void Register(UnityEngine.Object service)
    {
        if (service == null)
            return;

        Type type = service.GetType();
        if (Services.TryGetValue(type, out UnityEngine.Object existing) && existing != null && existing != service)
        {
            Debug.LogWarning($"[ServiceRegistry] Duplicate service for {type.Name} ignored.", service);
            return;
        }

        Services[type] = service;
    }

    public static void Unregister<T>(T service) where T : UnityEngine.Object
    {
        if (service == null)
            return;

        Type type = typeof(T);
        if (Services.TryGetValue(type, out UnityEngine.Object existing) && existing == service)
            Services.Remove(type);
    }

    public static void Unregister(UnityEngine.Object service)
    {
        if (service == null)
            return;

        Type type = service.GetType();
        if (Services.TryGetValue(type, out UnityEngine.Object existing) && existing == service)
            Services.Remove(type);
    }

    public static bool TryGet<T>(out T service) where T : UnityEngine.Object
    {
        if (Services.TryGetValue(typeof(T), out UnityEngine.Object existing))
        {
            if (existing != null)
            {
                service = existing as T;
                if (service != null)
                    return true;
            }

            Services.Remove(typeof(T));
        }

        service = null;
        return false;
    }

    public static T GetOrFind<T>() where T : UnityEngine.Object
    {
        return GetOrFind<T>(FindObjectsInactive.Exclude);
    }

    public static T GetOrFind<T>(FindObjectsInactive includeInactive) where T : UnityEngine.Object
    {
        if (TryGet(out T service))
            return service;

        T found = UnityEngine.Object.FindFirstObjectByType<T>(includeInactive);
        if (found != null)
            Services[typeof(T)] = found;
        return found;
    }
}
