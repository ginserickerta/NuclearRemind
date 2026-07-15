using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace NuclearReMind.EditorTools
{
    /// <summary>
    /// สร้าง prefab "ฝั่งซ้ายของกล่องบทสนทนา" = portrait Auren + กล่องบทพูด (DialogueBox: name plate/ข้อความ/hint)
    /// → แก้ตำแหน่ง/ขนาด/หน้าตาด้วยตาได้ · StoryUISetup จะ Instantiate + wire ref จาก prefab นี้
    /// **แก้แล้วไม่หายตอน re-run "Setup Story UI"**
    ///
    /// ลูก (ชื่อคงที่ — StoryUISetup ค้นด้วยชื่อ): DialoguePortraitAuren · DialogueBox
    ///   → DialogueBox/DialogueNamePlate/DialogueName · DialogueBox/DialogueBody · DialogueBox/DialogueHint
    ///
    /// วิธีใช้: รันเมนู → ดับเบิลคลิก prefab เปิด Prefab Mode → แก้ → Save → รัน "Setup Story UI"
    /// หมายเหตุ: ตอนเล่นจริง controller สลับ sprite ตามอารมณ์ + สีแถบชื่อ + กรอบกล่องตามความยาว เอง
    ///   (prefab คุม ตำแหน่ง/ขนาด/anchor/โครง · ค่าพวกนั้นเป็นพรีวิว)
    /// ⚠ รันซ้ำ = rebuild ตัวเต็มทับ (งานที่แก้มือใน prefab หาย) → แก้ prefab หลัง bake ครั้งเดียว
    /// </summary>
    public static class DialogueAurenPortraitBaker
    {
        public const string PrefabPath = "Assets/Prefabs/UI/DialogueAurenPortrait.prefab";
        private const string AurenSprite = "Assets/Sprites/Characters/Auren/auren_thinking.png";
        private const string FrameMedium = "Assets/Resources/DialogueUI/frame_medium.png";

        [MenuItem("NuclearReMind/UI/Bake Auren Portrait Prefab")]
        public static void Bake()
        {
            Directory.CreateDirectory("Assets/Prefabs/UI");
            var font = AssetDatabase.LoadAssetAtPath<Font>("Assets/Resources/Fonts/Kanit-Regular.ttf");
            if (font == null) font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            // ── root container (สตรตช์เต็ม overlay — ลูกใช้ anchor ของตัวเอง) ──
            var root = new GameObject("DialogueLeft", typeof(RectTransform));
            var rootRect = root.GetComponent<RectTransform>();
            rootRect.anchorMin = Vector2.zero; rootRect.anchorMax = Vector2.one;
            rootRect.offsetMin = Vector2.zero; rootRect.offsetMax = Vector2.zero;

            // ── portrait Auren (ล่างซ้าย) ──
            var portrait = new GameObject("DialoguePortraitAuren", typeof(RectTransform));
            portrait.transform.SetParent(root.transform, false);
            var pRect = portrait.GetComponent<RectTransform>();
            pRect.anchorMin = pRect.anchorMax = new Vector2(0f, 0f);
            pRect.pivot = new Vector2(0f, 0f);
            pRect.anchoredPosition = new Vector2(16f, 0f);
            pRect.sizeDelta = new Vector2(480f, 560f);
            var pImg = portrait.AddComponent<Image>();
            pImg.preserveAspect = true;
            pImg.raycastTarget = false;
            var aSprite = AssetDatabase.LoadAssetAtPath<Sprite>(AurenSprite);
            if (aSprite != null) pImg.sprite = aSprite; else pImg.color = new Color(0.72f, 0.68f, 0.85f);

            // ── กล่องบทพูด (โครงเดียวกับ StoryUISetup) ──
            var box = new GameObject("DialogueBox", typeof(RectTransform));
            box.transform.SetParent(root.transform, false);
            var boxRect = box.GetComponent<RectTransform>();
            boxRect.anchorMin = boxRect.anchorMax = new Vector2(0f, 0f);
            boxRect.pivot = new Vector2(0f, 0f);
            boxRect.anchoredPosition = new Vector2(48f, 56f);
            boxRect.sizeDelta = new Vector2(900f, 240f);
            var boxImg = box.AddComponent<Image>();
            boxImg.raycastTarget = false;
            var frameMed = AssetDatabase.LoadAssetAtPath<Sprite>(FrameMedium);
            if (frameMed != null) { boxImg.sprite = frameMed; boxImg.type = Image.Type.Simple; boxImg.color = Color.white; }
            else boxImg.color = new Color(0.08f, 0.09f, 0.13f, 0.95f);

            // name plate + ชื่อ
            var plateGO = new GameObject("DialogueNamePlate", typeof(RectTransform));
            plateGO.transform.SetParent(box.transform, false);
            var plateRect = plateGO.GetComponent<RectTransform>();
            plateRect.anchorMin = plateRect.anchorMax = new Vector2(0f, 1f);
            plateRect.pivot = new Vector2(0f, 1f);
            plateRect.anchoredPosition = new Vector2(52f, -34f);
            plateRect.sizeDelta = new Vector2(200f, 40f);
            var plate = plateGO.AddComponent<Image>();
            plate.raycastTarget = false;
            plate.color = new Color(0.3f, 0.7f, 0.95f);

            var nameText = MakeText("DialogueName", plateGO.transform, font, "Auren", 21, TextAnchor.MiddleLeft);
            var nameRect = nameText.GetComponent<RectTransform>();
            nameRect.anchorMin = Vector2.zero; nameRect.anchorMax = Vector2.one;
            nameRect.offsetMin = new Vector2(16, 0); nameRect.offsetMax = new Vector2(-10, 0);
            nameText.fontStyle = FontStyle.Bold;
            nameText.raycastTarget = false;
            nameText.color = new Color(0.06f, 0.07f, 0.10f);

            // เนื้อความ (best-fit)
            var body = MakeText("DialogueBody", box.transform, font, "", 24, TextAnchor.UpperLeft);
            var bodyRect = body.GetComponent<RectTransform>();
            bodyRect.anchorMin = Vector2.zero; bodyRect.anchorMax = Vector2.one;
            bodyRect.offsetMin = new Vector2(58, 46); bodyRect.offsetMax = new Vector2(-58, -86);
            body.color = new Color(0.93f, 0.94f, 0.88f);
            body.lineSpacing = 1.2f;
            body.raycastTarget = false;
            body.horizontalOverflow = HorizontalWrapMode.Wrap;
            body.verticalOverflow = VerticalWrapMode.Truncate;
            body.resizeTextForBestFit = true;
            body.resizeTextMinSize = 14;
            body.resizeTextMaxSize = 26;

            // hint คลิกต่อ
            var hint = MakeText("DialogueHint", box.transform, font, "▼ คลิกเพื่อไปต่อ", 15, TextAnchor.LowerRight);
            var hintRect = hint.GetComponent<RectTransform>();
            hintRect.anchorMin = new Vector2(1f, 0f); hintRect.anchorMax = new Vector2(1f, 0f);
            hintRect.pivot = new Vector2(1f, 0f);
            hintRect.anchoredPosition = new Vector2(-46, 20);
            hintRect.sizeDelta = new Vector2(220, 22);
            hint.raycastTarget = false;
            hint.color = new Color(0.75f, 0.72f, 0.62f, 0.85f);

            var prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Object.DestroyImmediate(root);

            Selection.activeObject = prefab;
            EditorGUIUtility.PingObject(prefab);
            Debug.Log("[DialogueAurenPortraitBaker] ✅ สร้าง/อัปเดต prefab (portrait + DialogueBox): " + PrefabPath +
                      " — เปิด Prefab Mode แก้ด้วยตา แล้วรัน 'Setup Story UI' (จะ instantiate + wire ให้ · ไม่หาย)");
        }

        private static Text MakeText(string name, Transform parent, Font font, string content, int size, TextAnchor anchor)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var t = go.AddComponent<Text>();
            t.font = font; t.fontSize = size; t.text = content; t.alignment = anchor; t.color = Color.white;
            return t;
        }
    }
}
