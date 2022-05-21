using System.Collections.Generic;
using UnityEngine;

namespace OpenUGD.Core
{
    public class ContextFactoryInstancesComponent : MonoBehaviour
    {
        [SerializeField] public ContextInstanceComponent Prefab;

        [Range(1, 3)] [SerializeField] public int Count = 2;
        private readonly List<ContextInstanceComponent> _instances = new();
        public int Selected => _instances.FindIndex(s => s.IsSelected);

        private void Awake()
        {
            DontDestroyOnLoad(gameObject);
            Rebuild();
        }

        public void Select(int index)
        {
            if (_instances != null && _instances.Count > index && index != -1 &&
                !_instances[index].IsSelected)
            {
                var current = _instances.Find(a => a.IsSelected);
                if (current != null)
                {
                    current.Unselect();
                }

                _instances[index].Select();
            }
        }

        public void Rebuild()
        {
            DeleteLast();
            Create(Count);
        }

        private void Create(int count)
        {
            try
            {
                for (var i = 0; i < count; i++)
                {
                    Prefab.TargetDisplay = i;
                    var instance = Instantiate(Prefab);
                    instance.gameObject.name = $"{Prefab.gameObject.name}_{i}";
                    _instances.Add(instance);
                    if (i == 0)
                    {
                        instance.Select();
                    }
                    else
                    {
                        instance.Unselect();
                    }
                }
            }
            finally
            {
                Prefab.TargetDisplay = 0;
            }
        }

        private void DeleteLast()
        {
            var copy = _instances.ToArray();
            _instances.Clear();
            foreach (var instance in copy)
            {
                Destroy(instance.gameObject);
            }
        }
    }
}
