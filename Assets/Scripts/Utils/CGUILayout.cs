using UnityEditor;
using UnityEngine;

namespace Utils
{
    public static class CGUILayout
    {
        /// <summary>
        /// 
        /// </summary>
        /// <param name="toggle"></param>
        /// <param name="text"></param>
        /// <returns>True if the toggle has changed</returns>
        public static bool Toggle(ref bool toggle, string text)
        {
            bool oldVal = toggle;
            toggle = GUILayout.Toggle(toggle, text);
            return oldVal != toggle;
        }
    }
}