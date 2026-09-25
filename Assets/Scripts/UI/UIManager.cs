using System.Collections.Generic;
using UnityEngine;

namespace BeatSlash.UI
{
    /// <summary>
    /// 프리팹 UI 패널 관리자. 패널 프리팹은 Assets/Resources/UIPrefabs/에 두고 이름으로 띄운다.
    /// 한 번 만든 패널은 숨겼다가 재사용.
    /// </summary>
    public class UIManager : MonoBehaviour
    {
        [Tooltip("패널을 붙일 캔버스")]
        public Transform root;

        readonly Dictionary<string, GameObject> _panels = new Dictionary<string, GameObject>();

        public GameObject Show(string prefabName)
        {
            if (!_panels.TryGetValue(prefabName, out var panel) || panel == null)
            {
                var prefab = Resources.Load<GameObject>("UIPrefabs/" + prefabName);
                if (prefab == null)
                {
                    Debug.LogWarning($"[UIManager] UIPrefabs/{prefabName} 프리팹이 없어요");
                    return null;
                }
                panel = Instantiate(prefab, root, false);
                panel.name = prefabName;
                _panels[prefabName] = panel;
            }
            panel.transform.SetAsLastSibling(); // 항상 맨 위
            panel.SetActive(true);
            return panel;
        }

        public void Hide(string prefabName)
        {
            if (_panels.TryGetValue(prefabName, out var panel) && panel != null)
                panel.SetActive(false);
        }

        public bool IsOpen(string prefabName) =>
            _panels.TryGetValue(prefabName, out var panel) && panel != null && panel.activeSelf;
    }
}
