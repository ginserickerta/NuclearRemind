using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace NuclearReMind
{
    /// <summary>
    /// [TH] หน้าที่: ผู้จัดการบทพูด NPC — ทุกสิ้นวันถ่ายภาพ state หา bark ที่เข้าเงื่อนไข + พ้น cooldown
    /// [TH] แล้วพูดสูงสุด 2 บรรทัด/วัน (priority สูงชนะ, เลือกบรรทัดที่ไม่ได้พูดนานสุดก่อน)
    /// [TH] ผลคือแต่ละสไตล์การเล่นได้ยินบทต่างกัน — เมืองที่ดูแลดีไม่มีวันได้ยิน "คนหิว"
    /// Bark manager (GDD §16 / BARKS.md). Each day-end it snapshots state, finds every bark whose
    /// condition is met (BarkConditions) and whose cooldown/once allows it, and speaks at most 2 —
    /// the highest-priority ones, preferring lines not heard recently. Bound to STATE, never the day.
    ///
    /// The point (BARKS.md sim note): different playstyles hear different lines. A well-run city never
    /// hears M10 "คนหิว"; a struggling one hears the whole Death-Spiral chorus (M05/M06/M08/C03).
    ///
    /// Plain MonoBehaviour singleton like the other v6.3 managers; Initialize(cfg) is the test entry.
    /// </summary>
    // -20: last of the day-end handlers, so it narrates the settled state.
    [DefaultExecutionOrder(-20)]
    public class BarkManager : MonoBehaviour
    {
        public static BarkManager Instance { get; private set; }

        private const int MaxBarksPerDay = 2; // BARKS.md: never more than 2/day

        private readonly Dictionary<string, BarkSO> _catalog = new Dictionary<string, BarkSO>();
        private readonly Dictionary<string, int> _lastFiredDay = new Dictionary<string, int>();
        private readonly HashSet<string> _usedOnce = new HashSet<string>();
        private bool _catalogLoaded;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            Initialize();
        }

        /// <summary>
        /// [TH] ล้างประวัติการพูดทั้งหมด (วันที่พูดล่าสุด + รายการพูดครั้งเดียว) — จุดเข้าเทสต์ด้วย
        /// Reset firing history — also the EditMode-test entry point.
        /// </summary>
        public void Initialize()
        {
            _lastFiredDay.Clear();
            _usedOnce.Clear();
        }

        private void OnEnable()
        {
            if (EventManager.Instance == null) return;
            EventManager.Instance.OnDayEnded += HandleDayEnded;
        }

        private void OnDisable()
        {
            if (EventManager.Instance == null) return;
            EventManager.Instance.OnDayEnded -= HandleDayEnded;
        }

        private void HandleDayEnded(int day)
        {
            if (day <= 1) return; // Day 1 = tutorial
            EvaluateDay(day, BarkWorldState.Snapshot());
        }

        // ─────────────────────────────────────────
        //  Catalog (tests register directly; play mode auto-loads from Resources)
        // ─────────────────────────────────────────

        // [TH] ลงทะเบียน asset bark เข้าแคตตาล็อก (เทสต์เรียกตรง · โหมดเล่นโหลดอัตโนมัติจาก Resources/Barks)
        public void RegisterCatalog(IEnumerable<BarkSO> barks)
        {
            if (barks != null)
                foreach (var b in barks)
                    if (b != null && !string.IsNullOrEmpty(b.barkId)) _catalog[b.barkId] = b;
            _catalogLoaded = true;
        }

        private void EnsureCatalog()
        {
            if (_catalogLoaded) return;
            _catalogLoaded = true;
            RegisterCatalog(Resources.LoadAll<BarkSO>("Barks")); // Assets/Resources/Barks
        }

        // ─────────────────────────────────────────
        //  Evaluate → speak up to 2 (public for tests)
        // ─────────────────────────────────────────

        // [TH] ประเมินประจำวัน: รวมทุกบรรทัดที่เข้าเงื่อนไข → เรียง priority (เสมอกันเลือกที่เงียบมานานสุด
        // แล้วเรียง id — ไม่สุ่ม ให้ผลซ้ำได้) → ยิงสูงสุด 2 บรรทัด
        public List<BarkSO> EvaluateDay(int day, in BarkWorldState state)
        {
            EnsureCatalog();
            var eligible = new List<BarkSO>();
            foreach (var b in _catalog.Values)
                if (IsEligible(b, day) && BarkConditions.IsMet(b.barkId, state))
                    eligible.Add(b);

            // highest priority first; tie-break: least-recently-said, then id (deterministic, no Random)
            eligible.Sort((a, b) =>
            {
                int p = b.priority.CompareTo(a.priority);
                if (p != 0) return p;
                int la = _lastFiredDay.TryGetValue(a.barkId, out var da) ? da : int.MinValue;
                int lb = _lastFiredDay.TryGetValue(b.barkId, out var db2) ? db2 : int.MinValue;
                int r = la.CompareTo(lb);
                return r != 0 ? r : string.CompareOrdinal(a.barkId, b.barkId);
            });

            var fired = new List<BarkSO>();
            foreach (var b in eligible)
            {
                if (fired.Count >= MaxBarksPerDay) break;
                Fire(b, day);
                fired.Add(b);
            }
            return fired;
        }

        // [TH] มีสิทธิ์พูดเมื่อ: ไม่ใช่บรรทัดพูดครั้งเดียวที่ใช้ไปแล้ว + พ้น cooldown แล้ว
        private bool IsEligible(BarkSO b, int day)
        {
            if (b.onceOnly && _usedOnce.Contains(b.barkId)) return false;
            if (_lastFiredDay.TryGetValue(b.barkId, out var last) && day - last < b.cooldownDays) return false;
            return true;
        }

        // [TH] ยิง bark จริง: จดวันที่พูด (เริ่ม cooldown) แล้วประกาศผ่าน event ให้ UI ไปแสดง
        private void Fire(BarkSO b, int day)
        {
            _lastFiredDay[b.barkId] = day;
            if (b.onceOnly) _usedOnce.Add(b.barkId);
            EventManager.Instance?.RaiseBarkFired(b);
        }

        public BarkSO GetBark(string barkId)
        {
            EnsureCatalog();
            return _catalog.TryGetValue(barkId, out var b) ? b : null;
        }
    }
}
