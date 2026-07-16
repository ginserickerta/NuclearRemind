using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using NuclearReMind;

namespace NuclearReMind.EditorTools
{
    /// <summary>
    /// สร้าง "prefab แผงเลือกอาคารทั้งแถบ" (BuildingSelectionPanel) ไว้แก้ทั้งชุดด้วยตาใน Prefab Mode
    ///   ต่างจาก Bake Hotbar Slot Prefab (ได้แค่ 1 ช่องต้นแบบ → runtime clone) — อันนี้ได้ "ทั้งแผง" จริง:
    ///   พื้นหลัง/กรอบ + ทุกช่องอาคาร (Slot_1..N) + ปุ่มทุบ (Slot_Demolish) วางเรียงพร้อม เห็นไอคอน/ชื่อ/ราคาจริง
    ///
    /// เวิร์กโฟลว์:
    ///   1) รันเมนูนี้ครั้งเดียว → Assets/Prefabs/UI/BuildingSelectionPanel.prefab (แผง BuildingSelectionPanel
    ///      ในซีนกลายเป็น instance ของ prefab นี้ · ref ของ BuildingSelectionUI ยังชี้ถูก)
    ///   2) ดับเบิลคลิก prefab → เข้า Prefab Mode → จัด spacing/กรอบ/พื้นหลัง + ตำแหน่ง·ขนาด·สี·ฟอนต์ของทุกช่องได้เลยทั้งแผง
    ///   3) Play — BuildingSelectionUI เห็นว่ามี Slot_1 อยู่แล้ว → "เติมข้อมูล" ลงช่องเดิม ไม่สร้างใหม่ (layout ที่แก้ครบ)
    ///
    /// ⚠ ห้ามเปลี่ยนชื่อ Slot_1..N / Slot_Demolish และชื่อลูกในช่อง (Icon/NameLabel/CostLabel/KeyLabel/LockLabel)
    ///    — runtime หาไอเทมด้วยชื่อ · ถ้าจำนวนอาคาร (buildingHotbar) เปลี่ยน ให้ bake ใหม่
    /// ♻ ไม่รวมใน Run All Setups (กันทับงานที่แก้มือใน prefab) — รันเมนูนี้เองเมื่ออยาก re-bake
    /// </summary>
    public static class BuildingSelectionPanelBaker
    {
        private const string ScenePath = "Assets/Scenes/Gamescene.unity";
        private const string Dir  = "Assets/Prefabs/UI";
        private const string Path = Dir + "/BuildingSelectionPanel.prefab";

        [MenuItem("NuclearReMind/UI/Bake BuildingSelectionPanel Prefab")]
        public static void Bake()
        {
            if (EditorSceneManager.GetActiveScene().path != ScenePath)
                EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            var sel = Object.FindFirstObjectByType<BuildingSelectionUI>();
            if (sel == null)
            {
                EditorUtility.DisplayDialog("Bake BuildingSelectionPanel",
                    "ไม่พบ BuildingSelectionUI ในซีน — เปิด Gamescene.unity แล้วรัน Setup ที่สร้าง hotbar ก่อน", "OK");
                return;
            }
            if (sel.panelRoot == null || sel.buttonContainer == null)
            {
                EditorUtility.DisplayDialog("Bake BuildingSelectionPanel",
                    "BuildingSelectionUI ยังไม่มี panelRoot/buttonContainer — รัน Setup Day 11 / HUD ก่อน", "OK");
                return;
            }

            // ถามก่อนถ้ามี prefab เดิม (กันงานที่แก้มือใน Prefab Mode หาย)
            if (AssetDatabase.LoadAssetAtPath<GameObject>(Path) != null)
            {
                int choice = EditorUtility.DisplayDialogComplex("Bake BuildingSelectionPanel",
                    "มี BuildingSelectionPanel.prefab อยู่แล้ว\nสร้างใหม่ทับจะทำให้ที่แก้มือใน prefab หาย",
                    "สร้างใหม่ทับ", "ยกเลิก", "");
                if (choice != 0) return;
            }

            EnsureFolders();

            // สร้างทุกช่อง (อาคาร + ปุ่มทุบ) ลง buttonContainer พร้อมไอคอน/ชื่อ/ราคาจริง — ให้เห็นของจริงตอนแก้ prefab
            sel.BakePopulateAllSlots();

            // เซฟ "ทั้งแผง" (panelRoot) เป็น prefab แล้วผูกแผงในซีนเป็น instance ของ prefab นี้
            //   → BuildingSelectionUI.panelRoot / buttonContainer ยังชี้ GameObject เดิม (ตอนนี้เป็น instance) ref ไม่หลุด
            var prefab = PrefabUtility.SaveAsPrefabAssetAndConnect(
                sel.panelRoot, Path, InteractionMode.AutomatedAction);

            if (prefab == null)
            {
                EditorUtility.DisplayDialog("Bake BuildingSelectionPanel", "สร้าง prefab ไม่สำเร็จ:\n" + Path, "OK");
                return;
            }

            EditorUtility.SetDirty(sel);
            var scene = EditorSceneManager.GetActiveScene();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            Selection.activeObject = prefab;
            EditorGUIUtility.PingObject(prefab);
            Debug.Log("[BuildingSelectionPanelBaker] ✅ BuildingSelectionPanel.prefab (ทั้งแผง) พร้อมแล้ว — " +
                      "ดับเบิลคลิก prefab เข้า Prefab Mode แก้ทั้งชุดได้เลย · Play จะเติมข้อมูลลงช่องเดิม (ไม่รื้อ layout)");
        }

        private static void EnsureFolders()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Prefabs"))
                AssetDatabase.CreateFolder("Assets", "Prefabs");
            if (!AssetDatabase.IsValidFolder(Dir))
                AssetDatabase.CreateFolder("Assets/Prefabs", "UI");
        }
    }
}
