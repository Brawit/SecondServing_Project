// ใช้เฉพาะตอน build เป็นเว็บ: สั่งให้ไฟล์ใน Application.persistentDataPath ถูกบันทึกลง IndexedDB ของเบราว์เซอร์ทันที
// ถ้าไม่สั่ง ไฟล์เซฟอาจหายเมื่อผู้เล่นรีเฟรชหรือปิดแท็บ
mergeInto(LibraryManager.library, {
  SaveSync_FlushToIndexedDB: function () {
    FS.syncfs(false, function (err) {
      if (err) {
        console.error("SaveSync: Failed to flush save to IndexedDB", err);
      }
    });
  }
});
