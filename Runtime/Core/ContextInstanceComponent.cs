using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace OpenUGD.Core
{
    public class ContextInstanceComponent : MonoBehaviour, IContextInstanceProvider
    {
        [field: SerializeField] public AudioListener AudioListener { get; private set; }
        private readonly Lifetime.Definition _definition = Lifetime.Eternal.DefineNested();
        private Signal _onChange;

        private void OnDestroy() => _definition.Terminate();

        [field: SerializeField] public EventSystem EventSystem { get; private set; }

        [field: SerializeField]
        [field: HideInInspector]
        public int TargetDisplay { get; set; }

        public bool IsSelected { get; private set; }

        public void Subscribe(Lifetime lifetime, Action listener)
        {
            if (_onChange == null)
            {
                _onChange = new Signal(_definition.Lifetime);
            }

            _onChange.Subscribe(lifetime, listener);
        }

        public void Select()
        {
            IsSelected = true;
            EventSystem.enabled = true;
            AudioListener.enabled = true;
            if (!IsSelected)
            {
                Fire();
            }
        }

        public void Unselect()
        {
            IsSelected = false;
            EventSystem.enabled = false;
            AudioListener.enabled = false;
            if (IsSelected)
            {
                Fire();
            }
        }

        private void Fire()
        {
            if (_onChange != null)
            {
                _onChange.Fire();
            }
        }
    }
}
