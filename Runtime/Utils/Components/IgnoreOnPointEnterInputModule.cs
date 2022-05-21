using UnityEngine.EventSystems;

namespace OpenUGD.Utils.Components
{
    public class IgnoreOnPointEnterInputModule : StandaloneInputModule
    {
        public override bool IsPointerOverGameObject(int pointerId)
        {
            var pointerEventData = GetLastPointerEventData(pointerId);
            if (pointerEventData != null)
            {
                if (pointerEventData.pointerEnter != null)
                {
                    var ignore = pointerEventData.pointerEnter.GetComponent<IgnoreOnPointerEnter>();
                    if (ignore)
                    {
                        return !ignore.ignore;
                    }

                    return true;
                }
            }

            return false;
        }
    }
}
