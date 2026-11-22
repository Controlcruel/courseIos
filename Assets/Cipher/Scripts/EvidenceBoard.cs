using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Cipher
{
    /// <summary>
    /// Доска расследования: игрок выбирает улики по порядку атаки.
    /// Правильный порядок — по полю «Номер шага» у улик. Верно → красные нити и событие OnSolved.
    /// </summary>
    public class EvidenceBoard : MonoBehaviour
    {
        public event Action OnSolved;
        public int Mistakes { get; private set; }

        class Item
        {
            public int ClueIndex;
            public Transform Root, Model;
            public TextMesh Number;
            public bool Chosen;
            public float Hover;
        }

        readonly List<Item> items = new List<Item>();
        readonly List<Item> chosen = new List<Item>();
        TextMesh status;
        Item hovered;
        bool locked;
        const float W = 0.95f;
        float H;
        int cols, rows;
        const float CellH = 0.36f, Header = 0.36f, Footer = 0.34f;

        string solvedText;
        List<CipherClue> clues;

        /// <param name="steps">Улики-шаги атаки, уже отсортированные по правильному порядку.</param>
        public static EvidenceBoard Create(Transform parent, Vector3 pos, Quaternion rot, List<CipherClue> steps, string task, string solved)
        {
            var go = new GameObject("EvidenceBoard");
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(pos, rot);
            var b = go.AddComponent<EvidenceBoard>();
            b.clues = steps;
            b.solvedText = solved;
            b.Build(task);
            return b;
        }

        void Build(string task)
        {
            var t = transform;
            cols = Mathf.Min(3, Mathf.Max(1, clues.Count));
            rows = Mathf.CeilToInt(clues.Count / (float)cols);
            H = Header + rows * CellH + Footer;
            CipherVisuals.Prim(PrimitiveType.Quad, t, Vector3.zero, new Vector3(W, H, 1), CipherVisuals.PanelBackground(new Color(0.03f, 0.04f, 0.08f, 0.97f)));
            CipherVisuals.Prim(PrimitiveType.Quad, t, new Vector3(0, H / 2 - 0.004f, -0.002f), new Vector3(W, 0.008f, 1), CipherVisuals.PanelBackground(new Color(1f, 0.25f, 0.3f)));
            CipherVisuals.Text(t, "ДОСКА РАССЛЕДОВАНИЯ", new Vector3(-W / 2 + 0.05f, H / 2 - 0.05f, -0.004f), 0.04f, new Color(1f, 0.6f, 0.6f), TextAnchor.UpperLeft, false, FontStyle.Bold);
            CipherVisuals.Text(t, CipherVisuals.Wrap(task, CipherVisuals.CharsFor(W - 0.1f, 0.024f)),
                new Vector3(-W / 2 + 0.05f, H / 2 - 0.12f, -0.004f), 0.024f, new Color(0.85f, 0.92f, 1f), TextAnchor.UpperLeft);
            status = CipherVisuals.Text(t, "", new Vector3(-W / 2 + 0.05f, -H / 2 + Footer - 0.03f, -0.004f), 0.024f, new Color(0.4f, 1f, 1f), TextAnchor.UpperLeft);

            // перемешиваем порядок карточек
            var order = new List<int>();
            for (int i = 0; i < clues.Count; i++) order.Add(i);
            for (int i = order.Count - 1; i > 0; i--) { int j = UnityEngine.Random.Range(0, i + 1); (order[i], order[j]) = (order[j], order[i]); }
            if (order.Count > 1 && IsSorted(order)) (order[0], order[1]) = (order[1], order[0]);

            float step = (W - 0.1f) / cols;
            for (int k = 0; k < order.Count; k++)
            {
                int idx = order[k];
                var info = clues[idx];
                var root = new GameObject("Card_" + idx).transform;
                root.SetParent(t, false);
                int row = k / cols, col = k % cols;
                int inRow = Mathf.Min(cols, clues.Count - row * cols);
                float rowOffset = (cols - inRow) * step * 0.5f; // неполный ряд — по центру
                root.localPosition = new Vector3(-W / 2 + 0.05f + rowOffset + step * (col + 0.5f), H / 2 - Header - CellH * (row + 0.5f) + 0.04f, -0.06f);
                var box = root.gameObject.AddComponent<BoxCollider>();
                box.size = new Vector3(step * 0.9f, 0.34f, 0.12f);
                box.center = new Vector3(0, -0.02f, 0);

                var model = CopyModel(info, root);
                CipherVisuals.Text(root, CipherVisuals.Wrap(info.title, 12), new Vector3(0, -0.12f, 0.05f), 0.02f, info.color, TextAnchor.UpperCenter, false, FontStyle.Bold);
                var num = CipherVisuals.Text(root, "", new Vector3(0, 0.13f, 0.02f), 0.06f, new Color(1f, 0.3f, 0.35f), TextAnchor.MiddleCenter, false, FontStyle.Bold);

                items.Add(new Item { ClueIndex = idx, Root = root, Model = model, Number = num });
            }
        }

        /// <summary>Копия модели улики для карточки: повёрнута «лицом» к игроку и уменьшена до 16 см.</summary>
        static Transform CopyModel(CipherClue clue, Transform card)
        {
            var wobble = new GameObject("Wobble").transform;
            wobble.SetParent(card, false);
            var tilt = new GameObject("Tilt").transform;
            tilt.SetParent(wobble, false);
            tilt.localRotation = Quaternion.Euler(-70f, 0f, 0f);
            if (!clue.model) return wobble;

            var copy = Instantiate(clue.model.gameObject, tilt);
            copy.SetActive(true);
            foreach (Transform child in copy.GetComponentsInChildren<Transform>(true)) child.gameObject.SetActive(true);
            foreach (var c in copy.GetComponentsInChildren<Collider>(true)) Destroy(c);
            copy.transform.localPosition = Vector3.zero;
            copy.transform.localRotation = Quaternion.identity;
            copy.transform.localScale = clue.ModelBaseScale;

            var rends = copy.GetComponentsInChildren<Renderer>();
            if (rends.Length > 0)
            {
                var b = rends[0].bounds;
                foreach (var r in rends) b.Encapsulate(r.bounds);
                float size = Mathf.Max(b.size.x, b.size.y, b.size.z);
                if (size > 0.0001f) copy.transform.localScale *= 0.16f / (size / card.lossyScale.x);
                // центрируем
                var b2 = rends[0].bounds;
                foreach (var r in rends) b2.Encapsulate(r.bounds);
                copy.transform.position += tilt.position - b2.center;
            }
            return wobble;
        }

        static bool IsSorted(List<int> l) { for (int i = 1; i < l.Count; i++) if (l[i] < l[i - 1]) return false; return true; }

        /// <summary>Вызывается игрой каждый кадр, пока доска активна.</summary>
        public void HandlePointer(ARInput pointer)
        {
            Item h = null;
            if (!locked && Physics.Raycast(pointer.Ray, out var hit, 10f))
                foreach (var it in items) if (hit.collider.transform == it.Root) h = it;

            if (h != hovered && h != null) CipherAudio.Hover();
            hovered = h;

            foreach (var it in items)
            {
                it.Hover = Mathf.MoveTowards(it.Hover, it == hovered ? 1 : 0, Time.deltaTime * 6f);
                it.Root.localScale = Vector3.one * (1f + it.Hover * 0.12f);
                it.Model.localRotation = Quaternion.Euler(0f, Mathf.Sin(Time.time * (it.Chosen ? 0.6f : 1.2f) + it.ClueIndex) * 25f, 0f);
            }

            if (locked || !pointer.TapDown || hovered == null) return;
            Choose(hovered);
        }

        /// <summary>Только для проверки в редакторе: выбрать следующую карточку в правильном порядке.</summary>
        public void DebugChooseNext()
        {
            if (locked) return;
            foreach (var it in items) if (it.ClueIndex == chosen.Count && !it.Chosen) { Choose(it); return; }
        }

        void Choose(Item item)
        {
            if (item.Chosen)
            {
                // отмена последнего выбора
                if (chosen.Count > 0 && chosen[chosen.Count - 1] == item)
                {
                    item.Chosen = false; item.Number.text = "";
                    chosen.RemoveAt(chosen.Count - 1);
                    CipherAudio.Select();
                }
                return;
            }

            item.Chosen = true;
            chosen.Add(item);
            item.Number.text = chosen.Count.ToString();
            CipherAudio.Select();
            status.text = "";

            if (chosen.Count == items.Count) StartCoroutine(Check());
        }

        IEnumerator Check()
        {
            locked = true;
            bool ok = true;
            for (int i = 0; i < chosen.Count; i++) if (chosen[i].ClueIndex != i) ok = false;

            if (!ok)
            {
                Mistakes++;
                CipherAudio.Error();
                status.text = CipherVisuals.Wrap(Mistakes == 1
                    ? "Не сходится. Подумай, с чего злоумышленник мог начать и чем всё закончилось."
                    : $"Подсказка: первый шаг — «{clues[0].title}», последний — «{clues[clues.Count - 1].title}».", CipherVisuals.CharsFor(W - 0.1f, 0.024f));
                CipherVisuals.SetTextColor(status, new Color(1f, 0.45f, 0.45f));
                yield return new WaitForSeconds(1.5f);
                foreach (var it in chosen) { it.Chosen = false; it.Number.text = ""; }
                chosen.Clear();
                locked = false;
                yield break;
            }

            CipherAudio.Success();
            CipherVisuals.SetTextColor(status, new Color(0.5f, 1f, 0.6f));
            status.text = CipherVisuals.Wrap(solvedText, CipherVisuals.CharsFor(W - 0.1f, 0.024f));

            // красные «нити», как на доске детектива
            for (int i = 0; i < chosen.Count - 1; i++)
            {
                var lr = CipherVisuals.Line(transform, new Color(1f, 0.2f, 0.25f, 0.95f), 0.006f);
                Vector3 a = chosen[i].Root.position, b = chosen[i + 1].Root.position;
                const int seg = 16;
                lr.positionCount = 0;
                for (int s = 0; s <= seg; s++)
                {
                    lr.positionCount = s + 1;
                    float u = s / (float)seg;
                    lr.SetPosition(s, Vector3.Lerp(a, b, u) - transform.up * Mathf.Sin(u * Mathf.PI) * 0.06f - transform.forward * 0.02f);
                    yield return null;
                }
            }

            yield return new WaitForSeconds(3.5f);
            OnSolved?.Invoke();
        }
    }
}
