using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace NuclearReMind
{
    /// <summary>
    /// สแต็กกลางของ "Game UI" — แผงที่กดเปิดทับ scene (Core Tower/Building/Lab/Codex/Inventory/Memorial/Records/Help
    /// + ควิซ/วิกฤต) รวมกติกา 3 ข้อไว้ที่เดียว กันลำดับ Update ของแต่ละแผงชนกัน:
    ///   1) เปิดแผง → ดัน sortingOrder ขึ้นบนสุด (overrideSorting) — บนสุดเสมอ แม้อยู่คนละ Canvas
    ///   2) มี Game UI เปิดอยู่ → Esc ปิด "ตัวบนสุด" (แผงปิดได้ = เหมือนปุ่ม ✕) / เงียบ (แผงบังคับ เช่นควิซ) — ไม่เปิด Pause
    ///   3) Pause เปิดได้เฉพาะตอนไม่มี Game UI เปิด (PauseMenuController เช็ค AnyOpen ก่อน)
    ///
    /// แผงลงทะเบียนผ่าน Push (ตอนเปิด) / Pop (ตอนปิด) · เป็น util ฝั่ง UI ล้วน ไม่ผูก manager gameplay
    /// (คู่ขนานกับ registry แบบ static ที่โปรเจกต์ใช้อยู่ เช่น BuildingDepthSort.All)
    /// </summary>
    public static class GameUIStack
    {
        /// <summary>สัญญาที่แผง Game UI ทุกตัวต้อง implement (ผ่าน explicit interface — ไม่รก public API ของแผง)</summary>
        public interface IPanel
        {
            /// <summary>true = Esc ปิดได้ (เหมือนปุ่ม ✕) · false = แผงบังคับ (ควิซ/วิกฤต) Esc เงียบ ปิดเองไม่ได้</summary>
            bool ClosableByEscape { get; }
            /// <summary>ดันแผงขึ้นบนสุด (impl เรียก GameUIStack.RaiseToTop กับ root ของตัวเอง)</summary>
            void BringToFront();
            /// <summary>ปิดแผงแบบเดียวกับปุ่ม ✕ (สแต็ก Pop ให้ตอนแผงปิด — ไม่ต้อง Pop เองในนี้)</summary>
            void CloseFromStack();
            /// <summary>
            /// GameObject รากของแผง (ตัวที่ SetActive เปิด/ปิด) — ให้สแต็ก "self-heal":
            /// ถ้า root ปิด/หายไปทั้งที่ยังค้างในสแต็ก = ถือว่าแผงปิดแล้ว → prune ออกเอง
            /// (กัน ESC ถูกกลืนค้างถาวรจากรายการค้าง เช่น enter play mode ไม่ reload domain / ปิดทางอื่นโดยไม่ Pop)
            /// </summary>
            GameObject PanelRoot { get; }
        }

        // ── ค่าฐาน sortingOrder ของ Game UI (เหนือ HUD=0 / Story=60 / QuizExpl=70 / Pause=100) → บนสุดเสมอ ──
        private const int BaseSortingOrder = 200;

        private static readonly List<IPanel> _open = new List<IPanel>(); // เรียงตามลำดับเปิด (ท้ายสุด = บนสุด)
        private static int _counter;                                     // นับขึ้นเรื่อย ๆ · เปิดทีหลัง = order สูงกว่า

        // ล้าง static ทุกครั้งที่เริ่ม Play (กัน "Enter Play Mode แบบไม่ reload domain" ทำให้แผงรอบก่อนค้าง
        // → HandleEscape กลืน Esc เสมอ → Pause เปิดไม่ได้)
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() { _open.Clear(); _counter = 0; }

        /// <summary>มี Game UI เปิดค้างอยู่ไหม (ล้าง entry ที่ถูก destroy ทิ้งไปด้วย)</summary>
        public static bool AnyOpen
        {
            get
            {
                PruneDead();
                return _open.Count > 0;
            }
        }

        /// <summary>แผงเปิด → ดันขึ้นบนสุด + จำในสแต็ก (เรียกซ้ำได้ = ย้ายไปบนสุด)</summary>
        public static void Push(IPanel panel)
        {
            if (panel == null) return;
            _open.Remove(panel);
            _open.Add(panel);
            panel.BringToFront();
        }

        /// <summary>แผงปิด → เอาออกจากสแต็ก (idempotent)</summary>
        public static void Pop(IPanel panel)
        {
            if (panel == null) return;
            _open.Remove(panel);
        }

        /// <summary>
        /// จัดการ Esc: ปิด "แผงบนสุด" ถ้าปิดได้ · ถ้าแผงบนสุดบังคับ (ควิซ) = เงียบ (ไม่ปิด)
        /// คืน true เมื่อ "กลืน" Esc แล้ว (มี Game UI เปิดอยู่) → PauseMenuController จะไม่เปิด Pause
        /// </summary>
        public static bool HandleEscape()
        {
            for (int i = _open.Count - 1; i >= 0; i--)
            {
                var p = _open[i];
                // self-heal: แผงถูก destroy หรือ root ปิด/หาย (= ไม่ได้เปิดจริง) → prune ทิ้ง ไม่กลืน Esc
                if (IsClosed(p)) { _open.RemoveAt(i); continue; }
                if (p.ClosableByEscape) p.CloseFromStack(); // ปิดตัวบนสุด (มันจะ Pop ตัวเองตอนปิด)
                return true; // มี Game UI เปิดจริง → กลืน Esc (ทั้งกรณีปิดได้/บังคับ) ไม่ให้ไปเปิด Pause
            }
            return false; // ไม่มี Game UI เปิดจริง → ปล่อยให้ Pause จัดการต่อ
        }

        /// <summary>ดัน GameObject (root/backdrop ของแผง) ขึ้น sortingOrder บนสุด ด้วย overrideSorting (ข้าม Canvas ได้)</summary>
        public static void RaiseToTop(GameObject panelRoot)
        {
            if (panelRoot == null) return;
            var cv = panelRoot.GetComponent<Canvas>();
            if (cv == null) cv = panelRoot.AddComponent<Canvas>();
            cv.overrideSorting = true;
            cv.sortingOrder = BaseSortingOrder + (++_counter); // เปิดทีหลัง = order สูงกว่า = อยู่บน

            // ★ nested canvas ต้องมี GraphicRaycaster ของตัวเอง ไม่งั้นปุ่มในแผงกดไม่ได้
            //   (root GraphicRaycaster เห็นเฉพาะ graphic ที่ลงทะเบียนใต้ root canvas — ของใต้ sub-canvas จะถูกมองข้าม)
            if (panelRoot.GetComponent<GraphicRaycaster>() == null)
                panelRoot.AddComponent<GraphicRaycaster>();
        }

        // ล้าง entry ที่ไม่ได้เปิดจริงแล้ว (destroy หรือ root ปิด/หาย) — กันสแต็กค้างจนกลืน Esc/บล็อก Pause ถาวร
        private static void PruneDead()
        {
            for (int i = _open.Count - 1; i >= 0; i--)
                if (IsClosed(_open[i])) _open.RemoveAt(i);
        }

        // แผงนี้ "ไม่ได้เปิดอยู่จริงแล้ว" ไหม → ต้อง prune ออกจากสแต็ก
        //   • ถูก Destroy (MonoBehaviour → "fake null" ของ Unity — cast เป็น Object แล้วใช้ == ที่ Unity override)
        //   • หรือ root ถูกปิด/หาย (activeSelf=false / null) ทั้งที่ยังค้างในสแต็ก = ถือว่าปิดแล้ว
        //     (แผงที่กำลังเล่นแอนิเมชันหุบปิดถูก Pop ไปแล้ว จึงไม่อยู่ในสแต็ก — ไม่โดน prune ผิด)
        private static bool IsClosed(IPanel p)
        {
            if (ReferenceEquals(p, null)) return true;
            if (p is UnityEngine.Object o && o == null) return true; // controller ถูก destroy
            var root = p.PanelRoot;
            return root == null || !root.activeSelf; // Unity == จับ destroyed · activeSelf=false = ปิดอยู่
        }
    }
}
