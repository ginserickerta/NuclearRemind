using UnityEngine;

namespace NuclearReMind
{
    /// <summary>
    /// ผู้พูดในบทสนทนา (v8.5) — NPC สามคน + เสียงในใจผู้เล่น (Auren) + ระบบ
    /// Kova/Mira/Dorn = มี portrait (โผล่ในช่องสนทนาซ้าย/ขวา) · InnerVoice/System = ข้อความกลาง ไม่มี portrait
    /// </summary>
    /// ★ Citizen ต่อท้ายเมื่อ bark ย้ายมาใช้แผงนี้ (BARKS.md มีผู้พูด "ชาวเมือง") — ต่อท้ายเท่านั้น
    /// ห้ามแทรกกลาง ไม่งั้น DialogueLine ที่ serialize ไว้ใน asset เดิมจะเปลี่ยนผู้พูดยกชุด
    public enum Speaker { System, InnerVoice, Kova, Mira, Dorn, Citizen }

    /// <summary>
    /// อารมณ์สีหน้าของตัวละครที่มี portrait จริง (ตอนนี้ Kova) — เลือกภาพ portrait ตามอารมณ์ของประโยค
    /// index ตรงกับ DialogueUIController.kovaEmotionSprites · ต่อท้ายได้เท่านั้น (ห้ามแทรกกลาง)
    /// Neutral=อกอ้อมมั่นใจ · Happy=ยิ้มร่า · Worried=กังวล · Serious=จริงจัง · Explain=อธิบาย(พิมพ์เขียว)
    /// Excited=ฮึด · Welding=เชื่อม · Sad=เสียใจ · Proud=ยกนิ้วโป้ง
    /// (Auren) Thinking=คิด · Surprised=ตกใจ · Determined=มุ่งมั่น/ชี้นิ้ว
    /// </summary>
    public enum Emotion { Neutral, Happy, Worried, Serious, Explain, Excited, Welding, Sad, Proud, Thinking, Surprised, Determined }

    /// <summary>
    /// บทพูดหนึ่งบรรทัด — ผู้พูด + ข้อความ (v8.5 บทสนทนาหลายตัวละครสลับกัน)
    /// เรียงเป็น DialogueLine[] ใน StoryBeatSO (dialoguePre ก่อนวิกฤต / dialoguePost หลัง outcome)
    /// emotion ใช้เฉพาะผู้พูดที่มี portrait จริง (Kova) — default Neutral (เซฟ/บทเก่าที่ไม่ตั้งค่าก็ได้)
    /// </summary>
    [System.Serializable]
    public struct DialogueLine
    {
        public Speaker speaker;
        [TextArea(1, 3)] public string textTH;
        public Emotion emotion;
    }

    /// <summary>
    /// เมทาดาทาผู้พูด (ชื่อ/สี/ฝั่ง) — ใช้ร่วมกันระหว่าง StoryDirector (toast fallback)
    /// และ DialogueUIController (portrait + บอลลูน) เพื่อไม่ให้ตารางแยกสองที่แล้วหลุดกัน
    /// </summary>
    public static class SpeakerMeta
    {
        // ใช้ชื่อโรมันตามที่โค้ดเดิมใช้ (npcLinePre "Kova: ...") — ไม่เดาสะกดไทย
        public static string DisplayName(Speaker s)
        {
            switch (s)
            {
                case Speaker.Kova:       return "Kova";
                case Speaker.Mira:       return "Mira";
                case Speaker.Dorn:       return "Dorn";
                case Speaker.Citizen:    return "ชาวเมือง";
                case Speaker.InnerVoice: return "เสียงในใจ";
                case Speaker.System:     return "ระบบ";
                default:                 return s.ToString();
            }
        }

        /// <summary>
        /// Name → Speaker. Case-insensitive because the content assets are not consistent: research notes
        /// spell it "KOVA", crisis cards spell it "Kova". Anything unrecognised becomes System rather than
        /// silently drawing the wrong character's portrait.
        /// </summary>
        public static Speaker Parse(string raw)
        {
            if (string.IsNullOrEmpty(raw)) return Speaker.System;
            switch (raw.Trim().ToUpperInvariant())
            {
                case "KOVA":       return Speaker.Kova;
                case "MIRA":       return Speaker.Mira;
                case "DORN":       return Speaker.Dorn;
                case "AUREN":
                case "INNERVOICE": return Speaker.InnerVoice;
                case "CITIZEN":    return Speaker.Citizen;
                default:           return Speaker.System;
            }
        }

        /// <summary>
        /// Parse one authored line in the crisis-card format: `Kova: "…"`. The surrounding quotes are
        /// stripped because the dialogue box draws its own speech frame. A line with no `Name:` prefix is
        /// kept verbatim as a System line rather than being dropped.
        /// ★ 2026-07-23: `Name(emotion): "…"` picks the portrait face — e.g. `Dorn(sad): "…"`.
        /// Unknown/absent emotion falls back to Neutral, so old content parses exactly as before.
        /// </summary>
        public static DialogueLine FromPrefixedLine(string raw)
        {
            var line = new DialogueLine { speaker = Speaker.System, emotion = Emotion.Neutral, textTH = raw };
            if (string.IsNullOrEmpty(raw)) return line;

            int colon = raw.IndexOf(':');
            string body = raw;
            if (colon > 0)
            {
                string prefix = raw.Substring(0, colon).Trim();

                // optional `(emotion)` suffix on the name — split it off before matching the speaker
                var emotion = Emotion.Neutral;
                int paren = prefix.IndexOf('(');
                if (paren > 0 && prefix.EndsWith(")"))
                {
                    emotion = ParseEmotion(prefix.Substring(paren + 1, prefix.Length - paren - 2));
                    prefix = prefix.Substring(0, paren).Trim();
                }

                var candidate = Parse(prefix);
                if (candidate != Speaker.System)      // only treat it as a prefix if the name is real
                {
                    line.speaker = candidate;
                    line.emotion = emotion;
                    body = raw.Substring(colon + 1);
                }
            }

            body = body.Trim();
            if (body.Length >= 2 && (body[0] == '"' || body[0] == '“') &&
                                    (body[body.Length - 1] == '"' || body[body.Length - 1] == '”'))
                body = body.Substring(1, body.Length - 2);

            line.textTH = body.Trim();
            return line;
        }

        /// <summary>Emotion-name → enum for the `Name(emotion):` tag. Unknown = Neutral (never throws).</summary>
        public static Emotion ParseEmotion(string raw)
        {
            if (string.IsNullOrEmpty(raw)) return Emotion.Neutral;
            switch (raw.Trim().ToUpperInvariant())
            {
                case "HAPPY":      return Emotion.Happy;
                case "WORRIED":    return Emotion.Worried;
                case "SERIOUS":
                case "ANGRY":      return Emotion.Serious;   // Dorn's angry art rides the Serious slot
                case "EXPLAIN":    return Emotion.Explain;
                case "EXCITED":    return Emotion.Excited;
                case "WELDING":    return Emotion.Welding;
                case "SAD":        return Emotion.Sad;
                case "PROUD":      return Emotion.Proud;
                case "THINKING":   return Emotion.Thinking;
                case "SURPRISED":  return Emotion.Surprised;
                case "DETERMINED": return Emotion.Determined;
                default:           return Emotion.Neutral;
            }
        }

        // อักษรย่อบน placeholder portrait (จนกว่าจะมีภาพจริงมาสลับ)
        public static string Initial(Speaker s)
        {
            switch (s)
            {
                case Speaker.Kova: return "K";
                case Speaker.Mira: return "M";
                case Speaker.Dorn: return "D";
                default:           return "?";
            }
        }

        // สีเอกลักษณ์ต่อคน — ใช้ทั้ง portrait placeholder และแถบชื่อ
        public static Color AccentColor(Speaker s)
        {
            switch (s)
            {
                case Speaker.Kova:       return new Color(0.30f, 0.70f, 0.95f); // ฟ้า-วิศวกร
                case Speaker.Mira:       return new Color(0.95f, 0.45f, 0.70f); // ชมพู-หมอ
                case Speaker.Dorn:       return new Color(0.70f, 0.80f, 0.35f); // เขียว-เกษตร
                case Speaker.Citizen:    return new Color(0.72f, 0.70f, 0.66f); // เทาอุ่น-ไม่มีชื่อ
                case Speaker.InnerVoice: return new Color(0.72f, 0.68f, 0.85f); // ม่วงจาง-ความคิด
                case Speaker.System:     return new Color(1.00f, 0.55f, 0.30f); // ส้ม-เตือน
                default:                 return Color.white;
            }
        }

        // เฉพาะสามคนนี้มี portrait ในช่องสนทนา (VN ซ้าย/ขวา) — เสียงในใจ/ระบบ = ข้อความกลาง
        public static bool HasPortrait(Speaker s)
            => s == Speaker.Kova || s == Speaker.Mira || s == Speaker.Dorn;

        // คำนำหน้าเวลาลด degrade เป็น toast (ยังไม่มี Dialogue UI)
        public static string Prefix(Speaker s)
        {
            switch (s)
            {
                case Speaker.InnerVoice: return "▸ ความคิด: ";
                case Speaker.System:     return "[ระบบ] ";
                default:                 return DisplayName(s) + ": ";
            }
        }
    }
}
