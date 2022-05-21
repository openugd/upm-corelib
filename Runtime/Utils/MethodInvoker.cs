using System;
using System.Reflection;
using UnityEngine.Assertions;

namespace OpenUGD.Utils
{
    public class MethodInvoker<TType, TAttribute> where TAttribute : Attribute
    {
        private static MethodInfo[] _methods;

        public static void Invoke(TType obj)
        {
            if (_methods == null)
            {
                _methods = MethodAttributeUtil.GetMethods(typeof(TType), typeof(TAttribute));
                Assert.IsTrue(_methods.Length > 0);
            }

            foreach (var method in _methods)
            {
                method.Invoke(obj, null);
            }
        }

        public static void Invoke(TType obj, params object[] parameters)
        {
            if (_methods == null)
            {
                _methods = MethodAttributeUtil.GetMethods(typeof(TType), typeof(TAttribute));
                Assert.IsTrue(_methods.Length > 0);
            }

            foreach (var method in _methods)
            {
                method.Invoke(obj, parameters);
            }
        }
    }
}
