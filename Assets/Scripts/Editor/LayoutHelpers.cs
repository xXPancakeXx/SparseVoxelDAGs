﻿using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Assets.Scripts.Editor
{
    public class VerticalBlock : IDisposable
    {
        public VerticalBlock(params GUILayoutOption[] options)
        {
            GUILayout.BeginVertical(options);
        }

        public VerticalBlock(GUIStyle style, params GUILayoutOption[] options)
        {
            GUILayout.BeginVertical(style, options);
        }

        public void Dispose()
        {
            GUILayout.EndVertical();
        }
    }

    public class ScrollviewBlock : IDisposable
    {
        public ScrollviewBlock(ref Vector2 scrollPos, params GUILayoutOption[] options)
        {
            scrollPos = GUILayout.BeginScrollView(scrollPos, options);
        }

        public void Dispose()
        {
            GUILayout.EndScrollView();
        }
    }
    
    public class FoldoutBlockButton : IDisposable
    {
        public FoldoutBlockButton(ref bool value, string text, Action buttonDownAction, params GUILayoutOption[] options)
        {
            EditorGUI.indentLevel++;
            using (new HorizontalBlock())
            {
                value = EditorGUILayout.Foldout(value, text, true);  
                if (GUILayout.Button("-", GUILayout.ExpandWidth(true), GUILayout.MaxWidth(30)))
                {
                    buttonDownAction();
                }
            }
            EditorGUI.indentLevel++;
        }

        public void Dispose()
        {
            EditorGUI.indentLevel -= 2;
        }
    }
    
    public class FoldoutBlock : IDisposable
    {
        public FoldoutBlock(ref bool value, string text, params GUILayoutOption[] options)
        {
            EditorGUI.indentLevel++;
            value = EditorGUILayout.Foldout(value, text, true);  
            EditorGUI.indentLevel++;
        }

        public void Dispose()
        {
            EditorGUI.indentLevel -= 2;
        }
    }

    public class HorizontalBlock : IDisposable
    {
        public HorizontalBlock(params GUILayoutOption[] options)
        {
            GUILayout.BeginHorizontal(options);
        }

        public HorizontalBlock(GUIStyle style, params GUILayoutOption[] options)
        {
            GUILayout.BeginHorizontal(style, options);
        }

        public void Dispose()
        {
            GUILayout.EndHorizontal();
        }
    }

    public class ColoredBlock : IDisposable
    {
        public ColoredBlock(Color color)
        {
            GUI.color = color;
        }

        public void Dispose()
        {
            GUI.color = Color.white;
        }
    }

    [Serializable]
    public class TabsBlock
    {
        private  Dictionary<string, Action> methods;
        private Action currentGuiMethod;
        public int curMethodIndex = -1;

        public TabsBlock(Dictionary<string, Action> _methods)
        {
            methods = _methods;
            SetCurrentMethod(0);
        }

        public void Draw()
        {
            var keys = methods.Keys.ToArray();
            using (new VerticalBlock(EditorStyles.helpBox))
            {
                using (new HorizontalBlock())
                {
                    for (int i = 0; i < keys.Length; i++)
                    {
                        var btnStyle = i == 0 ? EditorStyles.miniButtonLeft : i == (keys.Length - 1) ? EditorStyles.miniButtonRight : EditorStyles.miniButtonMid;
                        using (new ColoredBlock(currentGuiMethod == methods[keys[i]] ? Color.grey : Color.white))
                            if (GUILayout.Button(keys[i], btnStyle))
                                SetCurrentMethod(i);
                    }
                }
                GUILayout.Label(keys[curMethodIndex], EditorStyles.centeredGreyMiniLabel);
                currentGuiMethod();
            }
        }

        public void SetCurrentMethod(int index)
        {
            curMethodIndex = index;
            currentGuiMethod = methods[methods.Keys.ToArray()[index]];
        }
    }
}