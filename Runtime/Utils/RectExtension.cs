using System;
using UnityEngine;

namespace OpenUGD.Utils
{
    public static class RectExtension
    {
        public static Rect Edit(this Rect rect, Func<Rect, Rect> action)
        {
            rect = action(rect);
            return rect;
        }
    }
}
