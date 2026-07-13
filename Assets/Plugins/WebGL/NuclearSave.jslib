// WebGL save persistence — flush Unity persistentDataPath (MEMFS) ลง IndexedDB
// เรียกจาก SaveManager.Save() หลัง File.WriteAllText (ผ่าน [DllImport("__Internal")] NuclearSyncFs)
// ไม่มีบล็อกนี้ = เซฟหายเมื่อรีเฟรช/ปิดแท็บ (persistentDataPath อยู่ใน MEMFS ชั่วคราวจนกว่าจะ syncfs)
mergeInto(LibraryManager.library, {
  NuclearSyncFs: function () {
    try {
      FS.syncfs(false, function (err) {
        if (err) { console.warn('[NuclearSave] syncfs error:', err); }
      });
    } catch (e) {
      console.warn('[NuclearSave] syncfs exception:', e);
    }
  }
});
