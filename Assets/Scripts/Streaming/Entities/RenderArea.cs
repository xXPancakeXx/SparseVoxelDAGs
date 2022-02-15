using System.Collections.Generic;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;

namespace Assets.Scripts.Streaming.Entities
{
    public interface IRenderArea
    {
        public void FindChange(IRenderArea other, List<int2> minus, List<int2> plus);
    }

    public class AreaQuad : IRenderArea
    {
        public int2 min;
        public int2 max;

        public void FindChange(IRenderArea other, List<int2> minus, List<int2> plus)
        {
            
        }
    }
}