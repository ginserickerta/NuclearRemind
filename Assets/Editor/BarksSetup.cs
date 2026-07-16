using System.IO;
using UnityEditor;
using UnityEngine;

namespace NuclearReMind.EditorTools
{
    /// <summary>
    /// Generates the 42 speaker BarkSO assets (BARKS.md — Kova 14 · Mira 14 · Dorn 8 · Citizen 6) into
    /// Assets/Resources/Barks/ so BarkManager auto-loads them. Text/priority/cooldown/once are VERBATIM
    /// from BARKS.md — ห้ามแต่งใหม่. The 20 Inner-Voice (V##) lines are milestone/event one-shots, not
    /// state polls, so they're wired via story hooks in Sprint 6, not here. Idempotent (keeps GUIDs).
    /// Run: menu NuclearReMind > Setup Barks.
    /// </summary>
    public static class BarksSetup
    {
        private const string Folder = "Assets/Resources/Barks";
        private const int Once = 99; // cooldown placeholder for onceOnly barks

        [MenuItem("NuclearReMind/Setup Barks")]
        public static void Apply()
        {
            Directory.CreateDirectory(Folder);

            // ── KOVA ────────────────────────────────────────────────
            W("K01", BarkSpeaker.Kova, "เตาไม่มีเชื้อเพลิง ฉันไม่รู้ว่ามันใช้อะไร ไปให้โรงวิจัยหาให้หน่อย", 90, Once, true);
            W("K02", BarkSpeaker.Kova, "ความร้อนขึ้นเร็วเกินที่หล่อเย็นจะรับไหว ต้องหาวิธีเพิ่มกำลังหล่อเย็น", 90, Once, true);
            W("K03", BarkSpeaker.Kova, "ความร้อนยังขึ้นอยู่ ยังไม่ได้เริ่มวิจัยเลยนะ", 70, 2, false);
            W("K04", BarkSpeaker.Kova, "คอยล์มาแล้ว ไปติดที่หอควบคุมได้เลย", 80, 2, false);
            W("K05", BarkSpeaker.Kova, "ติดแค่ตัวเดียวไม่พอ ต้องมีอีกตัว ความร้อนถึงจะลง", 75, 3, false);
            W("K06", BarkSpeaker.Kova, "เก้าสิบแล้ว ถึงร้อยเมื่อไหร่คือจบ ลดโหมดเดี๋ยวนี้", 100, 1, false);
            W("K07", BarkSpeaker.Kova, "เร่งแบบนี้หล่อเย็นตามไม่ทัน เลือกอย่างใดอย่างหนึ่ง", 85, 2, false);
            W("K08", BarkSpeaker.Kova, "CORE เจ็ดสิบแปด ใกล้ตันแล้ว เชื้อเพลิงที่มีดันได้แค่นี้", 90, Once, true);
            W("K09", BarkSpeaker.Kova, "CORE ค้างอยู่ที่แปดสิบ ไม่ขยับเลย ขาดอะไรสักอย่าง", 95, 2, false);
            W("K10", BarkSpeaker.Kova, "CORE ขยับแล้ว Zone B ทำงานได้จริง", 80, Once, true);
            W("K11", BarkSpeaker.Kova, "โรงวิจัยหยุดนิ่ง คนไม่ครบหรือของไม่พอ ไปดูหน่อย", 70, 2, false);
            W("K12", BarkSpeaker.Kova, "ไฟติดลบ อีกเดี๋ยวดับทั้งเมือง", 85, 1, false);
            W("K13", BarkSpeaker.Kova, "เก้าสิบห้า ใกล้แล้ว อย่าเพิ่งพลาดตอนนี้", 80, 2, false);
            W("K14", BarkSpeaker.Kova, "เตาปกติดี แต่คนข้างล่างไม่ไหวแล้ว", 75, 3, false);

            // ── MIRA ────────────────────────────────────────────────
            W("M01", BarkSpeaker.Mira, "มีคนล้มสองคนแล้ว ไม่มีแผล ไม่มีไข้ ฉันยังไม่รู้ว่าเป็นอะไร", 95, Once, true);
            W("M02", BarkSpeaker.Mira, "ฉันรักษาไม่ได้ถ้ายังไม่รู้ว่ามันคืออะไร ต้องวิจัยก่อน", 85, 2, false);
            W("M03", BarkSpeaker.Mira, "รู้วิธีรักษาแล้ว แต่ยังไม่มีที่รักษา สร้างที่พยาบาลก่อน", 85, 2, false);
            W("M04", BarkSpeaker.Mira, "ที่พยาบาลเต็ม คนไข้ล้นออกมาข้างนอก", 90, 2, false);
            W("M05", BarkSpeaker.Mira, "อาการหนักแล้วหนึ่งคน เหลือเวลาไม่กี่วัน", 95, 1, false);
            W("M06", BarkSpeaker.Mira, "เสียไปแล้วหนึ่งคน ฉันไม่อยากบอกข่าวแบบนี้อีก", 90, 3, false);
            W("M07", BarkSpeaker.Mira, "คนใน Zone B ยังไม่มีชุดกันรังสีครบ", 85, 2, false);
            W("M08", BarkSpeaker.Mira, "รังสีสะสมในตัวคนงานสูงขึ้นทุกวัน มันไม่ลดเอง", 80, 3, false);
            W("M09", BarkSpeaker.Mira, "จำกัดเวลาให้คนอยู่ในโซนเสี่ยง แค่นี้คนป่วยก็น้อยลงแล้ว", 60, Once, true);
            W("M10", BarkSpeaker.Mira, "คนหิวสามคนขึ้นไป ทำงานไม่ไหวหรอก", 80, 2, false);
            W("M11", BarkSpeaker.Mira, "คนเริ่มไม่เชื่อว่าจะรอด ฉันจะพูดยังไงกับพวกเขาดี", 85, 3, false);
            W("M12", BarkSpeaker.Mira, "ข้างล่างเริ่มพูดกันว่าจะรอด", 50, 5, false);
            W("M13", BarkSpeaker.Mira, "เราสร้างหอคอยเสร็จไปเพื่ออะไร ถ้าคนที่ต้องใช้มันไม่เหลือ", 90, 2, false);
            W("M14", BarkSpeaker.Mira, "ที่พยาบาลพร้อมแล้ว หวังว่าจะไม่ต้องใช้", 60, Once, true);

            // ── DORN ────────────────────────────────────────────────
            W("D01", BarkSpeaker.Dorn, "ข้าวเน่าเร็วกว่าปกติสามเท่า ผมทำนามาสามสิบปี ไม่เคยเจอแบบนี้", 95, Once, true);
            W("D02", BarkSpeaker.Dorn, "ยุ้งจะว่างในสามวัน ยังไม่มีใครหาทางแก้ได้เลยเหรอ", 85, 2, false);
            W("D03", BarkSpeaker.Dorn, "คุณจะเอารังสีมายิงใส่ข้าวที่คนต้องกินเนี่ยนะ", 90, Once, true);
            W("D04", BarkSpeaker.Dorn, "ผมยังไม่สบายใจ แต่ยุ้งไม่ว่างแล้ว ก็เอาเถอะ", 70, Once, true);
            W("D05", BarkSpeaker.Dorn, "ข้าวสายพันธุ์นี้โตในดินที่ผมคิดว่าปลูกอะไรไม่ขึ้นแล้ว", 70, Once, true);
            W("D06", BarkSpeaker.Dorn, "ยุ้งว่างเปล่า ผมไม่มีอะไรให้ใครแล้ว", 95, 1, false);
            W("D07", BarkSpeaker.Dorn, "คนในฟาร์มเหลือน้อยเกินไป ผมทำคนเดียวไม่ไหว", 75, 3, false);
            W("D08", BarkSpeaker.Dorn, "ผมเคยกลัวของพวกนี้ ตอนนี้ผมเป็นคนเปิดเครื่องเอง", 60, Once, true);

            // ── CITIZEN ─────────────────────────────────────────────
            W("C01", BarkSpeaker.Citizen, "หอคอยจะเสร็จเมื่อไหร่ครับ มีใครรู้บ้างไหม", 60, Once, true);
            W("C02", BarkSpeaker.Citizen, "เราทำงานทั้งวันแต่ไม่มีอะไรดีขึ้นเลย", 75, 3, false);
            W("C03", BarkSpeaker.Citizen, "มีคนเก็บของแล้ว เขาบอกว่าที่อื่นน่าจะดีกว่านี้", 90, 2, false);
            W("C04", BarkSpeaker.Citizen, "เด็กพวกนั้นไม่ควรต้องอยู่ตรงนั้น", 95, Once, true);
            W("C05", BarkSpeaker.Citizen, "ขอบคุณที่ไม่เอาลูกผมไป", 70, Once, true);
            W("C06", BarkSpeaker.Citizen, "เมื่อคืนไฟไม่ดับเลยสักครั้ง นานแล้วนะที่ไม่มีแบบนี้", 50, 5, false);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[BarksSetup] สร้าง/อัปเดต BarkSO 42 ใบ ที่ " + Folder);
        }

        private static void W(string barkId, BarkSpeaker speaker, string text, int priority, int cooldownDays, bool onceOnly)
        {
            string path = $"{Folder}/{barkId}.asset";
            var b = AssetDatabase.LoadAssetAtPath<BarkSO>(path);
            bool isNew = b == null;
            if (isNew) b = ScriptableObject.CreateInstance<BarkSO>();

            b.barkId = barkId;
            b.speaker = speaker;
            b.text = text;
            b.priority = priority;
            b.cooldownDays = cooldownDays;
            b.onceOnly = onceOnly;

            if (isNew) AssetDatabase.CreateAsset(b, path);
            else EditorUtility.SetDirty(b);
        }
    }
}
