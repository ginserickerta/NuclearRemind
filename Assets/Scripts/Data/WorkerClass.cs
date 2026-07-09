namespace NuclearReMind
{
    /// <summary>
    /// คลาสแรงงาน (V4 §5) — อาคารระบุคลาสที่ต้องใช้ผ่าน BuildingData.requiredClass
    /// Worker = 0 → asset เดิมทุกตัว (ที่ยังไม่ตั้งค่า) default เป็น Worker โดยอัตโนมัติ
    /// </summary>
    public enum WorkerClass
    {
        Worker = 0,  // ผลิต/ขุด — โรงไฟ/น้ำ/อาหารพื้นฐาน/เหมือง/โหนดแร่
        Engineer,    // วิจัย/CORE/หล่อเย็น — Lab, CORE TOWER
        Medic,       // รักษา/กันรังสี — Hospital
        Farmer,      // เพาะปลูก — Farm, Agri Dome
    }
}
