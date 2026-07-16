using System.IO;
using UnityEditor;
using UnityEngine;

namespace NuclearReMind.EditorTools
{
    /// <summary>
    /// Generates the 4 Data-Recovery RecordCardSO assets (GDD §24 / STORY.md §3) into
    /// Assets/Resources/Records/ so DataRecovery auto-loads them.
    ///
    /// ★ bodyTH is VERBATIM from STORY.md — ห้ามแต่งใหม่. Each record's unlocksLead pre-unlocks a
    /// research lead (record_01→water_analysis · record_02→magnetic_theory ·
    /// record_03→storm_detection · record_final→lithium_breeding). Idempotent (keeps GUIDs).
    /// Run: menu NuclearReMind > Setup Data Records.
    /// </summary>
    public static class DataRecordsSetup
    {
        private const string Folder = "Assets/Resources/Records";

        [MenuItem("NuclearReMind/Setup Data Records")]
        public static void Apply()
        {
            Directory.CreateDirectory(Folder);

            Write("record_01", "water_analysis", "บันทึก #01 — เชื้อเพลิงในน้ำ",
"ถ้ามีใครได้อ่านข้อความนี้ แปลว่าห้องวิจัยยังไม่พังหมด\n" +
"เชื้อเพลิงตัวแรกอยู่ในที่ที่นายคาดไม่ถึง — มันอยู่ในน้ำ\n\n" +
"ยังมีบันทึกอีกหลายส่วนที่กู้ไม่สำเร็จ\n" +
"ระบบจะถอดรหัสต่อเมื่อเมืองเดินหน้า");

            Write("record_02", "magnetic_theory", "บันทึก #02 — ปมขดลวด",
"วันที่เราแพ้ ไม่ใช่เพราะสนามอ่อน\n" +
"แต่เพราะมีคนเร่งเตาก่อนคอยล์จะพร้อม\n\n" +
"ถ้านายอ่านถึงตรงนี้ — อย่ารีบ ติดให้ครบก่อน");

            Write("record_03", "storm_detection", "บันทึก #03 — ปมพายุ",
"Zone B ไม่ได้ปิดตายเพราะมันพัง — เราปิดมันเองเพราะเรากลัวทริเทียม\n" +
"เตาที่เลี้ยงเชื้อเพลิงของตัวเองได้ คือเตาที่ไม่ต้องพึ่งใคร\n" +
"แต่เราไม่กล้าพอจะไว้ใจมัน\n\n" +
"เครื่องวัดที่เพี้ยนบ่อยผิดปกติ ไม่ใช่เครื่องเสีย\n" +
"มันคือสัญญาณ");

            Write("record_final", "lithium_breeding", "บันทึก #04 — เฉลยปม",
"ก่อนหอคอยระเบิด — เราตรวจพบบางอย่าง\n" +
"คลื่นรังสีที่จะตามมาหลังการระเบิด\n\n" +
"เราเขียนมันลงในรายงาน รายงานที่ควรจะถึงมือทุกคน\n\n" +
"ถ้ามันกำลังจะมา เตาต้องติดเต็มร้อยก่อนมันจะถึง\n" +
"นั่นคือทางเดียว");

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[DataRecordsSetup] สร้าง/อัปเดต RecordCardSO 4 ใบ ที่ " + Folder);
        }

        private static void Write(string recordId, string unlocksLead, string archiveTitle, string bodyTH)
        {
            string path = $"{Folder}/{recordId}.asset";
            var r = AssetDatabase.LoadAssetAtPath<RecordCardSO>(path);
            bool isNew = r == null;
            if (isNew) r = ScriptableObject.CreateInstance<RecordCardSO>();

            r.recordId = recordId;
            r.authorLabel = "ระบบกู้คืนข้อมูลจากเครือข่ายเก่า · ผู้บันทึก: Dr. Elara Vane";
            r.recorderName = "Dr. Elara Vane";
            r.statusLabel = "กู้คืนสำเร็จ";
            r.archiveTitle = archiveTitle;
            r.bodyTH = bodyTH;
            r.unlocksLead = unlocksLead;

            if (isNew) AssetDatabase.CreateAsset(r, path);
            else EditorUtility.SetDirty(r);
        }
    }
}
