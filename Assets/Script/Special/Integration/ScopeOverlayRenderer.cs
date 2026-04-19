using System.Collections.Generic;
using UnityEngine;

namespace Special.Integration
{
    /// <summary>
    /// 특수 블럭 효과의 영향 범위(scope)를 보드 위에 옅게 깔아 보여주는 헬퍼.
    /// SpriteRenderer 기반 1셀 짜리 quad 를 풀링해서 사용 — GC 와 매 프레임 인스턴스화를 피한다.
    /// SpecialPlacementSequencer 가 소유하고, Show/Hide 만 호출한다.
    /// </summary>
    public class ScopeOverlayRenderer
    {
        private readonly Transform parent;
        private readonly Sprite sprite;
        private readonly int sortingOrder;
        private readonly Stack<GameObject> pool = new Stack<GameObject>();
        private readonly List<GameObject> active = new List<GameObject>();

        private static Sprite sharedFallbackSprite;

        public ScopeOverlayRenderer(Transform parent, Sprite sprite, int sortingOrder)
        {
            this.parent = parent;
            this.sprite = sprite != null ? sprite : GetFallbackSprite();
            this.sortingOrder = sortingOrder;
        }

        public void Show(IReadOnlyList<Vector2Int> arrayCells, Color color, GridManager grid)
        {
            Hide();
            if (arrayCells == null || arrayCells.Count == 0 || grid == null || grid.groundTilemap == null) return;

            float cellSize = grid.groundTilemap.cellSize.x;
            // 살짝 안쪽으로 줄여 그리드 라인이 비치도록 한다.
            float scale = cellSize * 0.94f;

            for (int i = 0; i < arrayCells.Count; i++)
            {
                Vector3 wp = grid.ArrayIndexToWorldCenter(arrayCells[i]);
                GameObject q = Spawn();
                q.transform.position = new Vector3(wp.x, wp.y, wp.z + 0.01f);
                q.transform.localScale = new Vector3(scale, scale, 1f);
                SpriteRenderer sr = q.GetComponent<SpriteRenderer>();
                if (sr != null) sr.color = color;
                q.SetActive(true);
                active.Add(q);
            }
        }

        public void Hide()
        {
            for (int i = 0; i < active.Count; i++)
            {
                GameObject g = active[i];
                if (g == null) continue;
                g.SetActive(false);
                pool.Push(g);
            }
            active.Clear();
        }

        private GameObject Spawn()
        {
            while (pool.Count > 0)
            {
                GameObject g = pool.Pop();
                if (g != null) return g;
            }
            GameObject go = new GameObject("ScopeOverlayCell");
            if (parent != null) go.transform.SetParent(parent, false);
            SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sortingOrder = sortingOrder;
            return go;
        }

        private static Sprite GetFallbackSprite()
        {
            if (sharedFallbackSprite != null) return sharedFallbackSprite;
            Texture2D tex = new Texture2D(1, 1);
            tex.SetPixel(0, 0, Color.white);
            tex.Apply();
            sharedFallbackSprite = Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
            sharedFallbackSprite.name = "ScopeOverlay_Fallback";
            return sharedFallbackSprite;
        }
    }
}
