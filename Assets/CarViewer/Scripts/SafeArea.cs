using UnityEngine;

namespace CarViewer
{
    /// <summary>Keeps a UI rect inside the device safe area (notches, cut-outs, gesture bars).</summary>
    [RequireComponent(typeof(RectTransform))]
    public class SafeArea : MonoBehaviour
    {
        Rect applied;
        Vector2Int screenSize;

        void Update()
        {
            if (applied != Screen.safeArea || screenSize.x != Screen.width || screenSize.y != Screen.height)
                Apply();
        }

        void Apply()
        {
            applied = Screen.safeArea;
            screenSize = new Vector2Int(Screen.width, Screen.height);
            if (Screen.width == 0 || Screen.height == 0) return;
            var rt = (RectTransform)transform;
            Vector2 min = applied.position;
            Vector2 max = applied.position + applied.size;
            min.x /= Screen.width; min.y /= Screen.height;
            max.x /= Screen.width; max.y /= Screen.height;
            rt.anchorMin = min;
            rt.anchorMax = max;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }
    }
}
