using BeatSlash.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace BeatSlash.EditorTools
{
    /// <summary>
    /// 튜토리얼 안내 카드 프리팹 생성기 → Assets/Resources/UIPrefabs/TutorialCard.prefab.
    /// 생성 후엔 프리팹을 에디터에서 직접 다듬어도 된다 (재생성하면 덮어씀).
    /// </summary>
    public static class TutorialPrefabBuilder
    {
        const string Dir = "Assets/Resources/UIPrefabs";
        const string PrefabPath = Dir + "/TutorialCard.prefab";

        static readonly Color Gold = new Color(1f, 0.85f, 0.2f);
        static Font _font;

        [MenuItem("BeatSlash/Build Tutorial Card Prefab")]
        public static void Build()
        {
            _font = AssetDatabase.LoadAssetAtPath<Font>("Assets/Resources/Fonts/UIFont.ttf");
            var buttonSprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");

            // 루트: 전체 화면 반투명 어둡게 + 뒤 UI 클릭 차단 (게임 화면이 비쳐 보이게)
            var root = new GameObject(TutorialCard.PrefabName, typeof(RectTransform));
            Stretch(root.GetComponent<RectTransform>());
            root.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.55f);
            var card = root.AddComponent<TutorialCard>();

            var window = MakeImage(root.transform, "Window", Sprite("panel"), new Vector2(1150f, 620f), Vector2.zero);
            if (window.sprite == null) window.color = new Color(0.09f, 0.06f, 0.12f, 0.97f); // 다크 퍼플 폴백
            var w = window.transform;

            card.feedbackText = MakeText(w, "Feedback", "", 28, Color.white, new Vector2(0f, 250f), new Vector2(1000f, 44f));
            card.titleText = MakeText(w, "Title", "", 54, Gold, new Vector2(0f, 185f), new Vector2(1000f, 76f));
            card.bodyText = MakeText(w, "Body", "", 32, Color.white, new Vector2(0f, 10f), new Vector2(1000f, 270f));

            // 버튼 줄: 보조 버튼을 숨기면 주 버튼이 자동으로 가운데
            var row = new GameObject("Buttons", typeof(RectTransform));
            row.transform.SetParent(w, false);
            var rowRt = row.GetComponent<RectTransform>();
            rowRt.sizeDelta = new Vector2(1000f, 80f);
            rowRt.anchoredPosition = new Vector2(0f, -205f);
            var layout = row.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 40f;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = layout.childControlHeight = false;
            layout.childForceExpandWidth = layout.childForceExpandHeight = false;

            (card.secondaryButton, card.secondaryLabel) =
                MakeButton(row.transform, "Secondary", "건너뛰기", buttonSprite, new Color(0.6f, 0.55f, 0.8f, 0.85f));
            (card.primaryButton, card.primaryLabel) =
                MakeButton(row.transform, "Primary", "시작하기", buttonSprite, new Color(1f, 0.7f, 0.22f, 1f));

            MakeText(w, "Hint", "Enter / Space", 22, new Color(1f, 1f, 1f, 0.5f), new Vector2(0f, -275f), new Vector2(400f, 34f));

            if (!AssetDatabase.IsValidFolder(Dir)) AssetDatabase.CreateFolder("Assets/Resources", "UIPrefabs");
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Object.DestroyImmediate(root);
            Debug.Log($"[TutorialPrefabBuilder] {PrefabPath} 생성 완료");
        }

        static Sprite Sprite(string name) =>
            AssetDatabase.LoadAssetAtPath<Sprite>($"Assets/Resources/UI/{name}.png");

        static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }

        static Image MakeImage(Transform parent, string name, Sprite sprite, Vector2 size, Vector2 pos)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var img = go.AddComponent<Image>();
            img.sprite = sprite;
            img.raycastTarget = false;
            var rt = img.rectTransform;
            rt.sizeDelta = size;
            rt.anchoredPosition = pos;
            return img;
        }

        static Text MakeText(Transform parent, string name, string content, int size, Color color, Vector2 pos, Vector2 box)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var t = go.AddComponent<Text>();
            t.font = _font;
            t.text = content;
            t.fontSize = size;
            t.fontStyle = FontStyle.Bold;
            t.color = color;
            t.alignment = TextAnchor.MiddleCenter;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;
            var rt = t.rectTransform;
            rt.sizeDelta = box;
            rt.anchoredPosition = pos;
            return t;
        }

        static (Button, Text) MakeButton(Transform parent, string name, string label, Sprite sprite, Color color)
        {
            var img = MakeImage(parent, "Btn_" + name, sprite, new Vector2(300f, 80f), Vector2.zero);
            img.type = Image.Type.Sliced;
            img.color = color;
            img.raycastTarget = true;
            var btn = img.gameObject.AddComponent<Button>();
            var t = MakeText(img.transform, "Label", label, 34, Color.white, Vector2.zero, Vector2.zero);
            Stretch(t.rectTransform);
            return (btn, t);
        }
    }
}
