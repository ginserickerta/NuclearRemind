using UnityEditor;
using UnityEngine;

namespace NuclearReMind.EditorTools
{
    /// <summary>
    /// Bake the three code-built card panels (CrisisCardPanelUI / QuizCardPanelUI / NoteCardPopup)
    /// into prefabs the owner can restyle by hand in the Editor — same pattern as CoreTowerPanelBaker.
    ///
    ///   • Output: Assets/Resources/CardUI/&lt;Name&gt;.prefab — all refs wired via [SerializeField].
    ///   • Runtime: each panel's AutoSpawn Instantiates its prefab when present; no prefab → the old
    ///     code-build runs, so deleting a prefab is always a safe way back to the stock look.
    ///   • Re-baking OVERWRITES the prefab — hand edits are lost. Bake once, then edit by hand.
    /// </summary>
    public static class CardPanelBaker
    {
        private const string Dir = "Assets/Resources/CardUI";

        [MenuItem("NuclearReMind/UI/Bake Card Panels (Crisis + Quiz + Note + Codex)")]
        public static void BakeAll()
        {
            BakeCrisis();
            BakeQuiz();
            BakeNote();
            BakeCodex();
        }

        [MenuItem("NuclearReMind/UI/Bake Crisis Card Panel")]
        public static void BakeCrisis()
            => Bake<CrisisCardPanelUI>("CrisisCardPanel", c => c.BuildForBake());

        [MenuItem("NuclearReMind/UI/Bake Quiz Card Panel")]
        public static void BakeQuiz()
            => Bake<QuizCardPanelUI>("QuizCardPanel", c => c.BuildForBake());

        [MenuItem("NuclearReMind/UI/Bake Note Card Popup")]
        public static void BakeNote()
            => Bake<NoteCardPopup>("NoteCardPopup", c => c.BuildForBake());

        [MenuItem("NuclearReMind/UI/Bake Codex Panel")]
        public static void BakeCodex()
            => Bake<CodexPanelUI>("CodexPanel", c => c.BuildForBake());

        private static void Bake<T>(string name, System.Action<T> build) where T : Component
        {
            if (!AssetDatabase.IsValidFolder(Dir))
                AssetDatabase.CreateFolder("Assets/Resources", "CardUI");

            string path = $"{Dir}/{name}.prefab";
            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) != null &&
                !EditorUtility.DisplayDialog("Bake " + name,
                    $"มี {name}.prefab อยู่แล้ว — bake ทับจะล้างการแก้ด้วยมือทั้งหมด\nทับเลยหรือไม่?",
                    "ทับเลย", "ยกเลิก"))
            {
                Debug.Log($"[CardPanelBaker] ข้าม {name} (ผู้ใช้ยกเลิก)");
                return;
            }

            // Root carries a RectTransform so the prefab previews properly in prefab mode; at runtime
            // each panel's AutoSpawn stretches it over the canvas.
            var go = new GameObject(name, typeof(RectTransform));
            try
            {
                var comp = go.AddComponent<T>();
                build(comp);

                var prefab = PrefabUtility.SaveAsPrefabAsset(go, path);
                if (prefab != null)
                {
                    EditorGUIUtility.PingObject(prefab);
                    Debug.Log($"[CardPanelBaker] ✅ {path} — แก้ layout/สี/ฟอนต์ในไฟล์นี้ได้เลย " +
                              "(ลบ prefab = กลับไปใช้หน้าตาที่โค้ดสร้าง)");
                }
                else Debug.LogError($"[CardPanelBaker] ❌ SaveAsPrefabAsset ล้มเหลว: {path}");
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }
    }
}
