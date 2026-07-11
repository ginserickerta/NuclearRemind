using System.Collections.Generic;
using UnityEngine;

namespace NuclearReMind
{
    /// <summary>
    /// สร้าง/ลบ SpriteRenderer ของอาคารที่วางแล้วใน scene ตาม OnBuildingPlaced/OnBuildingRemoved
    /// เพื่อให้ตัวอาคารยังคงปรากฏอยู่บน grid หลังจากวางเสร็จ (ghost จะถูกซ่อนไปแล้ว)
    /// </summary>
    public class BuildingVisualSpawner : MonoBehaviour
    {
        private const string BuildingsSortingLayer = "Buildings";

        [Header("Parent transform สำหรับอาคารที่วางแล้ว (Buildings sorting layer)")]
        public Transform buildingsParent;

        private readonly Dictionary<Vector2Int, GameObject> _spawnedVisuals = new Dictionary<Vector2Int, GameObject>();

        // ===== Drop shadow (เพิ่มมิติบน light theme — ไม่ใช้ URP/Light2D) =====
        private const float ShadowWidth   = 1.0f;   // กว้างเงาเทียบ 1 tile
        private const float ShadowAlpha   = 0.38f;  // ความเข้มเงา (คูณกับ gradient ใน sprite) — เข้มพอให้เห็นบนพื้นสว่าง
        private const float ShadowYOffset = -0.18f; // เลื่อนลงไปที่ฐานอาคาร ให้เงาโผล่พ้นตัวอาคาร
        private static Sprite _shadowSprite;

        private void OnEnable()
        {
            EventManager.Instance.OnBuildingPlaced    += HandleBuildingPlaced;
            EventManager.Instance.OnBuildingRemoved   += HandleBuildingRemoved;
            EventManager.Instance.OnSaveLoaded        += HandleSaveLoaded;
        }

        private void OnDisable()
        {
            if (EventManager.Instance == null) return;
            EventManager.Instance.OnBuildingPlaced   -= HandleBuildingPlaced;
            EventManager.Instance.OnBuildingRemoved  -= HandleBuildingRemoved;
            EventManager.Instance.OnSaveLoaded       -= HandleSaveLoaded;
        }

        private void HandleBuildingPlaced(Cell cell, BuildingData data)
        {
            SpawnVisual(new Vector2Int(cell.col, cell.row), data);
        }

        private void HandleBuildingRemoved(Vector2Int position)
        {
            if (!_spawnedVisuals.TryGetValue(position, out var go))
                return;

            DestroyVisual(go);
            _spawnedVisuals.Remove(position);
        }

        /// <summary>
        /// ลบ visual ทั้งหมดแล้ว spawn ใหม่ตาม placedBuildings ใน save
        /// (อ่าน BuildingData จาก BuildingRegistry.GetBuildingDataByName ซึ่งเป็น read-only lookup)
        /// </summary>
        private void HandleSaveLoaded(SaveData save)
        {
            foreach (var go in _spawnedVisuals.Values)
                DestroyVisual(go);
            _spawnedVisuals.Clear();

            if (save.placedBuildings == null || save.buildingTypes == null)
                return;

            for (int i = 0; i < save.placedBuildings.Count; i++)
            {
                BuildingData data = BuildingRegistry.Instance.GetBuildingDataByName(save.buildingTypes[i]);
                if (data != null)
                    SpawnVisual(save.placedBuildings[i], data);
            }
        }

        /// <summary>
        /// ลบ visual GameObject — ใช้ DestroyImmediate นอก Play mode (เช่นใน EditMode tests)
        /// เพราะ Destroy() ใช้ได้เฉพาะ Play mode
        /// </summary>
        private void DestroyVisual(GameObject go)
        {
            if (Application.isPlaying)
                Destroy(go);
            else
                DestroyImmediate(go);
        }

        private void SpawnVisual(Vector2Int position, BuildingData data)
        {
            var go = new GameObject($"Building_{data.buildingName}_{position.x}_{position.y}");
            go.transform.SetParent(buildingsParent, false);
            // anchor ที่กึ่งกลาง footprint → sprite ฐานล่างกลางนั่งตรงช่อง (อาคาร multi-tile ไม่เยื้อง)
            // + spriteOffset ต่อหลัง (ปรับใน BuildingData Inspector) เลื่อนภาพเทียบกึ่งกลางช่อง
            go.transform.position = GridManager.Instance.FootprintCenterWorld(position, data.size)
                                  + (Vector3)data.spriteOffset;
            // ปรับขนาดภาพต่อหลัง (ไม่กระทบ footprint/การวาง) — เงาย่อ/ขยายตามด้วย (เป็นลูก)
            float visScale = data.spriteScale > 0f ? data.spriteScale : 1f;
            go.transform.localScale = new Vector3(visScale, visScale, 1f);

            // sort ตามฐานอาคาร = กึ่งกลาง footprint (ให้ตรงกับ anchor ที่ย้ายมากึ่งกลางแล้ว)
            // ไม่งั้นอาคาร multi-tile จะ sort ที่มุมหลัง → วาดทับกันผิด
            int sx = Mathf.Max(1, data.size.x);
            int sy = Mathf.Max(1, data.size.y);
            int baseSort = GridManager.SortOrder(position.x + (sx - 1) * 0.5f, position.y + (sy - 1) * 0.5f);

            var spriteRenderer = go.AddComponent<SpriteRenderer>();
            spriteRenderer.sprite = data.sprite;
            spriteRenderer.sortingLayerName = BuildingsSortingLayer;
            spriteRenderer.sortingOrder = baseSort;

            // อนิเมชัน idle (ถ้า asset มีเฟรม ≥ 2) — สลับ sprite วนลูป ไม่ใช้ Animator (ดูเหตุผลใน SpriteFrameAnimator)
            if (data.animationFrames != null && data.animationFrames.Length >= 2)
                go.AddComponent<SpriteFrameAnimator>().Play(data.animationFrames, data.animationFps);

            AddShadow(go, position, data, baseSort);

            _spawnedVisuals[position] = go;
        }

        // เงา ellipse นุ่ม ๆ ใต้อาคาร — child แยกจาก SpriteRenderer ตัวแม่
        private void AddShadow(GameObject parent, Vector2Int position, BuildingData data, int baseSort)
        {
            var shadow = new GameObject("Shadow");
            shadow.transform.SetParent(parent.transform, false);
            shadow.transform.localPosition = new Vector3(0f, ShadowYOffset, 0f);

            // ขยายเงาตาม footprint อาคาร (size = จำนวน tile กว้าง×ลึก)
            int footprint = Mathf.Max(1, data.size.x) + Mathf.Max(1, data.size.y);
            float scale = ShadowWidth * footprint * 0.5f;
            shadow.transform.localScale = new Vector3(scale, scale, 1f);

            var sr = shadow.AddComponent<SpriteRenderer>();
            sr.sprite = GetShadowSprite();
            sr.color = new Color(0f, 0f, 0f, ShadowAlpha);
            sr.sortingLayerName = BuildingsSortingLayer;
            sr.sortingOrder = baseSort - 1; // ใต้ตัวอาคาร เหนือพื้น
        }

        // sprite เงา: ellipse 2:1 ที่ alpha ไล่จากกลาง (1) ออกขอบ (0) — สร้างครั้งเดียว cache ไว้
        private static Sprite GetShadowSprite()
        {
            if (_shadowSprite != null) return _shadowSprite;

            const int w = 128, h = 64;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var px = new Color[w * h];
            float cx = w * 0.5f, cy = h * 0.5f;

            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    float dx = (x - cx) / cx;
                    float dy = (y - cy) / cy;
                    float d = Mathf.Sqrt(dx * dx + dy * dy); // 0 กลาง → 1 ขอบ
                    float a = Mathf.Clamp01(1f - d);
                    px[y * w + x] = new Color(0f, 0f, 0f, a * a); // soft falloff
                }
            }

            tex.SetPixels(px);
            tex.Apply();
            _shadowSprite = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), w);
            return _shadowSprite;
        }
    }
}
