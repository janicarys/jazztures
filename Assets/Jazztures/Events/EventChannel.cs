using System;
using UnityEngine;

namespace Jazztures.Events
{
    /// <summary>
    /// A typed ScriptableObject event channel (CLAUDE.md §2.3) — the one sanctioned way
    /// for systems to talk across assembly boundaries. No <c>FindObjectOfType</c>, no
    /// singletons.
    ///
    /// <para>
    /// <b>Direction rule:</b> input → domain → presentation. Whatever bridges the domain
    /// raises; presentation registers and reacts. Presentation must never call
    /// <see cref="Raise"/>.
    /// </para>
    ///
    /// <para>
    /// Consumers <see cref="Register"/> in <c>OnEnable</c> and <see cref="Unregister"/> in
    /// <c>OnDisable</c> so subscriptions do not survive a scene change.
    /// </para>
    /// </summary>
    public abstract class EventChannel<T> : ScriptableObject
    {
#if UNITY_EDITOR
        [SerializeField]
        [Tooltip("Editor only: log every raised value to the Console.")]
        private bool _logToConsole;
#endif

        private Action<T> _handlers;

        public void Register(Action<T> handler) => _handlers += handler;

        public void Unregister(Action<T> handler) => _handlers -= handler;

        public void Raise(T value)
        {
#if UNITY_EDITOR
            if (_logToConsole)
            {
                Debug.Log($"[{name}] {value}", this);
            }
#endif
            _handlers?.Invoke(value);
        }
    }
}
