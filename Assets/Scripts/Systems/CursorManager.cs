using UnityEngine;
using UnityEngine.EventSystems;

namespace NuclearReMind
{
    /// <summary>
    /// คุมรูปเคอร์เซอร์เมาส์ตามบริบท (wire โดย AtlasUISetup):
    ///   • ปกติ            → ลูกศร (arrow)
    ///   • ชี้บนอาคารในกริด → มือชี้ (hand)
    ///   • โหมดทุบอาคาร     → ค้อน (hammer)
    /// อ่าน InputManager/GridManager แบบ read-only · ฟัง OnDemolishModeToggled ผ่าน EventManager
    /// เรียก Cursor.SetCursor เฉพาะตอนสถานะเปลี่ยน (กัน set ทุกเฟรม)
    /// </summary>
    public class CursorManager : MonoBehaviour
    {
        [Header("Textures (wire โดย AtlasUISetup)")]
        public Texture2D arrowCursor;
        public Vector2 arrowHotspot = Vector2.zero;
        public Texture2D handCursor;
        public Vector2 handHotspot = new Vector2(10f, 2f);
        public Texture2D hammerCursor;
        public Vector2 hammerHotspot = new Vector2(6f, 4f);

        private enum CursorState { Arrow, Hand, Hammer }
        private CursorState _current = (CursorState)(-1);
        private bool _demolishing;

        private void OnEnable()
        {
            EventManager.Instance.OnDemolishModeToggled += HandleDemolish;
        }

        private void OnDisable()
        {
            if (EventManager.Instance == null) return;
            EventManager.Instance.OnDemolishModeToggled -= HandleDemolish;
        }

        private void HandleDemolish(bool active) => _demolishing = active;

        private void Start() => Apply(CursorState.Arrow);

        private void Update()
        {
            CursorState want = _demolishing ? CursorState.Hammer
                             : HoveringBuilding() ? CursorState.Hand
                             : CursorState.Arrow;
            if (want != _current) Apply(want);
        }

        // ชี้อยู่บน cell ที่มีอาคาร (และไม่ได้อยู่เหนือ UI)
        private bool HoveringBuilding()
        {
            var gm = GridManager.Instance;
            var im = InputManager.Instance;
            if (gm == null || im == null) return false;
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return false;

            var c = im.GetMouseGridPosition();
            if (!gm.IsInBounds(c.x, c.y)) return false;
            var cell = gm.GetCell(c.x, c.y);
            return cell != null && cell.isOccupied;
        }

        private void Apply(CursorState state)
        {
            _current = state;
            switch (state)
            {
                case CursorState.Hammer:
                    if (hammerCursor != null) { Cursor.SetCursor(hammerCursor, hammerHotspot, CursorMode.Auto); return; }
                    break;
                case CursorState.Hand:
                    if (handCursor != null) { Cursor.SetCursor(handCursor, handHotspot, CursorMode.Auto); return; }
                    break;
            }
            // default / fallback → arrow (null = เคอร์เซอร์ระบบ)
            Cursor.SetCursor(arrowCursor, arrowHotspot, CursorMode.Auto);
        }
    }
}
