using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using NuclearReMind;

/// <summary>
/// NuclearReMind/Setup Demolition System
/// เพิ่ม DemolitionController + highlight object ในฉาก
/// และเพิ่ม Demolish button ใน BuildingSelectionPanel
/// </summary>
public class DemolitionSetup : Editor
{
    [MenuItem("NuclearReMind/Setup Demolition System")]
    private static void Setup()
    {
        var demolitionGO = SetupDemolitionController();
        SetupHighlight(demolitionGO);
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Debug.Log("[DemolitionSetup] เสร็จ — กด Play แล้วกดปุ่ม 🔨 ในแถบอาคารเพื่อทดสอบ");
    }

    // ─────────────────────────────────────────
    //  DemolitionController
    // ─────────────────────────────────────────

    private static GameObject SetupDemolitionController()
    {
        // ถ้ามีอยู่แล้วให้ใช้ตัวเดิม
        var existing = Object.FindFirstObjectByType<DemolitionController>();
        if (existing != null)
        {
            Debug.Log("[DemolitionSetup] DemolitionController มีอยู่แล้ว");
            return existing.gameObject;
        }

        var go = new GameObject("DemolitionController");
        go.AddComponent<DemolitionController>();
        EditorUtility.SetDirty(go);
        Debug.Log("[DemolitionSetup] สร้าง DemolitionController");
        return go;
    }

    // ─────────────────────────────────────────
    //  Red highlight overlay
    // ─────────────────────────────────────────

    private static void SetupHighlight(GameObject demolitionGO)
    {
        var controller = demolitionGO.GetComponent<DemolitionController>();

        // สร้าง child highlight
        var highlight = new GameObject("DemolishHighlight");
        highlight.transform.SetParent(demolitionGO.transform, false);
        highlight.SetActive(false);

        var sr = highlight.AddComponent<SpriteRenderer>();

        // สร้าง 1×1 white sprite สำหรับ highlight
        var tex = new Texture2D(2, 1);
        tex.SetPixels(new[] { Color.white, Color.white });
        tex.Apply();
        var sprite = Sprite.Create(tex, new Rect(0, 0, 2, 1), new Vector2(0.5f, 0.5f), 2f);
        sr.sprite = sprite;
        sr.color  = new Color(1f, 0.2f, 0.2f, 0.55f);

        // วางอยู่เหนือ grid tiles
        sr.sortingLayerName = "Buildings";
        sr.sortingOrder     = 50;

        // scale ให้พอดีกับ tile (tileWidth=1, tileHeight=0.5 → isometric diamond ~1×0.5)
        highlight.transform.localScale = new Vector3(1f, 0.5f, 1f);

        controller.highlightRenderer = sr;
        EditorUtility.SetDirty(demolitionGO);
        Debug.Log("[DemolitionSetup] สร้าง DemolishHighlight");

        // บันทึก prefab (ถ้าต้องการ) — ไม่จำเป็นสำหรับ runtime object นี้
    }
}
