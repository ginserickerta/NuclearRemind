using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using NuclearReMind;

namespace NuclearReMind.EditorTools
{
    /// <summary>
    /// สร้าง "prefab จริง" ของช่อง hotbar + ปุ่มทุบ (BuildingSelectionUI) ไว้แก้ด้วยมือใน Prefab Mode
    ///   ต่างจาก Bake …Template เดิม (สร้างเป็น object ในซีน inactive — แก้ยาก/สับสน) → อันนี้เป็นไฟล์ .prefab แท้
    ///
    /// เวิร์กโฟลว์:
    ///   1) รันเมนูนี้ครั้งเดียว → ได้ Assets/Prefabs/UI/HotbarSlot.prefab (+ HotbarDemolishButton.prefab) และ
    ///      wire ให้ BuildingSelectionUI.slotTemplate / demolishTemplate อัตโนมัติ
    ///   2) ดับเบิลคลิก prefab → เข้า Prefab Mode → ลาก/ปรับ ตำแหน่ง·ขนาด·ฟอนต์·สี ของ
    ///      Icon / NameLabel / CostLabel / KeyLabel / LockLabel (ช่อง) · Icon / IconEmoji / Label / SubLabel (ปุ่มทุบ)
    ///   3) Play — runtime clone prefab แล้วเติมแค่ icon/ชื่อ/ราคา/คีย์/lock (ไม่แตะ layout/สี/ฟอนต์) → งานที่แก้อยู่ครบ
    ///
    /// ⚠ ห้ามเปลี่ยน "ชื่อลูก" (Icon/NameLabel/CostLabel/KeyLabel/LockLabel …) — PopulateSlot หาไอเทมด้วยชื่อ
    /// ♻ รันซ้ำ: ถ้า prefab มีอยู่แล้วจะ "ถามก่อน" — เลือกผูก field เฉย ๆ (ไม่ทับ) ได้ กันงานที่แก้มือหาย
    /// </summary>
    public static class HotbarPrefabBaker
    {
        const string Dir = "Assets/Prefabs/UI";
        const string SlotPath = Dir + "/HotbarSlot.prefab";
        const string DemolishPath = Dir + "/HotbarDemolishButton.prefab";

        [MenuItem("NuclearReMind/UI/Bake Hotbar Slot Prefab")]
        public static void BakeSlot()
        {
            var sel = GetSel("Bake Hotbar Slot Prefab");
            if (sel == null) return;

            var prefab = BakeOrReuse(
                SlotPath, "HotbarSlot",
                p => sel.BuildSlotStructure(p, "HotbarSlot"),
                sel, existing: sel.slotTemplate);
            if (prefab == null) return;

            WireField(sel, () => sel.slotTemplate = prefab);
            RemoveSceneLeftover(sel, "HotbarSlotTemplate");
            Finish(sel, prefab,
                "[HotbarPrefabBaker] ✅ HotbarSlot.prefab + wire slotTemplate — ดับเบิลคลิก prefab แก้ layout · Ctrl+S · Play เห็นผล");
        }

        [MenuItem("NuclearReMind/UI/Bake Demolish Button Prefab")]
        public static void BakeDemolish()
        {
            var sel = GetSel("Bake Demolish Button Prefab");
            if (sel == null) return;

            var prefab = BakeOrReuse(
                DemolishPath, "HotbarDemolishButton",
                p => sel.BuildDemolishStructure(p, "HotbarDemolishButton"),
                sel, existing: sel.demolishTemplate);
            if (prefab == null) return;

            WireField(sel, () => sel.demolishTemplate = prefab);
            RemoveSceneLeftover(sel, "DemolishButtonTemplate");
            Finish(sel, prefab,
                "[HotbarPrefabBaker] ✅ HotbarDemolishButton.prefab + wire demolishTemplate — ดับเบิลคลิก prefab แก้ layout · Ctrl+S · Play เห็นผล");
        }

        [MenuItem("NuclearReMind/UI/Bake Hotbar Prefabs (slot + demolish)")]
        public static void BakeBoth() { BakeSlot(); BakeDemolish(); }

        // ─────────────────────────────────────────

        static BuildingSelectionUI GetSel(string title)
        {
            var sel = Object.FindFirstObjectByType<BuildingSelectionUI>();
            if (sel == null)
                EditorUtility.DisplayDialog(title, "ไม่พบ BuildingSelectionUI ในซีน — เปิด Gamescene.unity ก่อน", "OK");
            return sel;
        }

        /// <summary>
        /// คืน prefab ที่พร้อม wire: ถ้ายังไม่มีไฟล์ = สร้างใหม่ · ถ้ามีแล้ว = ถามก่อน (ผูกเฉย ๆ / ทับ / ยกเลิก)
        /// null = ผู้ใช้ยกเลิก
        /// </summary>
        static GameObject BakeOrReuse(string path, string niceName,
            System.Func<Transform, GameObject> buildStructure, BuildingSelectionUI sel, GameObject existing)
        {
            var current = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (current != null)
            {
                int choice = EditorUtility.DisplayDialogComplex(
                    "Bake " + niceName,
                    "มี " + niceName + ".prefab อยู่แล้ว\nจะทำอย่างไร?",
                    "แค่ผูก field (ไม่ทับ)",   // 0
                    "ยกเลิก",                  // 1
                    "สร้างใหม่ทับ (งานที่แก้หาย)"); // 2
                if (choice == 1) return null;
                if (choice == 0) return current; // ผูกของเดิม ไม่แตะเนื้อ prefab
                // choice == 2 → สร้างใหม่ทับด้านล่าง
            }

            EnsureFolders();

            // ต้องมี parent ที่เป็น Canvas เพื่อให้ RectTransform/UGUI ตั้งค่าถูก ก่อน save เป็น prefab แล้วลบทิ้ง
            var canvas = sel.buttonContainer != null
                ? sel.buttonContainer.GetComponentInParent<Canvas>()
                : sel.GetComponentInParent<Canvas>();
            Transform parent = canvas != null ? canvas.transform : sel.transform;

            var temp = buildStructure(parent);
            temp.SetActive(true);
            var prefab = PrefabUtility.SaveAsPrefabAsset(temp, path);
            Object.DestroyImmediate(temp);

            if (prefab == null)
                EditorUtility.DisplayDialog("Bake " + niceName, "สร้าง prefab ไม่สำเร็จ:\n" + path, "OK");
            return prefab;
        }

        static void EnsureFolders()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Prefabs"))
                AssetDatabase.CreateFolder("Assets", "Prefabs");
            if (!AssetDatabase.IsValidFolder(Dir))
                AssetDatabase.CreateFolder("Assets/Prefabs", "UI");
        }

        static void WireField(BuildingSelectionUI sel, System.Action assign)
        {
            Undo.RecordObject(sel, "wire hotbar prefab");
            assign();
            EditorUtility.SetDirty(sel);
        }

        // ลบ object template เก่าในซีน (จาก Bake …Template เดิม) กันซ้ำซ้อน/สับสน
        static void RemoveSceneLeftover(BuildingSelectionUI sel, string sceneObjName)
        {
            var canvas = sel.GetComponentInParent<Canvas>();
            Transform root = canvas != null ? canvas.transform : sel.transform;
            var old = root.Find(sceneObjName);
            if (old != null) Undo.DestroyObjectImmediate(old.gameObject);
        }

        static void Finish(BuildingSelectionUI sel, GameObject prefab, string log)
        {
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Selection.activeObject = prefab;
            EditorGUIUtility.PingObject(prefab);
            Debug.Log(log);
        }
    }
}
