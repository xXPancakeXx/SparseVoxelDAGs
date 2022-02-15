using UnityEngine;

namespace Assets.Scripts.Controls
{
    public class SimpleCameraController : MonoBehaviour
    {

        /*
        Writen by Windexglow 11-13-10.  Use it, edit it, steal it I don't care.  
        Converted to C# 27-02-13 - no credit wanted.
        Simple flycam I made, since I couldn't find any others made public.  
        Made simple to use (drag and drop, done) for regular keyboard layout  
        wasd : basic movement
        shift : Makes camera accelerate
        space : Moves camera on X and Z axis only.  So camera doesn't gain any height*/


        public float mainSpeed = 1.0f;      //regular speed
        public float shiftAdd = 250.0f;     //multiplied by how long shift is held.  Basically running
        public float maxShift = 1000.0f;    //Maximum speed when holdin gshift
        public float camSens = 0.25f;       //How sensitive it with mouse
        public bool invertY = true;

        public float scrollWheelSens = 1f;

        private float totalRun = 1.0f;

        void Update()
        {
            if (!IsMouseOverGameWindow()) return;

            if (Input.GetMouseButton(1))
            {
                var mouseMoveY = invertY ? -1 * Input.GetAxis("Mouse Y") : Input.GetAxis("Mouse Y");
                var mouseMoveX = Input.GetAxis("Mouse X");

                var mouseMove = new Vector3(mouseMoveY, mouseMoveX, 0) * camSens;
                transform.eulerAngles = transform.eulerAngles + mouseMove;
            }

            if (Input.GetMouseButtonDown(1))
            {
                Cursor.visible = false;
                Cursor.lockState = CursorLockMode.Locked;
            }

            if (Input.GetMouseButtonUp(1))
            {
                Cursor.visible = true;
                Cursor.lockState = CursorLockMode.None;
            }

            //Mouse  camera angle done.  

            //Keyboard commands
            Vector3 p = GetBaseInput();
            if (p.sqrMagnitude > 0)
            { // only move while a direction key is pressed
                if (Input.GetKey(KeyCode.LeftShift))
                {
                    totalRun += Time.deltaTime;
                    p = p * totalRun * shiftAdd;
                    p.x = Mathf.Clamp(p.x, -maxShift, maxShift);
                    p.y = Mathf.Clamp(p.y, -maxShift, maxShift);
                    p.z = Mathf.Clamp(p.z, -maxShift, maxShift);
                }
                else
                {
                    totalRun = Mathf.Clamp(totalRun * 0.5f, 1f, 1000f);
                    p = p * mainSpeed;
                }

                p = p * Time.deltaTime;
                Vector3 newPosition = transform.position;
                if (Input.GetKey(KeyCode.Space))
                { //If player wants to move on X and Z axis only
                    transform.Translate(p);
                    newPosition.x = transform.position.x;
                    newPosition.z = transform.position.z;
                    transform.position = newPosition;
                }
                else
                {
                    transform.Translate(p);
                }
            }

            var scroll = Input.GetAxis("Mouse ScrollWheel");
            mainSpeed += scroll * scrollWheelSens;
        }

        private Vector3 GetBaseInput()
        { //returns the basic values, if it's 0 than it's not active.
            Vector3 p_Velocity = new Vector3();
            if (Input.GetKey(KeyCode.W))
            {
                p_Velocity += new Vector3(0, 0, 1);
            }
            if (Input.GetKey(KeyCode.S))
            {
                p_Velocity += new Vector3(0, 0, -1);
            }
            if (Input.GetKey(KeyCode.A))
            {
                p_Velocity += new Vector3(-1, 0, 0);
            }
            if (Input.GetKey(KeyCode.D))
            {
                p_Velocity += new Vector3(1, 0, 0);
            }
            if (Input.GetKey(KeyCode.Q))
            {
                p_Velocity += new Vector3(0, 1, 0);
            }
            if (Input.GetKey(KeyCode.E))
            {
                p_Velocity += new Vector3(0, -1, 0);
            }
            return p_Velocity;
        }

        private bool IsMouseOverGameWindow() => !(0 > Input.mousePosition.x || 0 > Input.mousePosition.y || Screen.width < Input.mousePosition.x || Screen.height < Input.mousePosition.y);
    }
}
