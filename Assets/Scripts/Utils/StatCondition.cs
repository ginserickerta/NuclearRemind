namespace NuclearReMind
{
    /// <summary>
    /// ตัวประเมินเงื่อนไขสถิติแบบข้อความ (ใช้ร่วมระหว่าง DilemmaManager กับ StoryDirector)
    /// syntax เดียวกับ DilemmaData.triggerCondition:
    ///   "heat_above_80" | "q_above_0.3" (Q = CORE%/100) | "food_below_120" | "food_above_500"
    ///   | "energy_below_100" | "water_below_80" | "day_reached_20"
    ///   | "exposure_above_60" (ค่าเสี่ยงรังสีสะสม RadiationManager — Story Guide §4 วิกฤตโรครังสี)
    /// เชื่อมหลายเงื่อนไขแบบ "อย่างใดอย่างหนึ่ง" ด้วย '|' เช่น "exposure_above_60|day_reached_23"
    /// เงื่อนไขที่ไม่รู้จัก = false (ปลอดภัยกว่ายิงมั่ว)
    /// </summary>
    public static class StatCondition
    {
        /// <summary>ประเมินเงื่อนไข ณ จบวัน (day) กับ snapshot ทรัพยากร/เตา/รังสีที่ caller แคชไว้จาก event
        /// radiationExposure เป็น optional (default 0) — caller เดิมที่ไม่ส่งจะประเมิน exposure_above_* เป็น false</summary>
        public static bool Matches(string condition, int day, ResourceData resources, TowerData tower,
            float radiationExposure = 0f)
        {
            if (string.IsNullOrEmpty(condition)) return false;

            // "a|b" = เข้าเงื่อนไขอย่างใดอย่างหนึ่ง (V4 §10 วิกฤต 1: "heat_above_80|q_above_0.3")
            if (condition.IndexOf('|') >= 0)
            {
                foreach (var part in condition.Split('|'))
                    if (Matches(part, day, resources, tower, radiationExposure)) return true;
                return false;
            }

            if (TryThreshold(condition, "heat_above_",     out float h))  return tower.coreHeat     >= h;
            if (TryThreshold(condition, "q_above_",        out float q))  return tower.corePercent  >= q * 100f; // Q = CORE%/100 (§8)
            if (TryThreshold(condition, "food_below_",     out float fb)) return resources.food     <= fb;
            if (TryThreshold(condition, "food_above_",     out float fa)) return resources.food     >= fa;
            if (TryThreshold(condition, "energy_below_",   out float e))  return resources.energy   <= e;
            if (TryThreshold(condition, "water_below_",    out float w))  return resources.water    <= w;
            if (TryThreshold(condition, "day_reached_",    out float d))  return day                >= d;
            if (TryThreshold(condition, "exposure_above_", out float ex)) return radiationExposure  >= ex;
            return false;
        }

        /// <summary>แกะตัวเลขท้าย prefix เช่น ("hope_below_30", "hope_below_") → 30 · InvariantCulture ("0.3" ไม่ขึ้นกับเครื่อง)</summary>
        public static bool TryThreshold(string condition, string prefix, out float value)
        {
            value = 0f;
            return !string.IsNullOrEmpty(condition)
                && condition.StartsWith(prefix)
                && float.TryParse(condition.Substring(prefix.Length),
                       System.Globalization.NumberStyles.Float,
                       System.Globalization.CultureInfo.InvariantCulture, out value);
        }
    }
}
