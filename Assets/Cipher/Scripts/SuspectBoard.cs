using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Cipher
{
    /// <summary>
    /// Доска «Кто это сделал?»: три карточки подозреваемых в AR.
    /// Игрок наводит прицел и касается экрана. Невиновный → игра показывает его алиби.
    /// Виновный → разгадка и событие OnSolved.
    /// </summary>
    public class SuspectBoard : MonoBehaviour
    {
        public event Action OnSolved;
        public int Mistakes { get; private set; }

        class Item
        {
            public Suspect Data;
            public Transform Root, Avatar;
            public TextMesh Stamp;
            public bool Cleared;
            public float Hover;
        }

        readonly List<Item> items = new List<Item>();
        TextMesh status;
        Item hovered;
        bool locked;
        const float W = 1.2f, Header = 0.3f, CardH = 0.58f, Footer = 0.4f;
        float H;

        public static SuspectBoard Create(Transform parent, Vector3 pos, Quaternion rot, List<Suspect> suspects, string task)
        {
            var go = new GameObject("SuspectBoard");
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(pos, rot);
            var b = go.AddComponent<SuspectBoard>();
            b.Build(suspects, task);
            return b;
        }

        void Build(List<Suspect> suspects, string task)
        {
            var t = transform;
            H = Header + CardH + Footer;
            var accent = new Color(1f, 0.78f, 0.3f);
            CipherVisuals.Prim(PrimitiveType.Quad, t, Vector3.zero, new Vector3(W, H, 1), CipherVisuals.PanelBackground(new Color(0.03f, 0.04f, 0.08f, 0.97f)));
            CipherVisuals.Prim(PrimitiveType.Quad, t, new Vector3(0, H / 2 - 0.004f, -0.002f), new Vector3(W, 0.008f, 1), CipherVisuals.PanelBackground(accent));
            CipherVisuals.Text(t, "КТО ЭТО СДЕЛАЛ?", new Vector3(-W / 2 + 0.05f, H / 2 - 0.05f, -0.004f), 0.04f, accent, TextAnchor.UpperLeft, false, FontStyle.Bold);
            CipherVisuals.Text(t, CipherVisuals.Wrap(task, CipherVisuals.CharsFor(W - 0.1f, 0.024f)),
                new Vector3(-W / 2 + 0.05f, H / 2 - 0.12f, -0.004f), 0.024f, new Color(0.85f, 0.92f, 1f), TextAnchor.UpperLeft);
            status = CipherVisuals.Text(t, "", new Vector3(-W / 2 + 0.05f, -H / 2 + Footer - 0.03f, -0.004f), 0.024f, new Color(0.85f, 0.92f, 1f), TextAnchor.UpperLeft);

            int n = suspects.Count;
            float step = (W - 0.1f) / Mathf.Max(1, n);
            int chars = CipherVisuals.CharsFor(step - 0.04f, 0.02f);
            for (int i = 0; i < n; i++)
            {
                var s = suspects[i];
                var root = new GameObject("Suspect_" + i).transform;
                root.SetParent(t, false);
                float cy = H / 2 - Header - CardH / 2;
                root.localPosition = new Vector3(-W / 2 + 0.05f + step * (i + 0.5f), cy, -0.03f);

                // подложка карточки
                CipherVisuals.Prim(PrimitiveType.Quad, root, new Vector3(0, 0, 0.02f), new Vector3(step - 0.02f, CardH - 0.02f, 1),
                    CipherVisuals.PanelBackground(new Color(s.color.r * 0.12f, s.color.g * 0.12f, s.color.b * 0.12f + 0.04f, 1f)));
                var box = root.gameObject.AddComponent<BoxCollider>();
                box.size = new Vector3(step - 0.02f, CardH - 0.02f, 0.1f);

                // голографический силуэт
                var avatar = new GameObject("Avatar").transform;
                avatar.SetParent(root, false);
                avatar.localPosition = new Vector3(0, 0.15f, -0.04f);
                var holo = CipherVisuals.Hologram(s.color, 0.5f, 2f, 1f, 30f);
                CipherVisuals.Prim(PrimitiveType.Sphere, avatar, new Vector3(0, 0.045f, 0), Vector3.one * 0.06f, holo);
                CipherVisuals.Prim(PrimitiveType.Capsule, avatar, new Vector3(0, -0.035f, 0), new Vector3(0.09f, 0.045f, 0.05f), holo);

                CipherVisuals.Text(root, CipherVisuals.Wrap(s.name, chars), new Vector3(0, 0.03f, -0.01f), 0.028f, s.color, TextAnchor.UpperCenter, false, FontStyle.Bold);
                CipherVisuals.Text(root, CipherVisuals.Wrap(s.role, chars), new Vector3(0, -0.035f, -0.01f), 0.02f, new Color(0.7f, 0.78f, 0.9f), TextAnchor.UpperCenter);
                CipherVisuals.Text(root, CipherVisuals.Wrap(s.suspicion, chars), new Vector3(0, -0.09f, -0.01f), 0.02f, new Color(0.85f, 0.92f, 1f), TextAnchor.UpperCenter);
                var stamp = CipherVisuals.Text(root, "", new Vector3(0, 0.15f, -0.08f), 0.05f, new Color(0.4f, 1f, 0.55f), TextAnchor.MiddleCenter, false, FontStyle.Bold);
                stamp.transform.localRotation = Quaternion.Euler(0, 0, 12f);

                items.Add(new Item { Data = s, Root = root, Avatar = avatar, Stamp = stamp });
            }
        }

        /// <summary>Вызывается игрой каждый кадр, пока доска активна.</summary>
        public void HandlePointer(ARInput pointer)
        {
            Item h = null;
            if (!locked && Physics.Raycast(pointer.Ray, out var hit, 10f))
                foreach (var it in items) if (hit.collider.transform == it.Root && !it.Cleared) h = it;

            if (h != hovered && h != null) CipherAudio.Hover();
            hovered = h;

            foreach (var it in items)
            {
                it.Hover = Mathf.MoveTowards(it.Hover, it == hovered ? 1 : 0, Time.deltaTime * 6f);
                it.Root.localScale = Vector3.one * (1f + it.Hover * 0.08f);
                it.Avatar.localRotation = Quaternion.Euler(0f, Mathf.Sin(Time.time * 0.9f + it.Root.GetSiblingIndex()) * 30f, 0f);
            }

            if (locked || !pointer.TapDown || hovered == null) return;
            StartCoroutine(Accuse(hovered));
        }

        /// <summary>Только для проверки в редакторе: выбрать виновного.</summary>
        public void DebugChooseCulprit()
        {
            if (locked) return;
            foreach (var it in items) if (it.Data.isCulprit) { StartCoroutine(Accuse(it)); return; }
        }

        /// <summary>Только для проверки в редакторе: выбрать первого невиновного (покажет алиби).</summary>
        public void DebugChooseInnocent()
        {
            if (locked) return;
            foreach (var it in items) if (!it.Data.isCulprit && !it.Cleared) { StartCoroutine(Accuse(it)); return; }
        }

        IEnumerator Accuse(Item item)
        {
            locked = true;
            int chars = CipherVisuals.CharsFor(W - 0.1f, 0.024f);
            if (!item.Data.isCulprit)
            {
                Mistakes++;
                CipherAudio.Error();
                item.Cleared = true;
                item.Stamp.text = "АЛИБИ";
                CipherVisuals.SetTextColor(item.Stamp, new Color(0.4f, 1f, 0.55f));
                item.Root.localScale = Vector3.one;
                CipherVisuals.SetTextColor(status, new Color(1f, 0.75f, 0.45f));
                status.text = CipherVisuals.Wrap("Не он. " + item.Data.explanation, chars);
                yield return new WaitForSeconds(1.2f);
                locked = false;
                yield break;
            }

            CipherAudio.Success();
            item.Stamp.text = "ВИНОВЕН";
            CipherVisuals.SetTextColor(item.Stamp, new Color(1f, 0.3f, 0.35f));
            foreach (var it in items) if (it != item) { it.Cleared = true; it.Root.localScale = Vector3.one * 0.92f; }
            CipherVisuals.SetTextColor(status, new Color(0.5f, 1f, 0.6f));
            status.text = CipherVisuals.Wrap(item.Data.explanation, chars);
            yield return new WaitForSeconds(2.5f);
            OnSolved?.Invoke();
        }
    }
}
