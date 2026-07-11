using UnityEngine;

namespace NuclearReMind
{
    /// <summary>
    /// คณิตศาสตร์เลือกเฟรมอนิเมชัน (idle loop) — pure ล้วน ให้เทสต์ตรวจได้โดยไม่ต้องมี scene
    /// แยกจาก MonoBehaviour เพื่อล็อกพฤติกรรมสำคัญด้วยเทสต์ (วนลูป · fps ≤ 0 · เฟรมเดียว)
    /// </summary>
    public static class SpriteAnimationMath
    {
        /// <summary>
        /// index เฟรมที่ควรแสดง ณ เวลา elapsed (วินาที) · วนลูปเสมอ
        /// frameCount ≤ 0 → -1 (ไม่มีเฟรม) · fps ≤ 0 → ค้างเฟรม 0 (กันหารศูนย์/อนิเมชันบ้า)
        /// </summary>
        public static int FrameIndex(float elapsed, float fps, int frameCount)
        {
            if (frameCount <= 0) return -1;
            if (frameCount == 1 || fps <= 0f || elapsed <= 0f) return 0;

            int raw = (int)(elapsed * fps);
            int idx = raw % frameCount;
            if (idx < 0) idx += frameCount; // elapsed ติดลบ (ไม่ควรเกิด) — กัน index ติดลบไว้
            return idx;
        }
    }

    /// <summary>
    /// เล่นอนิเมชันอาคารด้วยการสลับ sprite บน SpriteRenderer (idle loop) — ไม่ใช้ Animator/.controller
    ///
    /// ทำไมไม่ใช้ Animator + AnimationClip:
    ///   โปรเจกต์นี้ไม่มี .controller/.anim สักไฟล์ (visual ทุกอย่างขับด้วยโค้ด — ล้อ WorkerView/BuildingVisualSpawner)
    ///   อนิเมชัน idle ของอาคารคือ "วน sprite ไม่กี่เฟรม" ไม่ต้องใช้ state machine — Animator จึงหนักเกินงาน
    ///   และ sprite-swap คุม sortingOrder/สเกลเองได้ตรงกับที่ BuildingVisualSpawner ตั้งไว้
    ///
    /// BuildingVisualSpawner ใส่ component นี้ให้เองเมื่อ BuildingData มี animationFrames ≥ 2 เฟรม
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public class SpriteFrameAnimator : MonoBehaviour
    {
        public Sprite[] frames;
        public float fps = 6f;

        private SpriteRenderer _sr;
        private float _elapsed;
        private int _current = -1;

        private void Awake() => _sr = GetComponent<SpriteRenderer>();

        /// <summary>ตั้งเฟรม + ความเร็ว แล้วเริ่มเล่นจากเฟรม 0 (เรียกจาก spawner ตอน spawn)</summary>
        public void Play(Sprite[] animationFrames, float framesPerSecond)
        {
            frames = animationFrames;
            fps = framesPerSecond;
            _elapsed = 0f;
            _current = -1;
            if (_sr == null) _sr = GetComponent<SpriteRenderer>();
            Apply();
        }

        private void Update()
        {
            if (frames == null || frames.Length < 2) return; // เฟรมเดียว/ว่าง — ไม่ต้องอัปเดต
            _elapsed += Time.deltaTime;
            Apply();
        }

        private void Apply()
        {
            if (frames == null || frames.Length == 0 || _sr == null) return;
            int idx = SpriteAnimationMath.FrameIndex(_elapsed, fps, frames.Length);
            if (idx < 0 || idx == _current) return; // เฟรมเดิม — ไม่แตะ SpriteRenderer (เลี่ยง dirty เปล่า)
            _current = idx;
            _sr.sprite = frames[idx];
        }
    }
}
