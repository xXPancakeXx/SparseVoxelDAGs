using Assets.Scripts.Octree;
using System;
using System.IO;
using Unity.VisualScripting;
using UnityEngine;

namespace Assets.Scripts
{
    public abstract class RenderInfo : ScriptableObject
    {
        public abstract string Path { get; set; }

        public string ModelName => System.IO.Path.GetFileName(Path).Split('_')[0];
        public string Resolution => System.IO.Path.GetFileName(Path).Split('.')[0].Split('_')[1];
        
        
        public abstract RenderData ReadFromFileNew();

        public abstract int GetMaxDepth();
    }
}