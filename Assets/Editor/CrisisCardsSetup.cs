using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace NuclearReMind.EditorTools
{
    /// <summary>
    /// Generates the 8 CrisisCardSO assets (GDD §25 / CARDS.md) into Assets/Resources/CrisisCards/
    /// so CardManager auto-loads them at runtime.
    ///
    /// ★ title / description / dialogueLines / option labels / afterText are VERBATIM from CARDS.md —
    /// ห้ามแต่งใหม่. Numbers that CARDS.md/GDD §25 state explicitly are used as-is; a few "vague" prices
    /// (e.g. sick A "รังสีสะสมเพิ่ม") get a modest representative value. Locked options keep their
    /// requiredNoteId so CardManager greys them until the note is done. Idempotent (keeps GUIDs).
    /// Run: menu NuclearReMind > Setup Crisis Cards.
    /// </summary>
    public static class CrisisCardsSetup
    {
        private const string Folder = "Assets/Resources/CrisisCards";

        [MenuItem("NuclearReMind/Setup Crisis Cards")]
        public static void Apply()
        {
            Directory.CreateDirectory(Folder);

            // 1 · heat (confinement)
            Write(CardIds.Heat, "ความร้อนเกินพิกัด",
                "HEAT แตะ 62 · หล่อเย็นตามไม่ทัน\nถ้าถึง 100 เตาหลอมละลาย จบเกม",
                new[] { "Kova: \"ความร้อนขึ้นเร็วเกินที่หล่อเย็นจะรับไหว\"" }, 3, false,
                new[]
                {
                    Opt("เร่งไฟเข้าระบบหล่อเย็น", "พลังงาน −250 · คนที่หอควบคุม 3 คนรับรังสี +12 · HEAT −8 · Hope −4", "", "",
                        e => { e.power = -250f; e.radiationTargets = 3; e.radiationAmount = 12f; e.heatDelta = -8f; e.hopeDelta = -4f; }),
                    Opt("ลดโหมดเตา รอให้เย็นเอง", "CORE% หยุดขึ้น 2 วัน · HEAT ค่อยๆ ลง", "", "",
                        e => { e.coreStallDays = 2; }),
                    Opt("ฉีดสารหล่อเย็นฉุกเฉิน", "HEAT −22 ทันที · น้ำ −40", "Kova: \"คอยล์รับไหว ฉีดเลย\"", "confinement",
                        e => { e.heatDelta = -22f; e.water = -40f; }),
                });

            // 2 · sick (nuclear_medicine)
            Write(CardIds.Sick, "คนงานล้มพร้อมกัน",
                "คนงาน 3 คนล้ม ไม่มีแผล ไม่มีไข้\nทำงานไม่ได้ · ขวัญเมืองเริ่มตก",
                new[] { "Mira: \"ฉันยังไม่รู้ว่ามันคืออะไร รักษาไม่ถูก\"" }, 5, false,
                new[]
                {
                    Opt("ให้พวกเขาทำงานต่อ", "ได้แรงงานคืน · รังสีสะสมเพิ่ม · เสี่ยงตาย", "", "",
                        e => { e.radiationTargets = 3; e.radiationAmount = 8f; }),
                    Opt("กักตัวไว้เฉยๆ", "หยุดงาน · ความล้า +15 · Hope −6 · อาการไม่ดีขึ้นเอง", "", "",
                        e => { e.fatigueAll = 15f; e.hopeDelta = -6f; }),
                    Opt("บำบัดด้วยสารเภสัชรังสี", "Med Bay รักษา 4 คน · รังสี −25/วัน (−35 ถ้าตอบควิซแล้ว)", "Mira: \"เห็นก่อนถึงรักษาถูกจุด\"", "nuclear_medicine",
                        e => { /* heal via Med Bay — deferred to the building system (Sprint 5/6) */ }),
                });

            // 3 · spoil (irradiation)
            Write(CardIds.Spoil, "เสบียงเน่าเพราะรังสี",
                "คลังอาหารเน่าเร็วกว่าปกติ\nรังสีพื้นหลังสูงผิดปกติ",
                new[] { "Dorn(worried): \"ข้าวเน่าเร็วกว่าปกติสามเท่า ผมทำนามาสามสิบปี ไม่เคยเจอแบบนี้\"" }, 6, false,
                new[]
                {
                    Opt("เร่งเก็บเกี่ยวก่อนเน่า", "ดึงคน 2 คนมาช่วย 2 วัน · ได้อาหารบางส่วน", "", "",
                        e => { e.food = 15f; }),
                    Opt("ลดปันส่วน ยืดของที่มี", "อาหาร ×0.7 · ทุกคนหิว · Hope −6", "", "",
                        e => { e.foodMult = 0.7f; e.hungerAll = 10f; e.hopeDelta = -6f; }),
                    Opt("ฉายรังสีถนอมด้วยโคบอลต์-60", "อัตราเน่า ×0.3 ถาวร · พลังงาน −50", "Dorn: \"ผมยังไม่สบายใจ แต่ยุ้งไม่ว่างแล้ว ก็เอาเถอะ\"", "irradiation",
                        e => { e.spoilMult = 0.3f; e.power = -50f; }),
                });

            // 4 · hunger (food_logistics) — Dorn vs Kova clash
            Write(CardIds.Hunger, "วิกฤตเสบียง · ยุ้งใกล้ว่าง",
                "คนงาน 3 คนไม่ได้กินมา 2 วัน\nประสิทธิภาพตกลง 40% · ขวัญเมืองลด",
                new[]
                {
                    "Dorn(angry): \"ยุ้งเหลือไม่ถึงสามวัน คุณดึงคนของผมไปหมดแล้ว\"",
                    "Kova(serious): \"เหมืองก็ขาดคนเหมือนกัน ไม่มีเหล็กก็ไม่มีคอยล์\"",
                    "Dorn(angry): \"เหล็กมันรอได้ ท้องคนมันรอไม่ได้\"",
                }, 4, false,
                new[]
                {
                    Opt("ดึงคนกลับฟาร์มทันที", "+3 ชาวนา · วิจัย/เหมืองหยุด 3 วัน", "Kova: \"งั้นเหมืองหยุด สามวัน จำไว้ว่าใครสั่ง\"", "",
                        e => { e.farmersReturned = 3; }),
                    Opt("ปันส่วนครึ่งเดียว", "อาหาร +20 · ความหิว −20 ทุกคน · ประสิทธิภาพ −25% · Hope −6", "Dorn: \"ทุกคนได้ครึ่งเดียว รวมคนของคุณด้วยนะ Kova\"", "",
                        e => { e.food = 20f; e.hungerAll = -20f; e.hopeDelta = -6f; }),
                    Opt("เปิดคลังสำรอง", "อาหาร +80 ทันที · ไม่เสียคน ไม่เสีย Hope", "Dorn: \"ดีที่เราเก็บไว้\"", "food_logistics",
                        e => { e.food = 80f; }),
                });

            // 5 · overwork (shift_management) — ★ locked option is A · Kova vs Mira clash
            Write(CardIds.Overwork, "แรงงานหมดสภาพ",
                "คนงาน 3 คนยืนหลับคาเครื่อง\nความล้าเกิน 85 · หยุดงานเอง",
                new[]
                {
                    "Kova: \"คนของนายยืนหลับคาเครื่องแล้ว จะให้ฉันทำยังไง\"",
                    "Mira: \"เมื่อวานอีกคนเดินชนเสา ไม่ใช่คนซุ่มซ่ามนะ คนมันไม่ได้นอน\"",
                    "Kova: \"ถ้าหยุด CORE ก็ค้าง\"",
                    "Mira: \"คนพลาดตอนคุมหล่อเย็นน่ะ Kova มันไม่ได้จบแค่เท้าเจ็บ\"",
                }, 4, false,
                new[]
                {
                    Opt("บังคับพัก 2 วัน", "ความล้า −60 ทุกคน · Hope +3 · ผลผลิตหยุด 2 วัน", "Mira: \"พวกเขาต้องการแค่นี้แหละ\"", "shift_management",
                        e => { e.fatigueAll = -60f; e.hopeDelta = 3f; e.coreStallDays = 2; }),
                    Opt("อัดงานต่อ", "ได้งานต่อ · 30% ต่อคนบาดเจ็บ (รังสี +20) · Hope −8", "Mira: \"ฉันเตรียมเตียงไว้แล้ว\"", "",
                        e => { e.radiationTargets = 3; e.radiationAmount = 20f; e.hopeDelta = -8f; }),
                });

            // 6 · zoneb (no lock) — Kova vs Mira clash
            Write(CardIds.ZoneB, "Zone B ไม่มีคน",
                "คนที่เข้าไปรับรังสีจนต้องถอนออกหมด\nTritium หยุดผลิต · CORE% จะค้าง",
                new[]
                {
                    "Kova: \"Tritium หยุด CORE ก็ค้างที่แปดสิบ ต้องมีคนเข้าไป\"",
                    "Mira: \"คนที่ออกมาเมื่อวาน รังสีสะสมสามสิบสอง เขาเข้าไปอีกไม่ได้แล้ว\"",
                    "Kova: \"งั้นส่งคนใหม่\"",
                    "Mira: \"แล้วพอคนใหม่ถึงสามสิบสอง ฉันควรส่งใครต่อ\"",
                }, 4, false,
                new[]
                {
                    Opt("หมุนคนใหม่เข้าไป", "ส่งคนที่รังสียังต่ำ 2 คน (มีชุด: อยู่ได้ 8 วัน · ไม่มีชุด: 3.5 วัน)", "Mira: \"ฉันจดชื่อไว้แล้ว เผื่อคุณอยากรู้ทีหลัง\"", "",
                        e => { e.radiationTargets = 2; e.radiationAmount = 15f; }),
                    Opt("หยุด Zone B 2 วัน", "Tritium หยุด · CORE% ค้าง · Hope −4 · รอให้คนฟื้น", "Kova: \"สองวัน... เตาไม่ได้รอเรานะ\"", "",
                        e => { e.stopZoneBDays = 2; e.coreStallDays = 2; e.hopeDelta = -4f; }),
                });

            // 7 · triage (no lock — ethical fork) — Kova vs Mira clash
            Write(CardIds.Triage, "เตียงไม่พอ",
                "คนไข้ 6 คน · เตียงมี 4\nต้องเลือกว่าใครได้รักษา",
                new[]
                {
                    "Mira: \"ที่พยาบาลเต็ม คนไข้ล้นออกมาข้างนอก\"",
                    "Kova: \"เอาคนเหมืองก่อน ไม่มีเหล็กก็ไม่มีอะไรทั้งนั้น\"",
                    "Mira: \"คุณเลือกจากงานที่เขาทำเหรอ\"",
                    "Kova: \"ฉันไม่ได้เลือก ฉันบอกว่าเมืองต้องการอะไร\"",
                }, 6, false,
                new[]
                {
                    // A intentionally silent (CARDS.md #7A) — the player does as Kova says and no one speaks
                    Opt("รักษาคนงานเหมืองก่อน", "แร่ไหลต่อ · นักวิจัยตาย 1 · วิจัยช้าลง", "", "",
                        e => { e.hopeDelta = -6f; }),
                    Opt("รักษานักวิจัยก่อน", "วิจัยเดินหน้า · คนงานตาย 2 · Hope −16", "Kova: \"นายไม่ได้เลือกตามที่ฉันบอกนะ จำไว้\"", "",
                        e => { e.hopeDelta = -16f; }),
                    Opt("แบ่งครึ่ง", "ทุกคนหายช้า · ไม่มีใครตาย · เสียเวลา 3 วัน", "Mira: \"ทุกคนหายช้า แต่ทุกคนยังอยู่\"", "",
                        e => { e.hopeDelta = -4f; }),
                });

            // 8 · decree (no lock, once per game — ethical fork, Mira alone)
            Write(CardIds.Decree, "ประกาศฉุกเฉิน",
                "ระบบหล่อเย็นต้องการคนเพิ่มด่วน\nมีทางที่ได้ผล แต่ขัดกับสิ่งที่ควรทำ",
                new[] { "Mira: \"เราสร้างหอคอยเสร็จไปเพื่ออะไร ถ้าคนที่ต้องใช้มันไม่เหลือ\"" }, 99, true,
                new[]
                {
                    Opt("ไม่ออกประกาศ", "ไม่ได้แรงงานเพิ่ม · Hope คงที่ · ยากขึ้น แต่รักษาศักดิ์ศรีเมือง", "ชาวเมือง: \"ขอบคุณที่ไม่เอาลูกผมไป\"", "",
                        e => { /* no free lunch, but this is the hard, dignified road — no immediate cost */ }),
                    Opt("เกณฑ์ผู้ป่วยร่วมงาน", "+แรงงานหล่อเย็น · Hope −8 ทันที, −3/วัน · ผู้ป่วยเสี่ยงตาย 20%/วัน", "Mira: \"เราชนะพายุไปทำไม ถ้าไม่เหลือใครให้ช่วย\"", "",
                        e => { e.hopeDelta = -8f; e.hopePerDay = -3f; e.hopePerDayDays = 3; }),
                    Opt("ดึงแรงงานเด็ก", "+แรงงานหล่อเย็น · Hope −15 ทันที, −4/วัน", "ชาวเมือง: \"เด็กพวกนั้นไม่ควรต้องอยู่ตรงนั้น\"", "",
                        e => { e.hopeDelta = -15f; e.hopePerDay = -4f; e.hopePerDayDays = 3; }),
                });

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[CrisisCardsSetup] สร้าง/อัปเดต CrisisCardSO 8 ใบ ที่ " + Folder);
        }

        private static CardOption Opt(string label, string summary, string after, string reqNote, Action<CardEffect> fill)
        {
            var o = new CardOption
            {
                label = label,
                effectSummary = summary,
                afterText = after ?? "",
                requiredNoteId = reqNote ?? "",
                effect = new CardEffect(),
            };
            fill?.Invoke(o.effect);
            return o;
        }

        private static void Write(string cardId, string title, string description, string[] dialogue,
                                  int cooldownDays, bool onceOnly, CardOption[] options)
        {
            string path = $"{Folder}/{cardId}.asset";
            var card = AssetDatabase.LoadAssetAtPath<CrisisCardSO>(path);
            bool isNew = card == null;
            if (isNew) card = ScriptableObject.CreateInstance<CrisisCardSO>();

            card.cardId = cardId;
            card.title = title;
            card.description = description;
            card.dialogueLines = dialogue;
            card.cooldownDays = cooldownDays;
            card.onceOnly = onceOnly;
            card.options = options;

            if (isNew) AssetDatabase.CreateAsset(card, path);
            else EditorUtility.SetDirty(card);
        }
    }
}
