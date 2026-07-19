using UnityEditor;
using UnityEngine;
using NuclearReMind;

namespace NuclearReMind.EditorTools
{
    /// <summary>
    /// Toggle for BuildingDepthSort's runtime collider gizmos.
    ///
    /// The four depth boxes are computed at runtime from the sprite's bounds, so the numbers sitting in
    /// BuildingData say nothing about where a box actually lands on screen. When a worker sorts wrong,
    /// this is what separates "the box is in the wrong place" from "the box is right and the rule is
    /// wrong" — two causes with opposite fixes that look identical from the outside.
    ///
    /// Gizmos must also be ON in the Game view toolbar to see them while playing.
    /// </summary>
    public static class DepthBoxGizmoToggle
    {
        private const string Path = "NuclearReMind/Tools/Show Depth Boxes";
        private const string Key = "nrm_show_depth_boxes";

        [MenuItem(Path)]
        private static void Toggle()
        {
            bool on = !EditorPrefs.GetBool(Key, false);
            EditorPrefs.SetBool(Key, on);
            Apply(on);
            Debug.Log($"[DepthBoxes] {(on ? "เปิด" : "ปิด")} — เขียว=ฐานอาคาร · เขียวเข้ม=ยอดอาคาร · " +
                      "ฟ้า=worker ยืนหน้า · ส้ม=worker ยืนหลัง  (ต้องเปิด Gizmos ใน Game view ด้วย)");
        }

        [MenuItem(Path, validate = true)]
        private static bool ToggleValidate()
        {
            Menu.SetChecked(Path, EditorPrefs.GetBool(Key, false));
            return true;
        }

        [InitializeOnLoadMethod]
        private static void Restore() => Apply(EditorPrefs.GetBool(Key, false));

        private static void Apply(bool on) => BuildingDepthSort.DrawGizmos = on;
    }
}
