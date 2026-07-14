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

        [Header("Depth Sort (แยกส่วนบน/ฐาน — แก้อาคาร iso ซ้อนผิดลำดับ · rule ใน BuildingDepthSort)")]
        [Tooltip("สัดส่วนความสูงจากล่างที่ถือเป็น 'ฐาน' (ที่เหลือ = 'ส่วนบน' ที่ยกลอยเหนืออาคารที่บัง)")]
        [Range(0.1f, 0.6f)] public float depthBaseFraction = 0.3f;

        private readonly Dictionary<Vector2Int, GameObject> _spawnedVisuals = new Dictionary<Vector2Int, GameObject>();

        // ===== Silhouette drop shadow (รูปทรงเงา = รูปสไปรต์อาคารเอง · ไม่ใช้ URP/Light2D) =====
        // ใช้สไปรต์ของตัวอาคารเองมาทำเงา: ทำดำ + พลิกลง (flip) + หุบเตี้ย → เงาทอดพาดพื้นด้านหน้า
        // เข้ากับ pivot ฐานล่างกลางของสไปรต์อาคาร (alignment 7, spritePivot y=0)
        private const float ShadowAlpha     = 0.30f; // ความเข้มเงา (สไปรต์ดำล้วน · โปร่งพอไม่ทึบ)
        private const float ShadowSquash    = 0.42f; // สัดส่วนความสูงเงาเทียบตัวจริง (พลิกลง)
        private const float MaxShadowHeight = 1.5f;   // world units — คุมไม่ให้อาคารสูง (core tower) ทอดเงายาวเกินจริง
        private const float ShadowYOffset   = -0.02f; // ทุบลงนิดให้ฐานเงาจมพื้น ไม่ลอยพ้นฐานอาคาร

        private void OnEnable()
        {
            EventManager.Instance.OnBuildingPlaced    += HandleBuildingPlaced;
            EventManager.Instance.OnBuildingRemoved   += HandleBuildingRemoved;
            EventManager.Instance.OnBuildingUpgraded  += HandleBuildingUpgraded;
            EventManager.Instance.OnSaveLoaded        += HandleSaveLoaded;
        }

        private void OnDisable()
        {
            if (EventManager.Instance == null) return;
            EventManager.Instance.OnBuildingPlaced   -= HandleBuildingPlaced;
            EventManager.Instance.OnBuildingRemoved  -= HandleBuildingRemoved;
            EventManager.Instance.OnBuildingUpgraded -= HandleBuildingUpgraded;
            EventManager.Instance.OnSaveLoaded       -= HandleSaveLoaded;
        }

        private void HandleBuildingPlaced(Cell cell, BuildingData data)
        {
            SpawnVisual(new Vector2Int(cell.col, cell.row), data);
            BuildingDepthSort.RecomputeAll(); // จัดลำดับส่วนบน/ฐานใหม่หลังมีอาคารเพิ่ม
        }

        private void HandleBuildingRemoved(Vector2Int position)
        {
            if (!_spawnedVisuals.TryGetValue(position, out var go))
                return;

            // ปิด depth-sort ก่อนทำลาย → หลุด registry ทันที ไม่ค้างใน RecomputeAll เฟรมนี้
            var depth = go != null ? go.GetComponent<BuildingDepthSort>() : null;
            if (depth != null) depth.enabled = false;

            DestroyVisual(go);
            _spawnedVisuals.Remove(position);
            BuildingDepthSort.RecomputeAll();
        }

        // อัปเกรดระดับ → สลับ sprite ตัวอาคารเป็นภาพของระดับใหม่ (L1/L2/L3) ถ้า asset มี levelSprites
        private void HandleBuildingUpgraded(Vector2Int cell, int newLevel)
        {
            if (!_spawnedVisuals.TryGetValue(cell, out var go) || go == null) return;

            var registry = BuildingRegistry.Instance;
            if (registry == null ||
                !registry.PlacedBuildings.TryGetValue(cell, out var data) || data == null)
                return;

            var sr = go.GetComponent<SpriteRenderer>(); // ตัวแม่ (Shadow เป็นลูก คนละ SpriteRenderer)
            if (sr == null) return;

            var newSprite = data.SpriteForLevel(newLevel);
            // Play mode → เล่นเอฟเฟกต์อัพเกรด (ฝุ่น+flash+เด้ง · สลับ sprite กลางฝุ่น) · edit mode/เทส → สลับทันที
            if (Application.isPlaying)
                BuildingUpgradeEffect.Play(go, sr, newSprite);
            else
                sr.sprite = newSprite;

            // เงา silhouette ต้องเปลี่ยนรูปตามสไปรต์เลเวลใหม่ด้วย (ไม่งั้นเงายังเป็นทรงเลเวลเดิม)
            var shadowTf = go.transform.Find("Shadow");
            if (shadowTf != null)
                ConfigureShadow(shadowTf.GetComponent<SpriteRenderer>(), newSprite);

            // สร้างแถบบน/ฐานใหม่ตามสไปรต์เลเวลใหม่ แล้วจัดลำดับใหม่
            go.GetComponent<BuildingDepthSort>()?.Rebuild(newSprite, depthBaseFraction);
            BuildingDepthSort.RecomputeAll();
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

            BuildingDepthSort.RecomputeAll(); // จัดลำดับครั้งเดียวหลัง spawn ครบ
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

            // sort ตาม "ช่องหน้าสุด" ของ footprint (col+row มากสุด = ใกล้ผู้ชมสุด) — iso depth มาตรฐาน
            // เดิมใช้กึ่งกลาง footprint → อาคาร 3×3 สไปรต์ใหญ่ซ้อนกันแล้วลำดับหน้า-หลังสลับ (บั๊กที่ผู้ใช้เจอ)
            int sx = Mathf.Max(1, data.size.x);
            int sy = Mathf.Max(1, data.size.y);
            // ประเภทตัดสินตอนซ้อน depth เดียวกัน: Core Tower > Building > Ore (Player=worker อยู่ WorkerView)
            var tier = data.isCoreTowerPart ? GridManager.SortTier.CoreTower
                     : data.isOreNode       ? GridManager.SortTier.Ore
                     :                         GridManager.SortTier.Building;
            int baseSort = GridManager.SortOrder(position.x + (sx - 1), position.y + (sy - 1), tier);

            // เลือก sprite ตามระดับปัจจุบัน (โหลดเซฟ/วางใหม่ = L1) — มี levelSprites จึงสลับตาม, ไม่งั้นใช้ sprite เดี่ยว
            int level = BuildingRegistry.Instance != null ? BuildingRegistry.Instance.GetLevel(position) : 1;

            var spriteRenderer = go.AddComponent<SpriteRenderer>();
            spriteRenderer.sprite = data.SpriteForLevel(level);
            spriteRenderer.sortingLayerName = BuildingsSortingLayer;
            spriteRenderer.sortingOrder = baseSort;

            // Collider2D + click target ตามรูปสไปรต์ → แผงคลิกโดนตัวอาคารจริง (คลิกตัวสูง ๆ ก็เปิด ไม่ต้องเล็งฐาน footprint)
            AddClickTarget(go, spriteRenderer, position, data);

            // อนิเมชัน idle (ถ้า asset มีเฟรม ≥ 2) — สลับ sprite วนลูป ไม่ใช้ Animator (ดูเหตุผลใน SpriteFrameAnimator)
            if (data.animationFrames != null && data.animationFrames.Length >= 2)
                go.AddComponent<SpriteFrameAnimator>().Play(data.animationFrames, data.animationFps);

            AddShadow(go, spriteRenderer.sprite, baseSort);

            // แยก "ส่วนบน/ส่วนฐาน" + collider 2 โซน (depth-sort · main sprite เต็มใบไม่ถูกแตะ)
            go.AddComponent<BuildingDepthSort>().Setup(spriteRenderer, baseSort, BuildingsSortingLayer, depthBaseFraction, data);

            _spawnedVisuals[position] = go;
        }

        // Collider2D (trigger) ครอบรูปสไปรต์ + BuildingClickTarget → ให้แผง (CoreTower/Lab/Memorial) raycast โดนตัวอาคารจริง
        // isTrigger = ไม่บล็อก worker/placement (query ด้วย Physics2D.OverlapPoint ได้อย่างเดียว) · sr.sprite.bounds = พิกัด local ตาม pivot (ฐานล่างกลาง)
        private static void AddClickTarget(GameObject go, SpriteRenderer sr, Vector2Int position, BuildingData data)
        {
            if (sr == null || sr.sprite == null) return;
            var box = go.AddComponent<BoxCollider2D>();
            box.isTrigger = true;
            box.size = (Vector2)sr.sprite.bounds.size;
            box.offset = (Vector2)sr.sprite.bounds.center;
            var target = go.AddComponent<BuildingClickTarget>();
            target.originCell = position;
            target.data = data;
        }

        // เงา silhouette = รูปสไปรต์อาคารเอง — child แยกจาก SpriteRenderer ตัวแม่
        private void AddShadow(GameObject parent, Sprite sprite, int baseSort)
        {
            if (sprite == null) return;
            var shadow = new GameObject("Shadow");
            shadow.transform.SetParent(parent.transform, false);

            var sr = shadow.AddComponent<SpriteRenderer>();
            sr.sortingLayerName = BuildingsSortingLayer;
            sr.sortingOrder = baseSort - 1; // ใต้ตัวอาคาร เหนือพื้น (front building ที่ order สูงกว่าบังเงาได้ตามธรรมชาติ)
            ConfigureShadow(sr, sprite);
        }

        // ทำสไปรต์ให้เป็น "เงาทอดพื้น": ดำล้วน + พลิกลง (scaleY ลบ) + หุบเตี้ย
        // pivot ฐานล่างกลาง → ฐานเงาติดฐานอาคาร แล้วทอดลงด้านหน้า (ล่างจอ = ใกล้ผู้ชม) เหมือนแสงส่องจากบน
        // คุมความสูงสูงสุดด้วย MaxShadowHeight เพื่อไม่ให้อาคารสูง (core tower) ทอดเงายาวเวอร์
        private static void ConfigureShadow(SpriteRenderer sr, Sprite sprite)
        {
            if (sr == null || sprite == null) return;
            sr.sprite = sprite;
            sr.color = new Color(0f, 0f, 0f, ShadowAlpha);

            float spriteH = sprite.bounds.size.y; // world units (คิด pivot+ppu แล้ว · ยังไม่คูณ transform scale)
            float squash = spriteH > 0.001f ? Mathf.Min(ShadowSquash, MaxShadowHeight / spriteH) : ShadowSquash;
            sr.transform.localPosition = new Vector3(0f, ShadowYOffset, 0f);
            sr.transform.localScale    = new Vector3(1f, -squash, 1f); // ลบ = พลิกลง · หุบตาม squash
        }
    }
}
