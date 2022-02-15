using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using Utils;

namespace Assets.Scripts.Controls
{
    public class CameraLocations : MonoBehaviour
    {
        public const string LOCATION_PREFS_KEY = "camLocations";

        public List<CameraLocation> cameraLocations;
        private Camera cam;

        void Start()
        {
            cam = Camera.main;
            cameraLocations = LoadArray<CameraLocation>(LOCATION_PREFS_KEY)?.ToList();
            if (cameraLocations == null) cameraLocations = new List<CameraLocation>();

            //Keyboard.current.onTextInput += Current_onTextInput;
        }

        private void OnDestroy()
        {
            SaveArray<CameraLocation>(LOCATION_PREFS_KEY, cameraLocations.ToArray());

            //Keyboard.current.onTextInput -= Current_onTextInput;
        }

        private T[] LoadArray<T>(string prefsKey)
        {
            var camLocsJson = PlayerPrefs.GetString(prefsKey);
            if (camLocsJson != null)
            {
                return Json.From<T>(camLocsJson);
            }

            return default;
        }

        private void SaveArray<T>(string prefsKey, T[] data)
        {
            var camLocsJson = Json.To(data);
            PlayerPrefs.SetString(prefsKey, camLocsJson);
        }

        private void Update()
        {
            if (GUIUtility.keyboardControl != 0 || !IsMouseOverGameWindow()) return;

            if (Keyboard.current.backquoteKey.wasPressedThisFrame)
            {
                cameraLocations.Add(new CameraLocation(cam.transform.localPosition, cam.transform.localRotation));
            }

            if (IsNumKeyPressed(out var numKey, out var modifierPressed))
            {
                if (modifierPressed)
                {
                    cameraLocations[numKey - 1] = new CameraLocation(cam.transform.localPosition, cam.transform.localRotation);
                }
                else
                {
                    var camLoc = cameraLocations[numKey - 1];
                    if (camLoc == null) return;

                    cam.transform.localPosition = camLoc.position;
                    cam.transform.localRotation = camLoc.rotation;
                }
            }
        }

        private bool IsNumKeyPressed(out int digit, out bool modifierPressed)
        {
            modifierPressed = false;
            digit = -1;

            if (Keyboard.current.anyKey.isPressed)
            {
                if (Keyboard.current.altKey.isPressed) modifierPressed = true;

                if (Keyboard.current.digit1Key.isPressed) digit = 1;
                else if (Keyboard.current.digit2Key.isPressed) digit = 2;
                else if (Keyboard.current.digit3Key.isPressed) digit = 3;
                else if (Keyboard.current.digit4Key.isPressed) digit = 4;
                else if (Keyboard.current.digit5Key.isPressed) digit = 5;
                else if (Keyboard.current.digit6Key.isPressed) digit = 6;
                else if (Keyboard.current.digit7Key.isPressed) digit = 7;
                else if (Keyboard.current.digit8Key.isPressed) digit = 8;
                else if (Keyboard.current.digit9Key.isPressed) digit = 9;

                return digit != -1;
            }

            return false;
        }

        private bool IsMouseOverGameWindow() => !(0 > Input.mousePosition.x || 0 > Input.mousePosition.y || Screen.width < Input.mousePosition.x || Screen.height < Input.mousePosition.y);

    }

    [Serializable]
    public class CameraLocation
    {
        public Vector3 position;
        public Quaternion rotation;

        public CameraLocation(Vector3 position, Quaternion rotation)
        {
            this.position = position;
            this.rotation = rotation;
        }
    }
}