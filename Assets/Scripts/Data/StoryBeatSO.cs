using System.Collections.Generic;
using UnityEngine;

namespace NuclearReMind
{
    /// <summary>
    /// StoryBeat หนึ่งจังหวะของเนื้อเรื่อง (Story Guide §2) — มัดชิ้นส่วนเข้าด้วยกัน + เงื่อนไข trigger
    /// StoryDirector ฟัง event ของเมือง → เช็ค trigger → เล่นตามลำดับบังคับ:
    ///   record → infoCard → crisis (Choice/Outcome) → quiz
    /// ทุกชิ้นเป็น optional (null = ข้าม) · แต่ละ beat ยิงครั้งเดียวต่อรอบเล่น
    /// implement IQuizTrigger: QuizManager.EnqueueQuizzes(beat) เอาควิซของ beat เข้าคิวถามได้ทันที
    /// </summary>
    [CreateAssetMenu(fileName = "NewStoryBeat", menuName = "NuclearReMind/Story/Story Beat")]
    public class StoryBeatSO : ScriptableObject, IQuizTrigger
    {
        public string beatId; // เช่น "deuterium_ignition"

        [Header("Trigger (ดูความหมาย param ในคอมเมนต์ StoryTriggerType)")]
        public StoryTriggerType triggerType;
        public string triggerParam;

        [Header("ชิ้นส่วน — เล่นตามลำดับ record → infoCard → dialoguePre → crisis → outcome → dialoguePost → quiz (เว้น null/ว่างได้)")]
        public RecordCardSO record;      // การ์ดบันทึกกู้คืน (เก็บเข้าแผง Records)
        public InfoCardSO infoCard;      // ★ ความรู้ก่อนควิซเสมอ — ห้ามมี quiz โดยไม่มีแหล่งความรู้นำ
        public DilemmaData crisis;       // การ์ดวิกฤต + ทางเลือก A/B/C (= CrisisSO ของ Story Guide)
        public QuizQuestionSO[] quiz;    // ควิซทบทวน ยิงหลัง Outcome (beat ไม่มี crisis ก็ยิงหลัง infoCard)

        [Header("บทสนทนาหลายตัวละคร (v8.5 — portrait+บอลลูน VN · เว้นว่างได้)")]
        public DialogueLine[] dialoguePre;   // บทเปิด: หลัง infoCard ก่อนการ์ดวิกฤต (Kova↔Mira↔Dorn สลับกัน)
        public DialogueLine[] dialoguePost;  // บทปิด: หลัง outcome ก่อนควิซ (ใช้ร่วมทุกทางเลือก A/B/C)

        [Header("บรรยากาศ (optional — โชว์เป็นข้อความในการ์ด/log)")]
        [TextArea(1, 3)] public string npcLinePre;      // บทพูด NPC ก่อนเข้า beat เช่น "Kova: ..."
        [TextArea(1, 3)] public string innerVoiceAfter; // เสียงในใจ Auren ปิดท้าย beat (ตัวเอียงท้ายการ์ด)
        [TextArea(1, 2)] public string[] logLines;      // ข้อความ log หลายบรรทัดของ beat (ระบบ/NPC/เควสต์)
        public bool logLinesDaily;                      // true = ปล่อยวันละบรรทัดไม่รวบ (ลางพายุ Day 20–23) · false = โชว์ทุกบรรทัดทันที

        [TextArea(1, 4)] public string noteTH; // โน้ตสำหรับทีม — ไม่โชว์ผู้เล่น

        /// <summary>IQuizTrigger: คืนควิซของ beat (กรอง null ทิ้ง) — ให้ QuizManager เข้าคิวถามตามลำดับ</summary>
        public QuizQuestionSO[] GetLinkedQuizzes()
        {
            if (quiz == null || quiz.Length == 0)
                return System.Array.Empty<QuizQuestionSO>();

            var list = new List<QuizQuestionSO>(quiz.Length);
            foreach (var q in quiz)
                if (q != null) list.Add(q);
            return list.ToArray();
        }
    }
}
