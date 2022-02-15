using UnityEngine;

namespace Assets.Scripts
{
    public class DebugTriangleHighlighter : MonoBehaviour
    {
        public bool enable;

        public void FixedUpdate()
        {
            if (!enable) return;
            if (!Input.GetMouseButtonDown(0)) return;

            var ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            var hit = Physics.Raycast(ray, out var hitInfo);
            if (hit)
            {
                Debug.Log($"Meshgo: {hitInfo.collider.gameObject.name} TriIdx: {hitInfo.triangleIndex}");
            }

        }

    }
}