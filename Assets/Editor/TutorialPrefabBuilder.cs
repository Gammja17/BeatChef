using BeatSlash.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace BeatSlash.EditorTools
{
    /// <summary>
    /// 튜토리얼(노트 가이드) 프리팹 생성기 → Assets/Resources/UIPrefabs/TutorialPanel.prefab.
    /// 생성 후엔 프리팹을 에디터에서 직접 다듬어도 된다 (재생성하면 덮어씀).
    /// </summary>
    public static class TutorialPrefabBuilder
    {
        const string Dir = "Assets/Resources/UIPrefabs";
        const string PrefabPath = Dir + "/TutorialPanel.prefab";

        static readonly Color Gold = new Color(1f, 0.85f, 0.2f);
        static Font _font;

        // (이름, 화면 뱃지, 색, 설명) — 색/뱃지는 PromptHUD 노트 표시와 같게 유지
        static readonly (string name, string badge, Color color, string desc)[] Notes =
        {
            ("일반", "", new Color(1f, 0.85f, 0.2f), "링이 화살표에 겹치는 순간 그 방향으로 썰기"),
            ("연타", "연타 ×5", new Color(1f, 0.45f, 0.1f), "큰 재료! 맞춰 친 뒤 아무 방향으로 5번 빠르게 연타"),
            ("슬로우", "SLOW", new Color(0.5f, 0.9f, 1f), "타이밍 맞춰 썰면 슬로우모션 한 컷"),
            ("홀드", "HOLD", new Color(0.7f, 1f, 0.5f), "맞춰 누르고 그대로 유지 — 가운데 링이 겹칠 때 떼기"),
        };

        [MenuItem("BeatSlash/Build Tutorial Prefab")]
        public static void Build()
        {
            _font = AssetDatabase.LoadAssetAtPath<Font>("Assets/Resources/Fonts/UIFont.ttf");
            var buttonSprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");

            // 루트: 전체 화면 어둡게 + 뒤 UI 클릭 차단
            var root = new GameObject(TutorialPanel.PrefabName, typeof(RectTransform));
            Stretch(root.GetComponent<RectTransform>());
            root.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.75f);
            var tut = root.AddComponent<TutorialPanel>();

            var window = MakeImage(root.transform, "Window", Sprite("panel"), new Vector2(1400f, 820f), Vector2.zero);
            if (window.sprite == null) window.color = new Color(0.09f, 0.06f, 0.12f, 0.97f); // 다크 퍼플 폴백
            var w = window.transform;

            MakeText(w, "Title", "노트 가이드", 56, Gold, new Vector2(0f, 330f), new Vector2(800f, 80f));

            // ── 페이지 1: 기본 조작 ──
            var p1 = MakePage(w, "Page_Basics");
            MakeNoteIcon(p1, Gold, new Vector2(-500f, 40f), 190f);
            var basics = MakeText(p1, "Body",
                "방향키 / WASD 또는 마우스 스와이프로 재료를 썰어요\n\n" +
                "링이 조여들다 화살표에 딱 겹치는 순간이 정답 타이밍!\n" +
                "판정은 PERFECT · GOOD · MISS\n\n" +
                "클래식 모드: 화면 곳곳에 화살표가 떠요\n" +
                "이지 모드: 4방향 고정 위치로 날아와 읽기 쉬워요\n\n" +
                "<color=#FFD84D>20콤보</color>를 넘기면 피버 타임 — 점수 배율 UP! (MISS하면 해제)\n\n" +
                "ESC 일시정지 · F1 / F2 판정 타이밍 보정",
                30, Color.white, new Vector2(130f, 20f), new Vector2(1000f, 560f));
            basics.alignment = TextAnchor.MiddleLeft;

            // ── 페이지 2: 노트 종류 ──
            var p2 = MakePage(w, "Page_Notes");
            for (int i = 0; i < Notes.Length; i++)
            {
                var n = Notes[i];
                float y = 200f - i * 135f;
                MakeNoteIcon(p2, n.color, new Vector2(-540f, y), 110f);
                string title = n.badge == "" ? n.name : $"{n.name}   [{n.badge}]";
                var t = MakeText(p2, "Name_" + i, title, 36, n.color, new Vector2(60f, y + 24f), new Vector2(1060f, 50f));
                t.alignment = TextAnchor.MiddleLeft;
                var d = MakeText(p2, "Desc_" + i, n.desc, 28, Color.white, new Vector2(60f, y - 24f), new Vector2(1060f, 44f));
                d.alignment = TextAnchor.MiddleLeft;
            }

            // ── 네비게이션 ──
            tut.pages = new[] { p1.gameObject, p2.gameObject };
            tut.prevButton = MakeButton(w, "Prev", "이전", buttonSprite, new Vector2(-420f, -330f));
            tut.nextButton = MakeButton(w, "Next", "다음", buttonSprite, new Vector2(420f, -330f));
            tut.closeButton = MakeButton(w, "Close", "닫기", buttonSprite, new Vector2(560f, 330f));
            tut.pageLabel = MakeText(w, "PageLabel", "1 / 2", 30, new Color(1f, 1f, 1f, 0.7f),
                new Vector2(0f, -330f), new Vector2(200f, 50f));

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

        static Transform MakePage(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            Stretch(go.GetComponent<RectTransform>());
            return go.transform;
        }

        /// <summary>인게임 노트 모양: 색 링 + 같은 색 화살표.</summary>
        static void MakeNoteIcon(Transform parent, Color color, Vector2 pos, float size)
        {
            var ring = MakeImage(parent, "Ring", Sprite("note_ring"), new Vector2(size, size), pos);
            ring.color = color;
            var arrow = MakeImage(parent, "Arrow", Sprite("arrow_up"), new Vector2(size * 0.62f, size * 0.62f), pos);
            arrow.color = color;
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

        static Button MakeButton(Transform parent, string name, string label, Sprite sprite, Vector2 pos)
        {
            var img = MakeImage(parent, "Btn_" + name, sprite, new Vector2(220f, 70f), pos);
            img.type = Image.Type.Sliced;
            img.color = new Color(0.85f, 0.8f, 1f, 0.85f); // 설정창 버튼 톤
            img.raycastTarget = true;
            var btn = img.gameObject.AddComponent<Button>();
            var t = MakeText(img.transform, "Label", label, 32, new Color(0.12f, 0.08f, 0.2f), Vector2.zero, Vector2.zero);
            Stretch(t.rectTransform);
            return btn;
        }
    }
}
