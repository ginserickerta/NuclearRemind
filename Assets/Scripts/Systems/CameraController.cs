using UnityEngine;

namespace NuclearReMind
{
    /// <summary>
    /// ควบคุมกล้อง Orthographic: เลื่อนด้วย WASD และ Zoom ด้วย Scroll
    /// </summary>
    public class CameraController : MonoBehaviour
    {
        [Header("Pan")]
        public float panSpeed = 10f;

        [Header("Zoom")]
        public float zoomSpeed = 10f;
        public float minZoom = 3f;
        public float maxZoom = 15f;

        private Camera cam;

        private void Awake()
        {
            cam = GetComponent<Camera>();
            cam.orthographic = true;
        }

        private void Update()
        {
            HandlePan();
            HandleZoom();
        }

        /// <summary>
        /// เลื่อนกล้องด้วย WASD
        /// </summary>
        private void HandlePan()
        {
            Vector3 move = Vector3.zero;

            if (Input.GetKey(KeyCode.W)) move.y += 1f;
            if (Input.GetKey(KeyCode.S)) move.y -= 1f;
            if (Input.GetKey(KeyCode.A)) move.x -= 1f;
            if (Input.GetKey(KeyCode.D)) move.x += 1f;

            transform.position += move * panSpeed * Time.deltaTime;
        }

        /// <summary>
        /// ซูมกล้องด้วย scroll wheel
        /// </summary>
        private void HandleZoom()
        {
            float scroll = Input.GetAxis("Mouse ScrollWheel");
            if (Mathf.Approximately(scroll, 0f))
                return;

            cam.orthographicSize -= scroll * zoomSpeed;
            cam.orthographicSize = Mathf.Clamp(cam.orthographicSize, minZoom, maxZoom);
        }
    }
}
