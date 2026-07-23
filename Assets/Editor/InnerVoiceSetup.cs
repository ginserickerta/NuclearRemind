using System.IO;
using UnityEditor;
using UnityEngine;

namespace NuclearReMind.EditorTools
{
    /// <summary>
    /// [TH] หน้าที่: สร้างไฟล์ asset "เสียงในใจ" ของ Auren (BarkSO) 20 บรรทัด V01–V20 จาก docs/BARKS.md
    /// ลง Resources/InnerVoice ให้ InnerVoiceDirector โหลดไปใช้ — ข้อความคัดลอกตรงตามเอกสาร ห้ามแต่งใหม่
    /// Generates the 20 Inner-Voice BarkSO assets (BARKS.md §Inner Voice, V01–V20, speaker = Auren)
    /// into Assets/Resources/InnerVoice/ so InnerVoiceDirector auto-loads them. Text VERBATIM from
    /// BARKS.md — ห้ามแต่งใหม่. Idempotent (keeps GUIDs). Run: menu NuclearReMind > Setup Inner Voice.
    /// </summary>
    public static class InnerVoiceSetup
    {
        private const string Folder = "Assets/Resources/InnerVoice";

        // เมนูนี้: สร้าง/อัปเดต asset เสียงในใจ 20 บรรทัดจาก BARKS.md — รันซ้ำได้ (คง GUID เดิม)
        [MenuItem("NuclearReMind/Setup Inner Voice")]
        public static void Apply()
        {
            Directory.CreateDirectory(Folder);

            W("V01", "มืดสนิท เริ่มจากไฟก่อน");
            W("V02", "หกชื่อ... พวกเขาอยู่ที่นี่ก่อนผม");
            W("V03", "Elara ชื่อนี้อยู่บนอนุสรณ์");
            W("V04", "ผมไม่รู้คำตอบ แต่ที่นี่อาจมี");
            W("V05", "ทีมเก่าคงเคยนั่งอ่านแบบนี้เหมือนกัน");
            W("V06", "อยู่ในน้ำมาตลอด เราแค่ไม่เคยแยกมันออกมา");
            W("V07", "อีกสิบองศา");
            W("V08", "ถ้าคอยล์พัง ก็ไม่มีอะไรกั้นแล้ว");
            W("V09", "เขาเร่งเตาก่อนคอยล์พร้อม... ผมเกือบทำแบบเดียวกัน");
            W("V10", "เขาขุดแร่ให้เมืองนี้ทุกวัน");
            W("V11", "ผมรู้ชื่อเขา");
            W("V12", "รังสีทำให้เขาป่วย แล้วรังสีก็รักษาเขา");
            W("V13", "ค้างอยู่ที่แปดสิบสามวันแล้ว ผมพลาดอะไรไป");
            W("V14", "ทีมเก่าปิดโซนนี้ไว้ ตอนนี้ผมต้องเปิดมัน");
            W("V15", "พวกเขากลัวมันเกินกว่าจะลอง");
            W("V16", "รายงานที่ผมส่งวันนั้น เขียนเรื่องพายุไว้ตั้งแต่แรก");
            W("V17", "มันมาแล้ว ผมยังทำไม่เสร็จ");
            W("V18", "ผมสร้างหอคอย แต่คนข้างล่างกำลังไป");
            W("V19", "ผมเซ็นไปแล้ว");
            W("V20", "เสร็จแล้ว... ผมส่งรายงานถึงแล้ว");

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[InnerVoiceSetup] สร้าง/อัปเดต Inner Voice 20 ใบ ที่ " + Folder);
        }

        private static void W(string id, string text)
        {
            string path = $"{Folder}/{id}.asset";
            var b = AssetDatabase.LoadAssetAtPath<BarkSO>(path);
            bool isNew = b == null;
            if (isNew) b = ScriptableObject.CreateInstance<BarkSO>();

            b.barkId = id;
            b.speaker = BarkSpeaker.Auren;
            b.text = text;
            b.priority = 50;
            b.cooldownDays = 99;
            b.onceOnly = true;

            if (isNew) AssetDatabase.CreateAsset(b, path);
            else EditorUtility.SetDirty(b);
        }
    }
}
