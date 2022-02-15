using Assets.Scripts.Entities;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using Ray = Assets.Scripts.Entities.Ray;

namespace Assets.Scripts
{
    public class DebugStack : MonoBehaviour
    {
        public static DebugStack instance;

        public List<AABB> boxes = new List<AABB>();
        public List<float3> points = new List<float3>();

        public Ray ray;
        public float3 intersection;

        public Material voxelMat;
        public Material intersectionMat;

        public bool wireFrame;

        private bool step;
        private bool waitForUserInput = true;

        private void Awake()
        {
            instance = this;
        }

        void Update()
        {
            if (wireFrame)
            {
                for (int i = 0; i < boxes.Count; i++)
                {
                    DebugDrawer.DrawBox(boxes[i]);
                }

                Debug.DrawRay(ray.origin, ray.direction * 10f, Color.red);
            }
            else
            {
                for (int i = 0; i < boxes.Count; i++)
                {
                    DebugDrawer.DrawBoxSolid(boxes[i], voxelMat);
                }

                for (int i = 0; i < points.Count; i++)
                {
                    DebugDrawer.DrawSphereSolid(points[i], 0.05f, intersectionMat);
                }

                Debug.DrawRay(ray.origin, ray.direction * 10f, Color.red);
                if ((Vector3)intersection != Vector3.zero) DebugDrawer.DrawSphereSolid(intersection, 0.1f, intersectionMat);
            }
        }

        private void OnDrawGizmos()
        {
            if (wireFrame)
            {
                if ((Vector3)intersection != Vector3.zero) Gizmos.DrawSphere(intersection, 0.1f);

                for (int i = 0; i < points.Count; i++)
                {
                    Gizmos.DrawSphere(points[i], 0.05f);
                }
            }
        }

        [ContextMenu("Toggle Wait")]
        public void ToggleWait()
        {
            instance.waitForUserInput = !instance.waitForUserInput;
        }

        public static void WaitForUserInput(bool enable)
        {
            instance.waitForUserInput = enable;
        }

        public static void Reset()
        {
            instance.boxes.Clear();
            instance.ray = default;
            instance.intersection = default;
            instance.points.Clear();
        }

        public static void Step()
        {
            instance.step = true;
        }

        public static async Task WaitForStep()
        {
            await new WaitUntil(() => instance.step);
            if (instance.waitForUserInput) instance.step = false;
        }

        public static IEnumerator WaitForStepCoroutine()
        {
            yield return new WaitUntil(() => instance.step);
            if (instance.waitForUserInput) instance.step = false;
        }

        public static void PushBox(AABB box)
        {
            instance.boxes.Add(box);
        }

        public static void SetIntersection(float3 pos)
        {
            instance.intersection = pos;
        }


        public static void PopBox()
        {
            instance.boxes.RemoveAt(instance.boxes.Count - 1);
        }

        public static void SetRay(Ray ray)
        {
            instance.ray = ray;
        }

        public static void PushPoint(float3 p)
        {
            instance.points.Add(p);
        }

        public static void PopPoint()
        {
            instance.points.RemoveAt(instance.points.Count - 1);
        }

    }
}