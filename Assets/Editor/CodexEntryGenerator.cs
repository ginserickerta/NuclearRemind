using UnityEditor;
using UnityEngine;

namespace NuclearReMind.Editor
{
    /// <summary>
    /// สร้าง CodexEntry ScriptableObject assets สำหรับ 5 entries แกน (Core branch)
    /// รันผ่าน menu: NuclearReMind / Generate Core Codex Entries
    ///
    /// เนื้อหาที่สร้างเป็น placeholder — ทีมต้องตรวจและแก้ไขให้ถูกต้องตามแหล่งอ้างอิง TINT
    /// ก่อน submit NSC 2026
    /// </summary>
    public static class CodexEntryGenerator
    {
        private const string OutputPath = "Assets/ScriptableObjects/CodexEntries";

        [MenuItem("NuclearReMind/Generate Core Codex Entries")]
        public static void GenerateCoreEntries()
        {
            var entries = new[]
            {
                new EntryDef(
                    id: "Core_01",
                    title: "ฟิชชัน (Nuclear Fission)",
                    branch: "Core",
                    cost: 0,
                    unlockedBy: "phase_1_complete",
                    content:
@"ฟิชชัน (Fission) คือปฏิกิริยาที่นิวเคลียสของอะตอมหนัก เช่น ยูเรเนียม-235 (U-235) แตกออก
เป็นสองส่วนเล็กลง พร้อมปล่อยนิวตรอน 2-3 ตัว และพลังงานจำนวนมหาศาล

⚛️ กระบวนการ:
1. นิวตรอนความเร็วต่ำ (นิวตรอนความร้อน) ชนกับนิวเคลียส U-235
2. นิวเคลียสดูดซับนิวตรอน กลายเป็น U-236 ที่ไม่เสถียร
3. U-236 แตกตัว → เช่น Kr-92 + Ba-141 + 3 นิวตรอน + พลังงาน ~200 MeV

🔑 ความสำคัญ:
พลังงาน 200 MeV ต่อหนึ่งอะตอม ฟังดูน้อย แต่ถ้ามีอะตอม U-235 จำนวน 6×10²³ ตัว
จะได้พลังงานสูงกว่าการเผาถ่านหินหลายล้านเท่า นี่คือหัวใจของโรงไฟฟ้านิวเคลียร์

⚠️ หมายเหตุสำหรับทีม: ตรวจสอบตัวเลขและตัวอย่างนิวไคลด์กับแหล่งอ้างอิง TINT"
                ),
                new EntryDef(
                    id: "Core_02",
                    title: "ปฏิกิริยาลูกโซ่ (Chain Reaction)",
                    branch: "Core",
                    cost: 0,
                    unlockedBy: "phase_1_complete",
                    content:
@"ปฏิกิริยาลูกโซ่ (Chain Reaction) เกิดขึ้นเมื่อนิวตรอนจากการฟิชชันหนึ่งครั้ง
ไปกระตุ้นการฟิชชันครั้งถัดไป ทำให้ปฏิกิริยาดำเนินต่อเนื่องด้วยตัวเอง

⛓️ สามสถานะ:
• Sub-critical (k < 1): จำนวนฟิชชันลดลง — ปฏิกิริยาดับเอง
• Critical (k = 1): จำนวนฟิชชันคงที่ — เป็นสถานะที่โรงไฟฟ้าควบคุมให้อยู่
• Super-critical (k > 1): จำนวนฟิชชันเพิ่มขึ้นเรื่อยๆ — อันตราย

🏭 การควบคุมในโรงไฟฟ้า:
ใช้แท่งควบคุม (Control Rods) ทำจากโบรอนหรือแคดเมียม ดูดซับนิวตรอนส่วนเกิน
เพื่อรักษาสถานะ Critical พอดี ให้พลังงานออกมาอย่างสม่ำเสมอ

⚠️ หมายเหตุสำหรับทีม: เพิ่มตัวอย่างเชิงตัวเลขและภาพประกอบ Chain Reaction"
                ),
                new EntryDef(
                    id: "Core_03",
                    title: "ครึ่งชีวิต (Half-Life)",
                    branch: "Core",
                    cost: 50,
                    unlockedBy: "",
                    content:
@"ครึ่งชีวิต (Half-Life, t½) คือเวลาที่ใช้เพื่อให้ปริมาณสารกัมมันตรังสีลดลงเหลือครึ่งหนึ่ง

📐 สมการการสลาย:
N(t) = N₀ × (½)^(t/t½)
โดยที่ N₀ คือปริมาณเริ่มต้น, t คือเวลาที่ผ่านไป

⏱️ ตัวอย่างครึ่งชีวิต:
• ไอโอดีน-131 (I-131): 8.02 วัน — ใช้รักษามะเร็งต่อมไทรอยด์
• ซีเซียม-137 (Cs-137): 30.17 ปี — กากนิวเคลียร์ระยะกลาง
• ยูเรเนียม-238 (U-238): 4.47 พันล้านปี — วัตถุดิบพลังงานนิวเคลียร์
• คาร์บอน-14 (C-14): 5,730 ปี — ใช้กำหนดอายุทางโบราณคดี

🔑 ความสำคัญ:
ครึ่งชีวิตบอกเราว่ากากนิวเคลียร์จะอยู่อันตรายนานแค่ไหน และช่วยออกแบบ
ระบบจัดเก็บที่ปลอดภัยในระยะยาว

⚠️ หมายเหตุสำหรับทีม: ตรวจสอบค่าครึ่งชีวิตกับแหล่ง IAEA/TINT"
                ),
                new EntryDef(
                    id: "Core_04",
                    title: "สารหล่อเย็น (Coolant)",
                    branch: "Core",
                    cost: 50,
                    unlockedBy: "",
                    content:
@"สารหล่อเย็น (Coolant) คือสารที่ไหลผ่านแกนปฏิกรณ์เพื่อดูดซับความร้อนจากการฟิชชัน
และนำไปผลิตไอน้ำหมุนกังหันผลิตไฟฟ้า

💧 ประเภทสารหล่อเย็นในโรงไฟฟ้านิวเคลียร์:
• น้ำธรรมดา (Light Water): ใช้ใน PWR, BWR — พบมากที่สุดในโลก
• น้ำหนัก (Heavy Water, D₂O): ใช้ใน CANDU — ใช้ยูเรเนียมธรรมชาติได้โดยตรง
• ก๊าซ CO₂ หรือฮีเลียม: ใช้ใน Gas-cooled Reactor
• โลหะเหลว (Liquid Metal, เช่น โซเดียม): ใช้ใน Fast Breeder Reactor

🏭 วงจรความร้อน:
น้ำหล่อเย็นที่ร้อน → เครื่องกำเนิดไอน้ำ → ไอน้ำหมุนกังหัน → ผลิตไฟฟ้า
→ น้ำกลั่นตัวเย็นลง → กลับสู่แกนปฏิกรณ์

⚠️ หมายเหตุสำหรับทีม: เพิ่มภาพ diagram วงจรหล่อเย็นและข้อมูลเฉพาะ SMR"
                ),
                new EntryDef(
                    id: "Core_05",
                    title: "รังสีและการป้องกัน (Radiation & Shielding)",
                    branch: "Core",
                    cost: 75,
                    unlockedBy: "",
                    content:
@"รังสีนิวเคลียร์มีหลายประเภท แต่ละประเภทต้องการวัสดุป้องกันต่างกัน

☢️ สามประเภทหลัก:
• รังสีแอลฟา (α): อนุภาคขนาดใหญ่ หยุดได้ด้วยกระดาษหรือผิวหนัง
  อันตรายมากถ้าสูดดมหรือกลืนเข้าร่างกาย
• รังสีบีตา (β): อิเล็กตรอนเร็ว หยุดได้ด้วยอะลูมิเนียมหนาไม่กี่มิลลิเมตร
• รังสีแกมมา (γ): คลื่นแม่เหล็กไฟฟ้าพลังงานสูง ทะลุได้มาก
  ต้องใช้ตะกั่วหนาหรือคอนกรีตกันรังสี

📏 หน่วยวัด:
• เบกเคอเรล (Bq): จำนวนการสลายตัวต่อวินาที
• ซีเวิร์ต (Sv): ปริมาณรังสีที่ร่างกายได้รับ (ปรับตามชนิดรังสี)
• ค่าปลอดภัยทั่วไป: < 1 mSv/ปี สำหรับประชาชนทั่วไป

🔬 ประโยชน์ของรังสี:
นอกจากอันตราย รังสียังใช้ในการแพทย์ (ฉายรังสี PET Scan), เกษตรกรรม
(ฉายรังสีอาหาร), และอุตสาหกรรม (ตรวจสอบโลหะ)

⚠️ หมายเหตุสำหรับทีม: ตรวจสอบค่า dose limit กับ ICRP/TINT และเพิ่มตาราง shielding"
                ),
            };

            System.IO.Directory.CreateDirectory(OutputPath);

            foreach (var def in entries)
            {
                string path = $"{OutputPath}/{def.Id}_CodexEntry.asset";

                var existing = AssetDatabase.LoadAssetAtPath<CodexEntry>(path);
                if (existing != null)
                {
                    Debug.Log($"[CodexEntryGenerator] ข้ามไฟล์ที่มีอยู่แล้ว: {path}");
                    continue;
                }

                var asset = ScriptableObject.CreateInstance<CodexEntry>();
                asset.entryId = def.Id;
                asset.title = def.Title;
                asset.branch = def.Branch;
                asset.content = def.Content;
                asset.researchPointCost = def.Cost;
                asset.unlockedByEvent = def.UnlockedBy;

                AssetDatabase.CreateAsset(asset, path);
                Debug.Log($"[CodexEntryGenerator] สร้าง: {path}");
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[CodexEntryGenerator] เสร็จแล้ว! เปิด ScriptableObjects/CodexEntries/ เพื่อตรวจและแก้เนื้อหา");
        }

        private readonly struct EntryDef
        {
            public readonly string Id, Title, Branch, Content, UnlockedBy;
            public readonly int Cost;

            public EntryDef(string id, string title, string branch, int cost, string unlockedBy, string content)
            {
                Id = id; Title = title; Branch = branch;
                Cost = cost; UnlockedBy = unlockedBy; Content = content;
            }
        }
    }
}
