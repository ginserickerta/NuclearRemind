using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;

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
        //
        // ★ ปรับสดได้จาก Inspector: ลาก slider ตอน Play แล้วเงาทุกหลังขยับทันที (OnValidate → RefreshAllShadows)
        //   ได้ค่าที่ชอบแล้ว: คลิกขวาหัวคอมโพเนนต์ → Copy Component → หยุด Play → Paste Component Values
        //   (ค่าที่ปรับตอน Play หายเมื่อออกจาก Play เหมือน component อื่นทุกตัว)
        [Header("เงาอาคาร (ปรับสดตอน Play เห็นผลทันที)")]
        public bool enableShadows = true;
        [Tooltip("ความเข้มเงา — 0 = มองไม่เห็น · 1 = ดำทึบ")]
        [Range(0f, 1f)] public float shadowAlpha = 0.30f;
        // ★ 0.42 เดิมทำให้อาคารสูงดู "ลอย" (เงายาวตามความสูงสไปรต์ จนหลุดจากฐาน)
        //   ค่ากลางใหม่สั้นลงมาก · ค่าที่เหมาะจริงเป็นรายหลัง ตั้งด้วยเมนู Setup Building Shadows (grounded)
        [Tooltip("ความยาวเงา เทียบความสูงอาคาร (สั้น = ดูติดพื้น · ยาว = ดูลอย)")]
        [Range(0.05f, 1.2f)] public float shadowSquash = 0.18f;
        [Tooltip("เพดานความยาวเงา (world units) — กันอาคารสูงอย่าง CORE TOWER ทอดเงายาวเวอร์")]
        [Range(0.2f, 6f)] public float shadowMaxHeight = 1.5f;
        [Tooltip("เลื่อนตำแหน่งเงา: X = ซ้าย/ขวา · Y = ขึ้น/ลง (ค่าบวกเล็กน้อย = ดันหัวเงาซ้อนใต้ฐาน ปิดรอยต่อ)")]
        public Vector2 shadowOffset = new Vector2(0f, 0.03f);
        [Tooltip("เอียงเงาตามทิศแสง (องศา) — ลบ = เงาเอียงไปทางขวา · บวก = ไปทางซ้าย · ควรใช้ค่าเดียวทั้งเมือง")]
        [Range(-70f, 70f)] public float shadowLeanDegrees = -15f;

        // ===== Window ember glow (RENDER_PLAN §5.4 — warm light per building at dusk) =====
        // Runtime-created Light2D can't set its target sorting layers (no public API) —
        // clone the scene's "Reactor Glow Light 2D" instead: its layer list (patched by
        // LightingSetup to cover all layers) and additive blend style copy with it.
        [Header("Window Glow (ไฟอุ่นประจำอาคาร — ปรับได้ · ปิดด้วย enableWindowGlow)")]
        public bool enableWindowGlow = true;
        public Color windowGlowColor = new Color32(0xFF, 0xB2, 0x4D, 0xFF); // warm ember
        [Range(0f, 3f)] public float windowGlowIntensity = 1.0f;             // additive on a dark grade — below ~0.7 it barely reads
        [Range(0.1f, 1.5f)] public float windowGlowRadiusScale = 0.9f;       // × sprite height
        private const string GlowTemplateName = "Reactor Glow Light 2D";
        private GameObject _glowTemplate;    // cached (no Find in hot paths)
        private bool _warnedNoTemplate;      // log the failure once, not per building
        private bool _testLightSpawned;      // TEMP diagnostic — one giant test light per play

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
                ConfigureShadow(shadowTf.GetComponent<SpriteRenderer>(), newSprite, data);

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

            AddShadow(go, spriteRenderer.sprite, baseSort, data);
            AddWindowGlow(go, spriteRenderer.sprite, data);

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

        // Warm ember light per building — makes windows read as "lit" against the dark grade.
        // Skips: ore piles (nothing to light) + core tower parts (scene already has the big reactor glow).
        private void AddWindowGlow(GameObject parent, Sprite sprite, BuildingData data)
        {
            if (!enableWindowGlow || sprite == null || data.isOreNode || data.isCoreTowerPart) return;

            if (_glowTemplate == null)
            {
                _glowTemplate = GameObject.Find(GlowTemplateName); // once, then cached
                if (_glowTemplate == null) // inactive objects escape Find — sweep all lights
                {
                    foreach (var l in FindObjectsByType<Light2D>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                        if (l.name == GlowTemplateName) { _glowTemplate = l.gameObject; break; }
                }
            }
            if (_glowTemplate == null || _glowTemplate.GetComponent<Light2D>() == null)
            {
                if (!_warnedNoTemplate)
                {
                    _warnedNoTemplate = true;
                    Debug.LogWarning($"[BuildingVisualSpawner] WindowGlow skipped — template '{GlowTemplateName}' " +
                                     "not in scene (run 'Setup Rendering (Bloom + Reactor Glow)' first)");
                }
                return;
            }

            var glowGo = Instantiate(_glowTemplate, parent.transform);
            glowGo.name = "WindowGlow";
            // sprite pivot is bottom-center → bounds.center = visual middle of the facade
            glowGo.transform.localPosition = sprite.bounds.center;
            glowGo.transform.localScale = Vector3.one;

            var light = glowGo.GetComponent<Light2D>();
            light.color = windowGlowColor;
            light.intensity = windowGlowIntensity;
            light.pointLightInnerRadius = 0.2f;
            light.pointLightOuterRadius = Mathf.Clamp(sprite.bounds.size.y * windowGlowRadiusScale, 1.2f, 4f);

            // template may carry LightFlicker (Phase B) — retune it to this glow's intensity
            var flicker = glowGo.GetComponent<LightFlicker>();
            if (flicker != null) flicker.baseIntensity = windowGlowIntensity;

            // TEMP diagnostic (remove after WindowGlow is confirmed visible in play)
            Debug.Log($"[WindowGlow] spawned on {parent.name} @ {glowGo.transform.position} " +
                      $"intensity={light.intensity} radius={light.pointLightOuterRadius:0.00} " +
                      $"blendStyle={light.blendStyleIndex} enabled={light.enabled} active={glowGo.activeInHierarchy} " +
                      $"flicker={(flicker != null)}");

            // TEMP TEST v2 — one-shot lighting-system diagnosis (remove after):
            //   1. logs the ACTIVE pipeline + renderer type (is Renderer2D really running?)
            //   2. spawns two red point lights: LEFT = blend style 0 (multiply), RIGHT = style 1 (additive)
            //   3. dims all global lights for 1.5s — screen must go dark if 2D lighting works at all
            if (!_testLightSpawned)
            {
                _testLightSpawned = true;
                Debug.Log($"[WindowGlow] PIPELINE: {UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline?.name ?? "NULL"} | " +
                          $"renderer: {Camera.main?.GetUniversalAdditionalCameraData()?.scriptableRenderer?.GetType().Name ?? "NULL"}");

                SpawnTestLight(parent.transform.position + new Vector3(-4f, 0f, 0f), 0, "TEST_MULTIPLY_LIGHT");
                SpawnTestLight(parent.transform.position + new Vector3(4f, 0f, 0f), 1, "TEST_ADDITIVE_LIGHT");
                StartCoroutine(BlackoutProbe());
            }
        }

        // TEMP diagnostic helpers — remove with the test block above
        private void SpawnTestLight(Vector3 pos, int blendStyle, string name)
        {
            var go = Instantiate(_glowTemplate);
            go.name = name;
            go.transform.position = pos;
            var fl = go.GetComponent<LightFlicker>();
            if (fl != null) Destroy(fl); // hold constant intensity
            var l = go.GetComponent<Light2D>();
            l.color = Color.red;
            l.intensity = 6f;
            l.blendStyleIndex = blendStyle;
            l.pointLightInnerRadius = 2f;
            l.pointLightOuterRadius = 8f;
            Debug.Log($"[WindowGlow] TEST light '{name}' blendStyle={blendStyle} @ {pos}");
        }

        private System.Collections.IEnumerator BlackoutProbe()
        {
            yield return new WaitForSeconds(1f); // let the scene settle first
            var globals = new List<Light2D>();
            var saved = new List<float>();
            foreach (var l in FindObjectsByType<Light2D>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
                if (l.lightType == Light2D.LightType.Global) { globals.Add(l); saved.Add(l.intensity); l.intensity = 0.02f; }
            Debug.Log($"[WindowGlow] BLACKOUT probe ON — dimmed {globals.Count} global lights. " +
                      "Screen dark now = 2D lighting IS working. Unchanged = lights are inert.");
            yield return new WaitForSeconds(1.5f);
            for (int i = 0; i < globals.Count; i++)
                if (globals[i] != null) globals[i].intensity = saved[i];
            Debug.Log("[WindowGlow] BLACKOUT probe OFF — lights restored.");
        }

        // เงา silhouette = รูปสไปรต์อาคารเอง — child แยกจาก SpriteRenderer ตัวแม่
        private void AddShadow(GameObject parent, Sprite sprite, int baseSort, BuildingData data)
        {
            if (sprite == null) return;
            var shadow = new GameObject("Shadow");
            shadow.transform.SetParent(parent.transform, false);

            var sr = shadow.AddComponent<SpriteRenderer>();
            sr.sortingLayerName = BuildingsSortingLayer;
            sr.sortingOrder = baseSort - 1; // ใต้ตัวอาคาร เหนือพื้น (front building ที่ order สูงกว่าบังเงาได้ตามธรรมชาติ)
            ConfigureShadow(sr, sprite, data);
        }

        // ทำสไปรต์ให้เป็น "เงาทอดพื้น": ดำล้วน + พลิกลง (scaleY ลบ) + หุบเตี้ย
        // pivot ฐานล่างกลาง → ฐานเงาติดฐานอาคาร แล้วทอดลงด้านหน้า (ล่างจอ = ใกล้ผู้ชม) เหมือนแสงส่องจากบน
        // คุมความยาวสูงสุดด้วย shadowMaxHeight เพื่อไม่ให้อาคารสูง (core tower) ทอดเงายาวเวอร์
        // หมุนรอบฐาน (lean) = เอียงตามทิศแสง — ฐานเงายังติดฐานอาคารเพราะ pivot อยู่ล่างกลาง
        //
        // ★ data.overrideShadow = true → ใช้ค่าเงาเฉพาะของอาคารหลังนั้น (ลากตั้งได้ที่ Shadow Editor)
        //   ไม่งั้นใช้ค่ากลางของ spawner ตัวนี้ — อาคารเดิมทุกหลังจึงไม่กระทบ
        public void ConfigureShadow(SpriteRenderer sr, Sprite sprite, BuildingData data)
        {
            if (sr == null || sprite == null) return;
            bool ovr = data != null && data.overrideShadow;

            float alpha  = ovr ? data.shadowAlpha        : shadowAlpha;
            float squash = ovr ? data.shadowSquash       : shadowSquash;
            float lean   = ovr ? data.shadowLeanDegrees  : shadowLeanDegrees;
            Vector2 off  = ovr ? data.shadowOffset       : shadowOffset;

            sr.sprite = sprite;
            sr.enabled = enableShadows;
            sr.color = new Color(0f, 0f, 0f, alpha);

            float spriteH = sprite.bounds.size.y; // world units (คิด pivot+ppu แล้ว · ยังไม่คูณ transform scale)
            if (spriteH > 0.001f) squash = Mathf.Min(squash, shadowMaxHeight / spriteH);
            sr.transform.localPosition = new Vector3(off.x, off.y, 0f);
            sr.transform.localRotation = Quaternion.Euler(0f, 0f, lean);
            sr.transform.localScale    = new Vector3(1f, -squash, 1f); // ลบ = พลิกลง · หุบตาม squash
        }

        /// <summary>
        /// ยิงค่าเงาปัจจุบันลงเงาทุกหลังที่ spawn อยู่ — ให้ลาก slider แล้วเห็นผลทันทีตอน Play
        /// (เรียกจาก OnValidate ตอนแก้ Inspector · ปุ่มขวาคอมโพเนนต์ก็สั่งเองได้)
        /// </summary>
        [ContextMenu("Refresh All Shadows")]
        public void RefreshAllShadows()
        {
            var registry = BuildingRegistry.Instance;
            foreach (var kvp in _spawnedVisuals)
            {
                var go = kvp.Value;
                if (go == null) continue;
                var shadowTf = go.transform.Find("Shadow");
                if (shadowTf == null) continue;
                var sr = shadowTf.GetComponent<SpriteRenderer>();
                if (sr == null) continue;

                BuildingData data = null;
                registry?.PlacedBuildings.TryGetValue(kvp.Key, out data); // ค่า override รายอาคาร
                ConfigureShadow(sr, sr.sprite, data);
            }
        }

        // แก้ค่าใน Inspector (ตอน Play หรือ edit mode) → เงาอัปเดตทันที ไม่ต้อง restart
        private void OnValidate()
        {
            if (!Application.isPlaying) return; // edit mode ยังไม่มี visual ที่ spawn ไว้
            RefreshAllShadows();
        }
    }
}
