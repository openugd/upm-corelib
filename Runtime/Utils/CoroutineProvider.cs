﻿using System.Collections;
using UnityEngine;

namespace OpenUGD.Utils
{
    public class CoroutineProvider : ICoroutineProvider
    {
        private readonly MonoBehaviour _monoBehaviour;

        public CoroutineProvider(MonoBehaviour monoBehaviour) => _monoBehaviour = monoBehaviour;

        public Coroutine StartCoroutine(IEnumerator enumerator)
        {
            if (!_monoBehaviour.gameObject.activeSelf)
            {
                return null;
            }

            var result = _monoBehaviour.StartCoroutine(enumerator);
            return result;
        }

        public void StopCoroutine(Coroutine coroutine) => _monoBehaviour.StopCoroutine(coroutine);
    }
}
