// Copyright (c) 2013-2019 Innoactive GmbH
// Licensed under the Apache License, Version 2.0
// Modifications copyright (c) 2021-2026 MindPort GmbH

using UnityEngine;

namespace VRBuilder.Unity
{
    /// <summary>
    /// Make sure we always have one and only one instance of this class when we need it.
    /// </summary>
    public class UnitySingleton<T> : MonoBehaviour where T : UnitySingleton<T>
    {
        /// <summary>
        /// Semaphore to avoid instantiating the singleton twice.
        /// </summary>
        private static object _lock = new object();

        /// <summary>
        /// The actual instance of the singleton object.
        /// </summary>
        private static T instance;

        /// <summary>
        /// Public accessor for the singleton object.
        /// Will create a new instance if necessary.
        /// </summary>
        public static T Instance
        {
            get
            {
                // Share shutdown state through a non-generic owner so one runtime hook can reset
                // it for every singleton type. During shutdown, cleanup callers must not create objects.
                if (UnitySingletonLifetime.IsApplicationQuitting)
                {
                    return null;
                }
                lock (_lock)
                {
                    if (instance == null)
                    {
                        GameObject g = new GameObject();
                        g.name = "Singleton_ofType_" + typeof(T).ToString();
                        DontDestroyOnLoad(g);
                        instance =  g.AddComponent<T>();
                    }
                }
                return instance;
            }
            protected set
            {
                instance = value;
            }
        }

        protected virtual void Awake()
        {
            // make sure to assign the instance on awake
            if (instance == null)
            {
                instance = (T)this;
                DontDestroyOnLoad(instance);
            }
            else
            {
                if (Instance != this)
                {
                    Destroy(this);
                    Debug.LogWarningFormat("An instance of the singleton {0} already exists.", typeof(T).Name);
                } else
                {
                    DontDestroyOnLoad(instance);
                }
            }
        }

        protected virtual void OnDestroy()
        {
            // Ordinary destruction must allow later recreation. Treating every destruction as
            // application shutdown left coroutine dispatch permanently unavailable without a reload.
            // Only release our own slot, since a replacement instance may already have been assigned.
            if (instance == this)
            {
                instance = null;
            }
        }

        protected virtual void OnApplicationQuit()
        {
            // Unity calls this for actual application/play-session shutdown, before destruction.
            // Other components may still request the singleton while their own teardown is running.
            UnitySingletonLifetime.IsApplicationQuitting = true;
        }
    }

    /// <summary>
    /// Tracks application shutdown across singleton types, resetting before each play session.
    /// </summary>
    /// <remarks>
    /// Explicit session initialization also works in the older Unity versions supported by these
    /// reusable packages; it does not require Unity 6.6 automatic static-cleanup attributes.
    /// </remarks>
    internal static class UnitySingletonLifetime
    {
        internal static bool IsApplicationQuitting { get; set; }

        // Unity does not invoke runtime initialization methods declared on generic types.
        // Run before scene initialization so a previous session's quit flag cannot block the first request.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            IsApplicationQuitting = false;
        }
    }
}
