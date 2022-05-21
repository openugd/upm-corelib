using UnityEngine;

namespace OpenUGD.Utils
{
    public static class ArrayUtils
    {
        public static void ShuffleArray<T>(T[] array)
        {
            for (var i = array.Length - 1; i > 0; i--)
            {
                var j = Mathf.FloorToInt(Random.value * (i + 1));
                var temp = array[i];
                array[i] = array[j];
                array[j] = temp;
            }
        }
    }
}
