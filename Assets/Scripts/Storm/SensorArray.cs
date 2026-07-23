using UnityEngine;

namespace NuclearReMind
{
    /// <summary>
    /// [TH] หน้าที่: เครือข่ายเซนเซอร์เตือนพายุ — ปลดล็อกเมื่อวิจัย storm_detection สำเร็จ (เบาะแสมาจาก Record #3)
    /// เปลี่ยนพายุจาก "เส้นตายที่มองไม่เห็น" เป็นตัวเลขนับถอยหลัง (คาดวันพายุมาถึง) + ให้ cooling เพิ่มเล็กน้อย
    ///
    /// Sensor Array (GDD §23 / STORY.md Record #3). Unlocked once storm_detection is researched — the
    /// lead pre-unlocked by recovering Record #3. It turns the storm from a blind deadline into a
    /// countdown (predicted arrival day) and gives the reactor a little cooling headroom (+4).
    ///
    /// The sim gap it closes: a well-run city recovers Record #3 early → sensor ~100% of runs; an
    /// average one ~14% → plays the endgame blind. Plain query component (no day tick).
    /// </summary>
    public class SensorArray : MonoBehaviour
    {
        public static SensorArray Instance { get; private set; }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        /// <summary>Active once the storm_detection note is completed (Record #3 → lead → research).</summary>
        public bool IsActive => KnowledgeDB.Instance.HasNote("storm_detection");

        // [TH] คาดว่าอีกกี่วันพายุจะมาถึง = (แรงกดดันที่เหลือ ÷ อัตราไต่ล่าสุด) — -1 ถ้ายังไม่มีข้อมูล, 0 ถ้าพายุมาแล้ว
        /// <summary>
        /// Days until the storm hits, from current pressure and the latest rise. Only meaningful while
        /// active; returns -1 if not active, 0 if already here. Static so it's testable without a scene.
        /// </summary>
        public static int PredictedDaysUntilStorm(StormSystem storm)
        {
            if (storm == null) return -1;
            if (storm.IsStormActive) return 0;
            if (storm.LastRise <= 0.001f) return -1; // no data yet
            float remaining = GameConfigSO.Instance.stormPressureMax - storm.Pressure;
            return Mathf.Max(0, Mathf.CeilToInt(remaining / storm.LastRise));
        }
    }
}
