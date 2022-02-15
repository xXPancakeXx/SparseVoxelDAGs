using UnityEditor;
using UnityEngine;

namespace Utils
{
    public class Math
    {
        public static int FastLog2(int value)
        {
            int bits = 0;
            while (value > 1)
            {
                bits++;
                value >>= 1;
            }
            return bits;
        }
    }
}