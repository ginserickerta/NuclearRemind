using System.IO;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace NuclearReMind.EditorTools
{
    /// <summary>
    /// แถบปุ่มด้านข้าง HUD (ซ้ายล่าง) — 4 ปุ่มสไปรต์: ไอเทม(I) / คีย์ลัด(F1) / Codex(C) / บันทึก
    ///   • นำเข้าสไปรต์ Assets/Sprites/UI/side_*.png (icon + ข้อความ baked)
    ///   • ลบปุ่มเรียบเดิม (CodexToggleButton, HotkeyHelpButton) — แทนด้วยสไปรต์ในแถบนี้
    ///   • ผูก onClick → HudMenuButtons (คอมโพเนนต์ในซีน → persistent listener ติดถาวร)
    ///     ไอเทม/บันทึก = ปุ่มใหม่ (เดิมมีแต่คีย์ I/F5) · คีย์ลัด/Codex = ย้ายจากปุ่มเรียบมาสไปรต์
    ///
    /// ต้องรันหลัง Setup HUD Canvas + Setup Codex System (ปุ่มเรียบถูกสร้างที่นั่น — ที่นี่ลบทิ้งแล้วแทน)
    /// รัน: เมนู NuclearReMind/Setup Side Menu Buttons — หรือรวมใน Run All Setups
    /// </summary>
    public static class SideMenuSetup
    {
        private const string ScenePath = "Assets/Scenes/Gamescene.unity";
        private const string UIFolder = "Assets/Sprites/UI";

        [MenuItem("NuclearReMind/Setup Side Menu Buttons")]
        public static void Apply()
        {
            if (EditorSceneManager.GetActiveScene().path != ScenePath)
                EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            ImportSprites();

            var hud = GameObject.Find("HUDCanvas");
            if (hud == null)
            {
                Debug.LogWarning("[SideMenuSetup] ไม่พบ HUDCanvas — รัน Setup HUD Canvas ก่อน");
                return;
            }

            // ลบปุ่มเรียบเดิม + แถบเก่า (รันซ้ำได้)
            DestroyIfExists(hud, "CodexToggleButton");
            DestroyIfExists(hud, "HotkeyHelpButton");
            var oldBar = hud.transform.Find("SideMenuBar");
            if (oldBar != null) Object.DestroyImmediate(oldBar.gameObject);

            var bar = new GameObject("SideMenuBar", typeof(RectTransform));
            bar.transform.SetParent(hud.transform, false);
            var barRect = bar.GetComponent<RectTransform>();
            barRect.anchorMin = barRect.anchorMax = Vector2.zero;
            barRect.pivot = Vector2.zero;
            barRect.anchoredPosition = Vector2.zero;
            barRect.sizeDelta = Vector2.zero;
            var hudMenu = bar.AddComponent<HudMenuButtons>();

            // ตำแหน่ง/ขนาด = ที่ผู้ใช้จัดเอง (อ่านจากซีน Gamescene 13 ก.ค. · multi-select ย่อด้วย Rect tool)
            // anchored ซ้ายล่าง (x,y) + sizeDelta (w,h) · ปุ่มถูกย่อไม่ล็อกสัดส่วน (preserveAspect ยังจัดสไปรต์กลางในกล่อง)
            // เรียงจากล่างขึ้นบน: บันทึก → Codex → คีย์ลัด → ไอเทม (ไอเทมบนสุด)
            var save      = MakeSpriteButton(bar.transform, "SideBtn_Save",      "side_save",      new Vector2(19f, 98.64f),  new Vector2(107.15f, 75.07f));
            var codex     = MakeSpriteButton(bar.transform, "SideBtn_Codex",     "side_codex",     new Vector2(19f, 177.41f), new Vector2(107.15f, 82.20f));
            var hotkey    = MakeSpriteButton(bar.transform, "SideBtn_Hotkey",    "side_hotkey",    new Vector2(19f, 263.30f), new Vector2(107.15f, 77.19f));
            var inventory = MakeSpriteButton(bar.transform, "SideBtn_Inventory", "side_inventory", new Vector2(19f, 344.19f), new Vector2(107.15f, 102.77f));

            if (inventory != null) UnityEventTools.AddPersistentListener(inventory.onClick, new UnityAction(hudMenu.ToggleInventory));
            if (hotkey != null) UnityEventTools.AddPersistentListener(hotkey.onClick, new UnityAction(hudMenu.ToggleHotkeyHelp));
            if (codex != null) UnityEventTools.AddPersistentListener(codex.onClick, new UnityAction(hudMenu.ToggleCodex));
            if (save != null) UnityEventTools.AddPersistentListener(save.onClick, new UnityAction(hudMenu.RequestSave));

            EditorUtility.SetDirty(bar);
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
            Debug.Log("[SideMenuSetup] ✅ แถบปุ่มด้านข้าง 4 ปุ่ม (ไอเทม/คีย์ลัด/Codex/บันทึก) พร้อม — กด Save Scene แล้ว");
        }

        // สร้างปุ่มสไปรต์ anchored ซ้ายล่าง · ระบุ pos + size ตรงๆ (ค่าที่ผู้ใช้จัดเอง)
        private static Button MakeSpriteButton(Transform parent, string name, string spriteName,
            Vector2 anchoredPos, Vector2 size)
        {
            var sprite = LoadUISprite(spriteName);

            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = Vector2.zero;
            rect.pivot = Vector2.zero;
            rect.anchoredPosition = anchoredPos;
            rect.sizeDelta = size;

            var img = go.AddComponent<Image>();
            img.sprite = sprite;
            img.preserveAspect = true;
            img.color = Color.white;
            if (sprite == null)
            {
                Debug.LogWarning($"[SideMenuSetup] ไม่พบสไปรต์ {spriteName} — ปุ่มจะว่าง (รัน Setup ซ้ำหลัง Unity import)");
                img.color = new Color(0.15f, 0.14f, 0.11f, 1f);
            }

            var btn = go.AddComponent<Button>();
            btn.targetGraphic = img;
            btn.transition = Selectable.Transition.ColorTint;
            var cb = btn.colors;
            cb.normalColor = Color.white;
            cb.highlightedColor = new Color(0.86f, 0.86f, 0.86f, 1f);
            cb.pressedColor = new Color(0.70f, 0.70f, 0.70f, 1f);
            cb.selectedColor = Color.white;
            cb.fadeDuration = 0.08f;
            btn.colors = cb;

            return btn;
        }

        private static Sprite LoadUISprite(string name)
            => AssetDatabase.LoadAssetAtPath<Sprite>($"{UIFolder}/{name}.png");

        private static void DestroyIfExists(GameObject parent, string childName)
        {
            var t = parent.transform.Find(childName);
            if (t != null) Object.DestroyImmediate(t.gameObject);
        }

        // นำเข้า side_*.png เป็น Sprite (point filter — pixel art · ย่อ 512 ประหยัดหน่วยความจำ)
        private static void ImportSprites()
        {
            foreach (var n in new[] { "side_inventory", "side_hotkey", "side_codex", "side_save" })
            {
                string path = $"{UIFolder}/{n}.png";
                if (!File.Exists(path)) { Debug.LogWarning($"[SideMenuSetup] ไม่พบไฟล์ {path}"); continue; }
                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer == null) continue;
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.filterMode = FilterMode.Point;
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = false;
                importer.maxTextureSize = 512;
                importer.SaveAndReimport();
            }
        }
    }
}
