using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace NuclearReMind.EditorTools
{
    /// <summary>
    /// ทาสีพื้นหลายช่องพร้อมกัน — เปิดหน้าต่างนี้ เปิด "โหมดทา" แล้วทำงานที่ Scene view:
    ///   • หัวแปรง Single = คลิก/ลากทาทีละช่อง
    ///   • หัวแปรง Rectangle = ลากคลุมกรอบ ปล่อยเมาส์ = เติมทั้งกรอบทีเดียว
    /// สไตล์สี:
    ///   • Solid = สีเดียว (Color A)
    ///   • Checkerboard = สลับ A/B ตาราง isometric ด้วย (col+row)&1 (แนวเดียวกับพื้น Zone A)
    ///
    /// เก็บเป็น override list บน OreDepositManager.tileColorOverrides → GridSpriteFiller ทาทับ
    /// ตอน 'Fill Grids' จึงไม่หายเมื่อ re-fill/เปลี่ยนสีโซน (SetColor สด = เห็นทันที · เซฟลงซีนทั้งคู่)
    ///
    /// เปิด: เมนู NuclearReMind → Tile Color Painter
    /// </summary>
    public class TileColorPainter : EditorWindow
    {
        private const string GroundName = "Ground";
        private const string StoneName = "StonePave";
        private const string StoneFolder = "Assets/Sprites/Art/Tiles/Stone";
        private const int PreviewCellCap = 800; // เกินนี้พรีวิวเฉพาะกรอบ (กันหน่วง)

        private enum Action { Paint, Erase }
        private enum Brush { Single, Rectangle }
        private enum Style { Solid, Checkerboard, Gradient, Stain }
        private enum GradientDir { Horizontal, Vertical, DiagonalNE, DiagonalNW }
        /// <summary>What the brush edits: grass tint on Ground, or stone tiles on StonePave.</summary>
        private enum Target { GroundColor, StonePave }

        private bool _painting;
        private Target _target = Target.GroundColor;
        private Action _action = Action.Paint;
        private Brush _brush = Brush.Single;
        private Style _style = Style.Solid;                             // Solid / Checkerboard / Gradient
        private GradientDir _gradDir = GradientDir.Horizontal;         // ทิศไล่สีของ Gradient
        private Color _colorA = new Color(0.478f, 0.549f, 0.306f, 1f);  // #7A8C4E
        private Color _colorB = new Color(0.431f, 0.478f, 0.278f, 1f);  // #6E7A47

        // Stain brush: darkens whatever colour a cell already has, with per-cell variation so the
        // patch looks organic instead of a flat blob (รอยคล้ำบนหญ้า)
        private Color _stainColor = new Color(0.216f, 0.259f, 0.169f, 1f); // #37422B dark olive
        private float _stainStrength = 0.45f;
        private float _stainVariation = 0.55f;   // 0 = uniform, 1 = very mottled
        // cells already stained during the current drag — stops a single stroke compounding to black
        private readonly HashSet<Vector3Int> _strokeCells = new HashSet<Vector3Int>();

        // stone brush: index 0 = auto (variant picked from the cell hash, like the automatic pass)
        private static readonly string[] StoneTileNames =
        {
            "อัตโนมัติ (สุ่มตามช่อง)",
            // เทาอ่อน
            "stone_full_0", "stone_full_1", "stone_full_2",
            "stone_crack_0", "stone_crack_1",
            "stone_moss_0", "stone_moss_1",
            "stone_worn_0", "stone_worn_1",
            "stone_edge_0",
            // เหลืองอ่อน (อุ่น)
            "stone_full_3", "stone_full_4",
            "stone_crack_2", "stone_moss_2",
            "stone_worn_2", "stone_edge_1",
        };
        private int _stoneTileIndex;

        private Tilemap _ground;
        private Tilemap _stone;
        private bool _dragging;
        private Vector3Int _rectStart, _rectEnd;

        [MenuItem("NuclearReMind/Tile Color Painter")]
        public static void Open() => GetWindow<TileColorPainter>("Tile Color Painter");

        private void OnEnable() => SceneView.duringSceneGui += OnSceneGUI;
        private void OnDisable() => SceneView.duringSceneGui -= OnSceneGUI;

        private void OnGUI()
        {
            EditorGUILayout.LabelField("ทาสีพื้นหลายช่อง", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "1) เปิด 'โหมดทา'\n2) เลือกหัวแปรง + สไตล์ + สี\n3) ทำที่หน้าต่าง Scene:\n" +
                "   Single = คลิก/ลาก · Rectangle = ลากคลุมกรอบแล้วปล่อย\nเสร็จแล้วปิดโหมด + Ctrl+S",
                MessageType.Info);

            _painting = EditorGUILayout.ToggleLeft(
                _painting ? "● โหมดทา: เปิดอยู่ (ทำในกริดที่ Scene ได้)" : "โหมดทา: ปิด", _painting);

            EditorGUILayout.Space();
            _target = (Target)EditorGUILayout.EnumPopup(
                new GUIContent("ทาอะไร", "สีพื้น = ทาสีหญ้า/ดิน · หินปูพื้น = วางไทล์หินบน StonePave"), _target);
            _action = (Action)EditorGUILayout.EnumPopup("การกระทำ", _action);
            _brush = (Brush)EditorGUILayout.EnumPopup("หัวแปรง", _brush);

            if (_target == Target.StonePave)
            {
                DrawStoneGUI();
                return;
            }

            using (new EditorGUI.DisabledScope(_action == Action.Erase))
            {
                _style = (Style)EditorGUILayout.EnumPopup("สไตล์สี", _style);

                if (_style == Style.Stain)
                {
                    EditorGUILayout.HelpBox(
                        "รอยคล้ำ: ทำสีที่มีอยู่ให้เข้มลงแบบด่าง ๆ (ไม่ทับสีเดิมทิ้ง) — " +
                        "ลากซ้ำหลายรอบ = คล้ำขึ้นเรื่อย ๆ", MessageType.None);
                    _stainColor = EditorGUILayout.ColorField("สีรอยคล้ำ", _stainColor);
                    _stainStrength = EditorGUILayout.Slider("ความเข้ม", _stainStrength, 0f, 1f);
                    _stainVariation = EditorGUILayout.Slider("ความด่าง", _stainVariation, 0f, 1f);
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField("ลัด:", GUILayout.Width(30));
                    if (GUILayout.Button("คล้ำดิน #37422B")) _stainColor = Hex(0x37422B);
                    if (GUILayout.Button("คล้ำเทา #3A3E38")) _stainColor = Hex(0x3A3E38);
                    EditorGUILayout.EndHorizontal();
                }
                else
                {
                    _colorA = EditorGUILayout.ColorField(
                        _style == Style.Checkerboard ? "สี A (คู่)" :
                        _style == Style.Gradient ? "สี A (เริ่ม)" : "สี", _colorA);
                    if (_style != Style.Solid)
                        _colorB = EditorGUILayout.ColorField(
                            _style == Style.Gradient ? "สี B (จบ)" : "สี B (คี่)", _colorB);
                    if (_style == Style.Gradient)
                        _gradDir = (GradientDir)EditorGUILayout.EnumPopup("ทิศไล่สี", _gradDir);
                }

                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("ลัด:", GUILayout.Width(30));
                if (GUILayout.Button("A=#7A8C4E")) _colorA = Hex(0x7A8C4E);
                if (GUILayout.Button("B=#6E7A47")) _colorB = Hex(0x6E7A47);
                EditorGUILayout.EndHorizontal();

                if (_style == Style.Gradient)
                {
                    EditorGUILayout.HelpBox("Gradient: หัวแปรง Rectangle = ไล่สีในกรอบที่ลาก · หรือปุ่มด้านล่าง = ไล่ทั้งพื้น (กริด)", MessageType.None);
                    using (new EditorGUI.DisabledScope(Ore() == null || Ground() == null))
                        if (GUILayout.Button("ไล่สี Gradient ทั้งพื้น (auto)"))
                            ApplyGradientWholeGround();
                }
            }

            EditorGUILayout.Space();
            var ore = Ore();
            int count = ore != null && ore.tileColorOverrides != null ? ore.tileColorOverrides.Count : 0;
            EditorGUILayout.LabelField($"ช่องที่ทาไว้: {count}");

            using (new EditorGUI.DisabledScope(count == 0))
                if (GUILayout.Button("ล้างสีที่ทาทั้งหมด (คืน gradient)"))
                    ClearAll();

            if (ore == null)
                EditorGUILayout.HelpBox("ไม่พบ OreDepositManager — เปิด Gamescene ก่อน", MessageType.Warning);
        }

        // ── stone mode UI ──
        private void DrawStoneGUI()
        {
            using (new EditorGUI.DisabledScope(_action == Action.Erase))
                _stoneTileIndex = EditorGUILayout.Popup("ไทล์หิน", _stoneTileIndex, StoneTileNames);

            EditorGUILayout.Space();
            var stone = Stone();
            if (stone == null)
            {
                EditorGUILayout.HelpBox(
                    "ยังไม่มี Tilemap 'StonePave' — รันเมนู NuclearReMind/Setup Stone Ground (Plaza + Paths) ก่อน",
                    MessageType.Warning);
                return;
            }

            var ov = Overrides(stone);
            EditorGUILayout.LabelField($"ช่องหินที่ทาเอง: {(ov != null ? ov.overrides.Count : 0)}");
            EditorGUILayout.HelpBox(
                "หินที่ทาเองถูกเก็บเป็น override บน StonePave → รัน 'Setup Stone Ground' ซ้ำแล้วไม่หาย",
                MessageType.Info);

            using (new EditorGUI.DisabledScope(ov == null || ov.overrides.Count == 0))
                if (GUILayout.Button("ล้างหินที่ทาเองทั้งหมด (คืนค่าอัตโนมัติ)"))
                    ClearStoneOverrides(stone);
        }

        private void OnSceneGUI(SceneView sv)
        {
            if (!_painting) return;
            HandleUtility.AddDefaultControl(GUIUtility.GetControlID(FocusType.Passive)); // กันเลือก object อื่น

            Event e = Event.current;
            if (e.button != 0) return;

            if (_brush == Brush.Single)
            {
                if (e.type == EventType.MouseDown) _strokeCells.Clear(); // new stroke = stain can layer again
                if (e.type == EventType.MouseDown || e.type == EventType.MouseDrag)
                {
                    if (TryCell(e, out var cell))
                    {
                        // drag fires repeatedly on the same cell — without this a stain goes black instantly
                        if (_style != Style.Stain || _strokeCells.Add(cell)) ApplyCell(cell);
                        e.Use();
                    }
                }
                return;
            }

            // Rectangle: down = เริ่ม · drag = อัปเดตกรอบ (พรีวิว) · up = เติมทั้งกรอบ
            switch (e.type)
            {
                case EventType.MouseDown:
                    if (TryCell(e, out _rectStart)) { _rectEnd = _rectStart; _dragging = true; e.Use(); }
                    break;
                case EventType.MouseDrag:
                    if (_dragging && TryCell(e, out _rectEnd)) { SceneView.RepaintAll(); e.Use(); }
                    break;
                case EventType.MouseUp:
                    if (_dragging) { _dragging = false; ApplyRect(_rectStart, _rectEnd); e.Use(); }
                    break;
            }

            if (_dragging) DrawRectPreview();
        }

        // ── apply ──
        private void ApplyCell(Vector3Int pos)
        {
            if (_target == Target.StonePave) { ApplyStoneCell(pos); return; }

            var tm = Ground(); var ore = Ore();
            if (tm == null || ore == null || !tm.HasTile(pos)) return;

            Undo.RecordObject(ore, "Paint Tile Color");
            Undo.RegisterCompleteObjectUndo(tm, "Paint Tile Color");
            // หัวแปรงเดี่ยว: gradient อ้างอิงทั้งกริด (แต้มให้กลมกลืนกับ gradient รวม) · solid/checker ไม่สนขอบเขต
            GridBounds(out int gx0, out int gy0, out int gx1, out int gy1);
            PaintOne(tm, ore, pos, gx0, gy0, gx1, gy1);
            Commit(ore);
        }

        private void ApplyRect(Vector3Int a, Vector3Int b)
        {
            if (_target == Target.StonePave) { ApplyStoneRect(a, b); return; }

            var tm = Ground(); var ore = Ore();
            if (tm == null || ore == null) return;

            int x0 = Mathf.Min(a.x, b.x), x1 = Mathf.Max(a.x, b.x);
            int y0 = Mathf.Min(a.y, b.y), y1 = Mathf.Max(a.y, b.y);

            Undo.RecordObject(ore, "Paint Tile Rect");
            Undo.RegisterCompleteObjectUndo(tm, "Paint Tile Rect");
            int n = 0;
            for (int x = x0; x <= x1; x++)
                for (int y = y0; y <= y1; y++)
                {
                    var pos = new Vector3Int(x, y, 0);
                    if (tm.HasTile(pos)) { PaintOne(tm, ore, pos, x0, y0, x1, y1); n++; }
                }
            Commit(ore);
            Debug.Log($"[TileColorPainter] เติม {n} ช่อง ({x0},{y0})–({x1},{y1}) · {(_action == Action.Erase ? "ลบ" : _style.ToString())}");
        }

        // ทา 1 ช่อง (SetColor สด + อัปเดต override list) ตาม action/style ปัจจุบัน
        private void PaintOne(Tilemap tm, NuclearReMind.OreDepositManager ore, Vector3Int pos,
                              int x0, int y0, int x1, int y1)
        {
            var cell = new Vector2Int(pos.x, pos.y);
            tm.SetTileFlags(pos, TileFlags.None);
            if (_action == Action.Erase)
            {
                RemoveOverride(ore, cell);
                tm.SetColor(pos, DefaultColorAt(pos.x, pos.y, ore));
            }
            else
            {
                // Stain darkens the colour already on the cell, so it needs the tilemap, not just (x,y)
                Color c = _style == Style.Stain
                    ? StainAt(tm, pos)
                    : ColorAt(pos.x, pos.y, x0, y0, x1, y1);
                UpsertOverride(ore, cell, c);
                tm.SetColor(pos, c);
            }
        }

        // สีของช่องตามสไตล์ (x0..y1 = ขอบเขตอ้างอิงของ Gradient — กรอบที่ลาก/ทั้งกริด)
        //   Solid = A · Checkerboard: (col+row) คู่=A คี่=B · Gradient = Lerp(A,B) ตามทิศ
        private Color ColorAt(int x, int y, int x0, int y0, int x1, int y1)
        {
            switch (_style)
            {
                case Style.Checkerboard: return ((x + y) & 1) == 1 ? _colorB : _colorA;
                case Style.Gradient:     return GradientColor(x, y, x0, y0, x1, y1);
                case Style.Stain:        return _stainColor; // preview only — real blend needs the cell's colour
                default:                 return _colorA;
            }
        }

        /// <summary>
        /// Blend the cell's CURRENT colour toward the stain colour. Per-cell hash noise varies the
        /// amount so a painted area looks mottled/organic rather than a flat dark rectangle.
        /// </summary>
        private Color StainAt(Tilemap tm, Vector3Int pos)
        {
            Color cur = tm.GetColor(pos);
            float n = NuclearReMind.IsoGroundPainter.Hash(pos.x, pos.y) / 2147483647f; // 0..1
            float amount = _stainStrength * Mathf.Lerp(1f, n, _stainVariation);
            return Color.Lerp(cur, _stainColor, Mathf.Clamp01(amount));
        }

        // ไล่สี A→B ตามทิศที่เลือก · t = ตำแหน่งช่องเทียบขอบเขต (0 ต้นทาง → 1 ปลายทาง)
        private Color GradientColor(int x, int y, int x0, int y0, int x1, int y1)
        {
            float t;
            switch (_gradDir)
            {
                case GradientDir.Vertical:   t = Frac(y,     y0,      y1);      break;
                case GradientDir.DiagonalNE: t = Frac(x + y, x0 + y0, x1 + y1); break; // ทแยงตามความลึก iso
                case GradientDir.DiagonalNW: t = Frac(x - y, x0 - y1, x1 - y0); break;
                default:                     t = Frac(x,     x0,      x1);      break; // Horizontal
            }
            return Color.Lerp(_colorA, _colorB, t);
        }

        private static float Frac(float v, float lo, float hi)
            => Mathf.Approximately(hi, lo) ? 0f : Mathf.Clamp01((v - lo) / (hi - lo));

        // ขอบเขตกริดเล่นจริง (0..cols-1, 0..rows-1) — ใช้เป็นช่วง gradient ของหัวแปรงเดี่ยว/ปุ่มทั้งพื้น
        private static void GridBounds(out int x0, out int y0, out int x1, out int y1)
        {
            var grid = Object.FindFirstObjectByType<GridManager>();
            int cols = grid != null ? grid.columns : 43;
            int rows = grid != null ? grid.rows : 43;
            x0 = 0; y0 = 0; x1 = cols - 1; y1 = rows - 1;
        }

        private void Commit(NuclearReMind.OreDepositManager ore)
        {
            EditorUtility.SetDirty(ore);
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Repaint();
        }

        private void ClearAll()
        {
            var ore = Ore();
            if (ore == null) return;
            Undo.RecordObject(ore, "Clear Tile Colors");
            ore.tileColorOverrides.Clear();
            EditorUtility.SetDirty(ore);
            NuclearReMind.Editor.GridSpriteFiller.FillFromMenu(); // ระบายพื้นใหม่คืน gradient
            Repaint();
        }

        // ปุ่ม auto: ไล่สี Gradient A→B ทั้งกริด (0..cols-1, 0..rows-1) ทีเดียว ตามทิศที่เลือก
        private void ApplyGradientWholeGround()
        {
            var tm = Ground(); var ore = Ore();
            if (tm == null || ore == null) return;

            GridBounds(out int x0, out int y0, out int x1, out int y1);
            Undo.RecordObject(ore, "Gradient Whole Ground");
            Undo.RegisterCompleteObjectUndo(tm, "Gradient Whole Ground");
            int n = 0;
            for (int x = x0; x <= x1; x++)
                for (int y = y0; y <= y1; y++)
                {
                    var pos = new Vector3Int(x, y, 0);
                    if (tm.HasTile(pos)) { PaintOne(tm, ore, pos, x0, y0, x1, y1); n++; }
                }
            Commit(ore);
            Debug.Log($"[TileColorPainter] ไล่สี Gradient ทั้งพื้น {n} ช่อง · ทิศ {_gradDir}");
        }

        // ── stone painting ──
        private void ApplyStoneCell(Vector3Int pos)
        {
            var stone = Stone();
            if (stone == null) return;
            var ov = Overrides(stone);

            Undo.RegisterCompleteObjectUndo(stone, "Paint Stone");
            if (ov != null) Undo.RecordObject(ov, "Paint Stone");
            PaintStoneOne(stone, ov, pos);
            CommitStone(stone, ov);
        }

        private void ApplyStoneRect(Vector3Int a, Vector3Int b)
        {
            var stone = Stone();
            if (stone == null) return;
            var ov = Overrides(stone);

            int x0 = Mathf.Min(a.x, b.x), x1 = Mathf.Max(a.x, b.x);
            int y0 = Mathf.Min(a.y, b.y), y1 = Mathf.Max(a.y, b.y);

            Undo.RegisterCompleteObjectUndo(stone, "Paint Stone Rect");
            if (ov != null) Undo.RecordObject(ov, "Paint Stone Rect");
            int n = 0;
            for (int x = x0; x <= x1; x++)
                for (int y = y0; y <= y1; y++)
                {
                    PaintStoneOne(stone, ov, new Vector3Int(x, y, 0));
                    n++;
                }
            CommitStone(stone, ov);
            Debug.Log($"[TileColorPainter] หิน: {(_action == Action.Erase ? "ลบ" : "วาง")} {n} ช่อง ({x0},{y0})–({x1},{y1})");
        }

        private void PaintStoneOne(Tilemap stone, NuclearReMind.StonePaveOverrides ov, Vector3Int pos)
        {
            var cell = new Vector2Int(pos.x, pos.y);

            if (_action == Action.Erase)
            {
                stone.SetTile(pos, null);
                // record the erase so the automatic pass doesn't put stone back here
                UpsertStoneOverride(ov, cell, string.Empty);
                return;
            }

            var tile = PickStoneTile(pos);
            if (tile == null) return;
            stone.SetTile(pos, tile);
            stone.SetTileFlags(pos, TileFlags.None);
            stone.SetColor(pos, CurrentStoneTint());
            UpsertStoneOverride(ov, cell, tile.name);
        }

        /// <summary>Chosen tile, or an auto variant matching the automatic pass's distribution.</summary>
        private TileBase PickStoneTile(Vector3Int pos)
        {
            if (_stoneTileIndex > 0)
                return LoadStoneTile(StoneTileNames[_stoneTileIndex]);

            int h = (NuclearReMind.IsoGroundPainter.Hash(pos.x, pos.y) / 7) % 100;
            // mostly grey (full 0-2) with warm slabs (full 3-4) sprinkled in, like the reference
            string name = h < 45 ? $"stone_full_{h % 3}"
                        : h < 60 ? $"stone_full_{3 + h % 2}"
                        : h < 85 ? $"stone_crack_{h % 2}"
                        :          $"stone_moss_{h % 2}";
            return LoadStoneTile(name);
        }

        private static TileBase LoadStoneTile(string name)
        {
            var tile = AssetDatabase.LoadAssetAtPath<TileBase>($"{StoneFolder}/{name}.asset");
            if (tile == null)
                Debug.LogWarning($"[TileColorPainter] ไม่พบไทล์ {name} — รัน 'Generate Stone Tiles' ก่อน");
            return tile;
        }

        // match the live tint so hand-painted cells don't stand out against the automatic ones
        private static Color CurrentStoneTint()
        {
            var mood = Object.FindFirstObjectByType<NuclearReMind.AshfallMoodController>();
            return mood != null ? mood.stoneTint : Color.white;
        }

        private static void UpsertStoneOverride(NuclearReMind.StonePaveOverrides ov, Vector2Int cell, string tileName)
        {
            if (ov == null) return;
            var list = ov.overrides;
            for (int i = 0; i < list.Count; i++)
                if (list[i].cell == cell) { list[i] = new NuclearReMind.StonePaveOverride(cell, tileName); return; }
            list.Add(new NuclearReMind.StonePaveOverride(cell, tileName));
        }

        private void ClearStoneOverrides(Tilemap stone)
        {
            var ov = Overrides(stone);
            if (ov == null) return;
            Undo.RecordObject(ov, "Clear Stone Overrides");
            ov.overrides.Clear();
            EditorUtility.SetDirty(ov);
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            StoneGroundSetup.Apply();   // repaint from the automatic rules only
            Repaint();
        }

        private void CommitStone(Tilemap stone, NuclearReMind.StonePaveOverrides ov)
        {
            EditorUtility.SetDirty(stone);
            if (ov != null) EditorUtility.SetDirty(ov);
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Repaint();
        }

        private Tilemap Stone()
        {
            if (_stone != null) return _stone;
            var go = GameObject.Find(StoneName);
            _stone = go != null ? go.GetComponent<Tilemap>() : null;
            return _stone;
        }

        /// <summary>Override store lives on the StonePave object — added on first use.</summary>
        private static NuclearReMind.StonePaveOverrides Overrides(Tilemap stone)
        {
            if (stone == null) return null;
            var ov = stone.GetComponent<NuclearReMind.StonePaveOverrides>();
            if (ov == null)
            {
                ov = Undo.AddComponent<NuclearReMind.StonePaveOverrides>(stone.gameObject);
                EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            }
            return ov;
        }

        // ── scene helpers ──
        private bool TryCell(Event e, out Vector3Int cell)
        {
            cell = default;
            var tm = Ground();
            if (tm == null) return false;
            Ray r = HandleUtility.GUIPointToWorldRay(e.mousePosition);
            Vector3 world = Mathf.Abs(r.direction.z) > 1e-5f
                ? r.origin + r.direction * (-r.origin.z / r.direction.z) // ตัดระนาบ z=0
                : new Vector3(r.origin.x, r.origin.y, 0f);
            cell = tm.WorldToCell(world);
            return true;
        }

        // พรีวิวกรอบ: จุดสีต่อช่อง (เกิน cap = เฉพาะ 4 มุม กันหน่วง)
        private void DrawRectPreview()
        {
            var tm = Ground();
            if (tm == null) return;
            int x0 = Mathf.Min(_rectStart.x, _rectEnd.x), x1 = Mathf.Max(_rectStart.x, _rectEnd.x);
            int y0 = Mathf.Min(_rectStart.y, _rectEnd.y), y1 = Mathf.Max(_rectStart.y, _rectEnd.y);
            int area = (x1 - x0 + 1) * (y1 - y0 + 1);
            float rWorld = tm.layoutGrid != null ? tm.layoutGrid.cellSize.y * 0.35f : 0.18f;

            if (area <= PreviewCellCap)
            {
                for (int x = x0; x <= x1; x++)
                    for (int y = y0; y <= y1; y++)
                    {
                        var pos = new Vector3Int(x, y, 0);
                        if (!tm.HasTile(pos)) continue;
                        Handles.color = _action == Action.Erase ? Color.white : ColorAt(x, y, x0, y0, x1, y1);
                        Handles.DrawSolidDisc(tm.GetCellCenterWorld(pos), Vector3.forward, rWorld);
                    }
            }
            else
            {
                Handles.color = Color.yellow;
                foreach (var c in new[] { new Vector3Int(x0, y0, 0), new Vector3Int(x1, y0, 0),
                                          new Vector3Int(x0, y1, 0), new Vector3Int(x1, y1, 0) })
                    Handles.DrawWireCube(tm.GetCellCenterWorld(c), Vector3.one * rWorld * 3f);
            }
            Handles.Label(tm.GetCellCenterWorld(_rectEnd), $"  {area} ช่อง");
        }

        // ── override + default ──
        private Color DefaultColorAt(int x, int y, NuclearReMind.OreDepositManager ore)
        {
            var grid = Object.FindFirstObjectByType<GridManager>();
            int cols = grid != null ? grid.columns : 43;
            int rows = grid != null ? grid.rows : 43;
            if (!(x < 0 || x >= cols || y < 0 || y >= rows)) return Color.white;

            var pal = ore.groundPalette;
            if (pal.zoneA_base.a < 0.5f) pal = NuclearReMind.GroundPalette.Default;
            float f = NuclearReMind.IsoGroundPainter.OutsideDarken(x, y, cols, rows, pal);
            return new Color(f, f, f, 1f);
        }

        private static void UpsertOverride(NuclearReMind.OreDepositManager ore, Vector2Int cell, Color color)
        {
            var list = ore.tileColorOverrides;
            for (int i = 0; i < list.Count; i++)
                if (list[i].cell == cell) { list[i] = new NuclearReMind.TileColorOverride(cell, color); return; }
            list.Add(new NuclearReMind.TileColorOverride(cell, color));
        }

        private static void RemoveOverride(NuclearReMind.OreDepositManager ore, Vector2Int cell)
        {
            var list = ore.tileColorOverrides;
            for (int i = list.Count - 1; i >= 0; i--)
                if (list[i].cell == cell) list.RemoveAt(i);
        }

        private Tilemap Ground()
        {
            if (_ground != null) return _ground;
            var go = GameObject.Find(GroundName);
            _ground = go != null ? go.GetComponent<Tilemap>() : null;
            return _ground;
        }

        private static NuclearReMind.OreDepositManager Ore()
            => Object.FindFirstObjectByType<NuclearReMind.OreDepositManager>();

        private static Color Hex(int rgb) =>
            new Color(((rgb >> 16) & 0xFF) / 255f, ((rgb >> 8) & 0xFF) / 255f, (rgb & 0xFF) / 255f, 1f);
    }
}
