using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Cipher
{
    /// <summary>Запись в списке найденных улик.</summary>
    public struct CaseEntry
    {
        public string Title, Body;
        public Color Color;
    }

    /// <summary>
    /// Экранный интерфейс игры. Сами объекты (кнопки, карточки, тексты) лежат в сцене
    /// в Canvas «Cipher UI» — их можно двигать, перекрашивать и менять шрифты вручную.
    /// Этот скрипт только показывает/прячет их (с плавной анимацией) и подставляет тексты.
    /// </summary>
    public class CipherUI : MonoBehaviour
    {
        [Header("Прицел")]
        public RectTransform crosshairRoot;
        public Graphic[] crosshairParts;
        public Image progressRing;
        public GameObject hintPill;
        public Text hintText;

        [Header("Верхняя панель")]
        public GameObject hud;
        public Text counterText;
        public Image counterFill;

        [Header("Нижние кнопки")]
        public Button uvButton;
        public Text uvButtonLabel;
        public Image uvButtonBg;
        public Button caseButton;
        public CanvasGroup uvOverlay;

        [Header("Карточка сообщения")]
        public RectTransform messageSheet;
        public Image messageAccent;
        public Text messageEyebrow;
        public Text messageTitle;
        public Text messageBody;
        public GameObject lessonBlock;
        public Image lessonBar;
        public Text lessonText;
        public Button messageButton;
        public Text messageButtonLabel;
        public Image messageButtonBg;

        [Header("Нижняя карточка (сканирование, портал)")]
        public RectTransform bottomSheet;
        public Text bottomTitle;
        public Text bottomText;
        public GameObject statsRow;
        public Text statTables, statWalls, statFloors;
        public Button bottomButton;
        public Text bottomButtonLabel;

        [Header("Список найденных улик")]
        public RectTransform caseSheet;
        public RectTransform caseList;
        public GameObject caseEntryTemplate;
        public Text caseEmpty;
        public Button caseCloseButton;

        [Header("Цвета")]
        public Color accent = new Color(0.31f, 0.93f, 1f);
        public Color warning = new Color(1f, 0.66f, 0.28f);
        public Color uvColor = new Color(0.62f, 0.38f, 1f);
        public Color glass = new Color(0.035f, 0.06f, 0.1f, 0.86f);

        public event Action UVPressed, CasePressed;
        Action onMessage, onBottom;
        readonly Dictionary<RectTransform, Coroutine> anims = new Dictionary<RectTransform, Coroutine>();
        readonly Dictionary<RectTransform, Vector2> basePos = new Dictionary<RectTransform, Vector2>();
        readonly HashSet<RectTransform> visible = new HashSet<RectTransform>();
        float shownProgress;

        public bool BlockingPanelOpen => visible.Contains(messageSheet) || visible.Contains(caseSheet);

        void Awake()
        {
            foreach (var rt in new[] { messageSheet, bottomSheet, caseSheet })
                if (rt) { basePos[rt] = rt.anchoredPosition; rt.gameObject.SetActive(false); }

            if (messageButton) messageButton.onClick.AddListener(() => { var a = onMessage; onMessage = null; Hide(messageSheet); a?.Invoke(); });
            if (bottomButton) bottomButton.onClick.AddListener(() => onBottom?.Invoke());
            if (uvButton) uvButton.onClick.AddListener(() => UVPressed?.Invoke());
            if (caseButton) caseButton.onClick.AddListener(() => CasePressed?.Invoke());
            if (caseCloseButton) caseCloseButton.onClick.AddListener(() => Hide(caseSheet));
            if (caseEntryTemplate) caseEntryTemplate.SetActive(false);

            ShowHud(false);
            SetUV(false);
            SetProgress(-1);
            SetHint("");
            ShowCrosshair(false);
        }

        // ---------- карточка сообщения ----------

        public void ShowMessage(string eyebrow, string title, string body, string lesson, string button, Color color, Action onClick)
        {
            messageEyebrow.gameObject.SetActive(!string.IsNullOrEmpty(eyebrow));
            messageEyebrow.text = (eyebrow ?? "").ToUpperInvariant();
            messageEyebrow.color = color;
            messageTitle.text = title;
            messageBody.text = body;
            messageBody.gameObject.SetActive(!string.IsNullOrEmpty(body));
            bool hasLesson = !string.IsNullOrEmpty(lesson);
            lessonBlock.SetActive(hasLesson);
            if (hasLesson)
            {
                lessonText.text = "<b>Как защититься</b>\n" + lesson;
                lessonBar.color = color;
                lessonBlock.GetComponent<Image>().color = new Color(color.r, color.g, color.b, 0.1f);
            }
            if (messageAccent) messageAccent.color = color;
            if (messageButtonBg) messageButtonBg.color = color;
            messageButtonLabel.text = button;
            onMessage = onClick;
            Show(messageSheet);
        }

        public void HideMessage() => Hide(messageSheet);

        // ---------- нижняя карточка ----------

        public void ShowScan(int tables, int walls, int floors, bool ready, Action onPlace)
        {
            bottomTitle.text = "Сканирование комнаты";
            bottomText.text = ready
                ? "Поверхности найдены. Можно размещать улики — или продолжить сканировать для точности."
                : "Медленно поводите телефоном по комнате: по столам, стенам и полу.";
            statsRow.SetActive(true);
            statTables.text = tables.ToString();
            statWalls.text = walls.ToString();
            statFloors.text = floors.ToString();
            bottomButton.gameObject.SetActive(true);
            bottomButtonLabel.text = "Разместить улики";
            bottomButton.interactable = ready;
            onBottom = onPlace;
            if (!visible.Contains(bottomSheet)) Show(bottomSheet);
        }

        public void ShowBottom(string title, string text, string button, Action onClick)
        {
            bottomTitle.text = title;
            bottomText.text = text;
            statsRow.SetActive(false);
            bottomButton.gameObject.SetActive(button != null);
            if (button != null) bottomButtonLabel.text = button;
            bottomButton.interactable = true;
            onBottom = onClick;
            if (!visible.Contains(bottomSheet)) Show(bottomSheet);
        }

        public void HideBottom() => Hide(bottomSheet);

        // ---------- HUD ----------

        public void ShowHud(bool on) { if (hud) hud.SetActive(on); }

        public void SetCounter(int found, int total)
        {
            if (counterText) counterText.text = $"{found} / {total}";
            if (counterFill) StartCoroutine(FillTo(total > 0 ? found / (float)total : 0f));
        }

        IEnumerator FillTo(float target)
        {
            float start = counterFill.fillAmount;
            for (float t = 0; t < 1f; t += Time.unscaledDeltaTime / 0.4f)
            {
                counterFill.fillAmount = Mathf.Lerp(start, target, EaseOut(t));
                yield return null;
            }
            counterFill.fillAmount = target;
        }

        public void SetHint(string text)
        {
            if (!hintPill) return;
            bool on = !string.IsNullOrEmpty(text);
            if (on) hintText.text = text;
            hintPill.SetActive(on);
        }

        /// <summary>Кольцо сканирования: 0..1, или -1 — спрятать.</summary>
        public void SetProgress(float t)
        {
            if (!progressRing) return;
            progressRing.gameObject.SetActive(t >= 0);
            shownProgress = Mathf.Clamp01(t);
            progressRing.fillAmount = shownProgress;
        }

        /// <summary>0 — ничего, 1 — улика (можно изучать), 2 — улика далеко.</summary>
        public void SetCrosshair(int state)
        {
            if (!crosshairRoot) return;
            Color c = state == 1 ? accent : state == 2 ? warning : new Color(1, 1, 1, 0.75f);
            foreach (var g in crosshairParts) if (g) g.color = c;
            float target = state == 0 ? 1f : 1.2f;
            crosshairRoot.localScale = Vector3.Lerp(crosshairRoot.localScale, Vector3.one * target, 0.35f);
        }

        public void ShowCrosshair(bool on)
        {
            if (crosshairRoot) crosshairRoot.gameObject.SetActive(on);
            if (!on) { SetProgress(-1); SetHint(""); }
        }

        public void SetUV(bool on)
        {
            if (uvOverlay)
            {
                uvOverlay.gameObject.SetActive(true);
                StartCoroutine(FadeGroup(uvOverlay, on ? 1f : 0f));
            }
            if (uvButtonLabel) uvButtonLabel.text = on ? "УФ ВКЛ" : "УФ-ФИЛЬТР";
            if (uvButtonBg) uvButtonBg.color = on ? new Color(uvColor.r, uvColor.g, uvColor.b, 0.92f) : glass;
        }

        IEnumerator FadeGroup(CanvasGroup g, float target)
        {
            float start = g.alpha;
            for (float t = 0; t < 1f; t += Time.unscaledDeltaTime / 0.3f)
            {
                g.alpha = Mathf.Lerp(start, target, EaseOut(t));
                yield return null;
            }
            g.alpha = target;
            if (target <= 0f) g.gameObject.SetActive(false);
        }

        // ---------- список улик ----------

        public void ShowCase(IList<CaseEntry> entries)
        {
            for (int i = caseList.childCount - 1; i >= 0; i--)
            {
                var child = caseList.GetChild(i).gameObject;
                if (child != caseEntryTemplate && child != caseEmpty.gameObject) Destroy(child);
            }
            caseEmpty.gameObject.SetActive(entries.Count == 0);
            for (int i = 0; i < entries.Count; i++)
            {
                var e = entries[i];
                var go = Instantiate(caseEntryTemplate, caseList);
                go.SetActive(true);
                foreach (var t in go.GetComponentsInChildren<Text>(true))
                {
                    if (t.name == "Title") t.text = (i + 1) + ". " + e.Title;
                    else if (t.name == "Body") t.text = e.Body;
                }
                foreach (var img in go.GetComponentsInChildren<Image>(true))
                    if (img.name == "Bar") img.color = e.Color;
            }
            Show(caseSheet);
        }

        // ---------- анимация карточек ----------

        void Show(RectTransform rt)
        {
            if (!rt) return;
            visible.Add(rt);
            Animate(rt, true);
        }

        void Hide(RectTransform rt)
        {
            if (!rt || !visible.Contains(rt)) { if (rt) rt.gameObject.SetActive(false); return; }
            visible.Remove(rt);
            Animate(rt, false);
        }

        void Animate(RectTransform rt, bool show)
        {
            if (anims.TryGetValue(rt, out var c) && c != null) StopCoroutine(c);
            anims[rt] = StartCoroutine(AnimateRoutine(rt, show));
        }

        IEnumerator AnimateRoutine(RectTransform rt, bool show)
        {
            if (!rt.TryGetComponent(out CanvasGroup g)) g = rt.gameObject.AddComponent<CanvasGroup>();
            if (!basePos.TryGetValue(rt, out var target)) target = basePos[rt] = rt.anchoredPosition;
            rt.gameObject.SetActive(true);
            g.interactable = show;
            g.blocksRaycasts = show;
            float startA = show ? 0f : g.alpha, endA = show ? 1f : 0f;
            Vector2 from = show ? target + new Vector2(0, -70) : rt.anchoredPosition;
            Vector2 to = show ? target : target + new Vector2(0, -50);
            float dur = show ? 0.32f : 0.2f;
            for (float t = 0; t < 1f; t += Time.unscaledDeltaTime / dur)
            {
                float e = EaseOut(t);
                g.alpha = Mathf.Lerp(startA, endA, e);
                rt.anchoredPosition = Vector2.LerpUnclamped(from, to, e);
                yield return null;
            }
            g.alpha = endA;
            rt.anchoredPosition = to;
            if (!show) { rt.gameObject.SetActive(false); rt.anchoredPosition = target; }
        }

        static float EaseOut(float t) { t = Mathf.Clamp01(t); return 1f - Mathf.Pow(1f - t, 3f); }

        void Update()
        {
            // лёгкое «дыхание» прицела
            if (crosshairRoot && crosshairRoot.gameObject.activeSelf && crosshairRoot.childCount > 0)
                crosshairRoot.GetChild(0).localRotation = Quaternion.Euler(0, 0, Time.unscaledTime * 20f);
        }
    }
}
