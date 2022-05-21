using System;
using System.Collections.Generic;
using OpenUGD.Services;
using UnityEngine;

namespace OpenUGD.Core.ContextBuilder
{
    public class UnityDebugServiceObserver : MonoBehaviour, IServicesObserverRegister
    {
        [NonSerialized] public readonly List<Service> Services = new();

        public void Register(Service service) => Services.Add(service);

        public static IServicesObserverRegister Create(string context, Lifetime lifetime)
        {
            var go = new GameObject($"{context}-Services");
            DontDestroyOnLoad(go);
            lifetime.AddAction(() => Destroy(go));
            return go.AddComponent<UnityDebugServiceObserver>();
        }
    }
}
