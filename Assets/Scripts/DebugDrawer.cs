using Assets.Scripts.Entities;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using static Assets.Scripts.Raytracer.CPURaytracer;

namespace Assets.Scripts
{
    public class DebugDrawer
    {
        public static Color color = Color.green;
        public static Mesh boxMesh;
        public static Mesh sphereMesh;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Init()
        {
            boxMesh = Utils.Mesh.CreateBox(1);
            sphereMesh = Utils.Mesh.CreateSphere(1);
        }

        public static void DrawBox(AABB box)
        {
            DrawBox(box, color);
        }

        public static void DrawBox(AABB box, Color col)
        {
            var size = box[1] - box[0];

            var v3FrontTopLeft = box[0] + math.up() * size;         // Front top left corner
            var v3FrontTopRight = box[1] - math.forward() * size;   // Front top right corner
            var v3FrontBottomLeft = box[0];                         // Front bottom left corner
            var v3FrontBottomRight = box[0] + math.right() * size;  // Front bottom right corner
            var v3BackTopLeft = box[1] - math.right() * size;       // Back top left corner
            var v3BackTopRight = box[1];                            // Back top right corner
            var v3BackBottomLeft = box[0] + math.forward() * size;  // Back bottom left corner
            var v3BackBottomRight = box[1] - math.up() * size;      // Back bottom right corner

            Debug.DrawLine(v3FrontTopLeft, v3FrontTopRight, col);
            Debug.DrawLine(v3FrontTopRight, v3FrontBottomRight, col);
            Debug.DrawLine(v3FrontBottomRight, v3FrontBottomLeft, col);
            Debug.DrawLine(v3FrontBottomLeft, v3FrontTopLeft, col);

            Debug.DrawLine(v3BackTopLeft, v3BackTopRight, col);
            Debug.DrawLine(v3BackTopRight, v3BackBottomRight, col);
            Debug.DrawLine(v3BackBottomRight, v3BackBottomLeft, col);
            Debug.DrawLine(v3BackBottomLeft, v3BackTopLeft, col);

            Debug.DrawLine(v3FrontTopLeft, v3BackTopLeft, col);
            Debug.DrawLine(v3FrontTopRight, v3BackTopRight, col);
            Debug.DrawLine(v3FrontBottomRight, v3BackBottomRight, col);
            Debug.DrawLine(v3FrontBottomLeft, v3BackBottomLeft, col);
        }

        public static void DrawBoxSolid(AABB box, Material mat)
        {
            var size = box[1] - box[0];

            Matrix4x4 trs = Matrix4x4.TRS(box[0], quaternion.identity, size);
            Graphics.DrawMesh(boxMesh, trs, mat, 0);
        }

        public static void DrawSphereSolid(float3 pos, float radius, Material mat)
        {
            Matrix4x4 trs = Matrix4x4.TRS(pos, quaternion.identity, new float3(radius));
            Graphics.DrawMesh(sphereMesh, trs, mat, 0);
        }
    }
}