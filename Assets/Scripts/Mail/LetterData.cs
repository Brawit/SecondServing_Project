using System;
using System.Collections.Generic;
using UnityEngine;

// [2.5c] ของแนบในจดหมาย (ผู้เล่นต้องกดรับในหน้าจดหมายถึงจะได้)
// หมายเหตุ: เพิ่มค่าใหม่ต่อท้ายเท่านั้น ห้ามสลับลำดับ
public enum LetterAttachmentKind
{
    Recipe, // สูตรอาหาร (รับ = เรียนรู้สูตร)
    Item,   // ไอเทม/อาหาร (รับ = เข้ากระเป๋า)
    Money   // [2.6] เงิน (รับ = เข้ากระเป๋าเงิน)
}

[Serializable]
public class LetterAttachment
{
    public LetterAttachmentKind kind = LetterAttachmentKind.Item;

    [Tooltip("ใช้เมื่อ Kind = Recipe")]
    public RecipeData recipe;

    [Tooltip("ใช้เมื่อ Kind = Item")]
    public ItemData item;

    [Tooltip("จำนวน (ใช้เมื่อ Kind = Item)")]
    [Min(1)] public int amount = 1;

    [Tooltip("[2.6] จำนวนเงิน (ใช้เมื่อ Kind = Money)")]
    [Min(0)] public int money;
}

// [2.5c] เหตุที่ส่งจดหมายเอง (Manual = ระบบอื่นเรียก MailSystem.QueueLetter เอง)
// หมายเหตุ: เพิ่มค่าใหม่ต่อท้ายเท่านั้น / เฟส 3: Festival, Friendship, Support ...
public enum LetterTrigger
{
    Manual,
    PassOut // สลบตอน 03:00 → เช้าถัดไปได้จดหมาย (สุ่ม 1 ฉบับจากจดหมาย PassOut ทั้งหมด)
}

// [2.5c] จดหมาย 1 ฉบับที่ไม่ใช่จดหมายสูตรจากเชฟ เช่น จดหมายตอนสลบ, แจ้งเทศกาล, NPC สนิท/สนับสนุน (เฟส 3)
// จดหมายสูตรจากเชฟไม่ต้องสร้างไฟล์นี้ (มาจากคอลัมน์ Chef / Letter ในตาราง Recipes)
// สร้างได้จาก Assets > Create > Cozy > Letter Data แล้วกด Build Mail System เพื่อให้ MailSystem รู้จัก
[CreateAssetMenu(fileName = "NewLetter", menuName = "Cozy/Letter Data")]
public class LetterData : ScriptableObject
{
    [Header("Save")]
    [Tooltip("รหัสจดหมาย ใช้ในไฟล์เซฟ ต้องไม่ซ้ำ (เว้นว่าง = ตั้งจากชื่อไฟล์)")]
    public string letterId;

    [Header("Sender")]
    [Tooltip("ชื่อผู้ส่ง")]
    public string senderName = "Someone";

    [Tooltip("บรรทัดใต้ชื่อผู้ส่ง เช่น Neighbor / Town Hall")]
    public string senderSubtitle;

    [Header("Content")]
    [Tooltip("หัวข้อสั้นในรายการจดหมาย")]
    public string subject = "A letter";

    [TextArea(6, 14)] public string body;

    [TextArea(2, 4)] public string signOff;

    [Tooltip("ของแนบ (ผู้เล่นกดรับเอง)")]
    public List<LetterAttachment> attachments = new List<LetterAttachment>();

    [Header("Trigger")]
    public LetterTrigger trigger = LetterTrigger.Manual;

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (string.IsNullOrWhiteSpace(letterId) && !string.IsNullOrEmpty(name))
        {
            letterId = name.Trim().ToLowerInvariant().Replace(' ', '_');
            UnityEditor.EditorUtility.SetDirty(this);
        }
    }
#endif
}
