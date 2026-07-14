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
        }

        // ── ค่าฐาน sortingOrder ของ Game UI (เหนือ HUD=0 / Story=60 / QuizExpl=70 / Pause=100) → บนสุดเสมอ ──
        private const int BaseSortingOrder = 200;

        private static readonly List<IPanel> _open = new List<IPanel>(); // เรียงตามลำดับเปิด (ท้ายสุด = บนสุด)
        private static int _counter;                                     // นับขึ้นเรื่อย ๆ · เปิดทีหลัง = order สูงกว่า

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
                if (p == null) { _open.RemoveAt(i); continue; }
                if (p.ClosableByEscape) p.CloseFromStack(); // ปิดตัวบนสุด (มันจะ Pop ตัวเองตอนปิด)
                return true; // มี Game UI อยู่ → กลืน Esc เสมอ (ทั้งกรณีปิดได้/บังคับ) ไม่ให้ไปเปิด Pause
            }
            return false; // ไม่มี Game UI → ปล่อยให้ Pause จัดการต่อ
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

        // ล้าง entry ที่กลายเป็น null (แผงถูก destroy ตอนโหลด/รีสตาร์ทซีน)
        private static void PruneDead()
        {
            for (int i = _open.Count - 1; i >= 0; i--)
                if (_open[i] == null) _open.RemoveAt(i);
        }
    }
}
