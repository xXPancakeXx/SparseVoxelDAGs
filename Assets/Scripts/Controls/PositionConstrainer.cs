using Unity.Mathematics;
using UnityEditor;
using UnityEngine;

namespace Assets.Scripts.Controls
{
    public class PositionConstrainer : MonoBehaviour
    {
        public float3 min;
        public float3 max;
        public float delta;

        public void Update()
        {
            var pos = math.min(math.max(transform.position, min + delta), max + delta);
            if (math.distance(transform.position, pos) >= 1e-4) transform.position = pos;
        }
    }
}