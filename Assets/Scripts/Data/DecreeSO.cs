using UnityEngine;

namespace NuclearReMind
{
    /// <summary>
    /// ประกาศฉุกเฉิน (V4 §11) — ให้ "แรงงานหล่อเย็นเพิ่ม" แลกกับ Hope ที่ดิ่ง (ช่วง Day 25–30)
    /// ผูกควิซ Q10 (จริยธรรม ALARA)
    /// </summary>
    [CreateAssetMenu(fileName = "NewDecree", menuName = "NuclearReMind/Decree")]
    public class DecreeSO : ScriptableObject
    {
        public string id;
        public string title;

        [Header("Effect (V4 §11)")]
        public int coolingLaborGain;   // +หล่อเย็นเข้าสูตรเตา (§8)
        public float hopeImmediate;    // Hope ทันที (ค่าลบ)
        public float hopePerDay;       // Hope ต่อวันระหว่างใช้ (ค่าลบ)

        [Header("Linked Quiz (Q10)")]
        public string[] linkedQuizIds;
    }
}
