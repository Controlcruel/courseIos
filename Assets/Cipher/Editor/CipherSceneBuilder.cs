using System.IO;
using System.Linq;
using Unity.XR.CoreUtils;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.InputSystem.XR;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

namespace Cipher.EditorTools
{
    /// <summary>Собирает сцену игры для iPhone: AR-камера, поиск плоскостей, лидар, игра и экранный интерфейс.</summary>
    public static class CipherSceneBuilder
    {
        const string ScenePath = "Assets/Scenes/Cipher.unity";
        static Font regular, bold, mono;
        static Sprite card, outline, pill, ringThin, ringThick, dot, vignette, iconUV, iconCase, iconClose;

        [MenuItem("Cipher/Собрать всё (пункты 1–3)", priority = 0)]
        public static void BuildEverything()
        {
            CipherSetup.ConfigureIOS();
            CipherAssetBuilder.BuildMissing();
            BuildScene();
        }

        [MenuItem("Cipher/3. Создать сцену игры", priority = 3)]
        public static void BuildScene()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var caseFile = AssetDatabase.LoadAssetAtPath<CaseFile>(CipherAssetBuilder.CasePath);
            if (!caseFile) { CipherAssetBuilder.BuildMissing(); caseFile = AssetDatabase.LoadAssetAtPath<CaseFile>(CipherAssetBuilder.CasePath); }
            LoadUiAssets();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // ---- свет (для моделей с обычными материалами) ----
            var light = new GameObject("Directional Light").AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1f;
            light.shadows = LightShadows.None;
            light.transform.rotation = Quaternion.Euler(50, -30, 0);

            // ---- AR Session ----
            var session = new GameObject("AR Session");
            session.AddComponent<ARSession>();
            session.AddComponent<ARInputManager>();

            // ---- XR Origin + камера телефона ----
            var originGO = new GameObject("XR Origin (Mobile AR)");
            var origin = originGO.AddComponent<XROrigin>();
            var offset = new GameObject("Camera Offset").transform;
            offset.SetParent(originGO.transform, false);

            var camGO = new GameObject("Main Camera") { tag = "MainCamera" };
            camGO.transform.SetParent(offset, false);
            var cam = camGO.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Color.black;
            cam.nearClipPlane = 0.05f;
            cam.farClipPlane = 40f;
            camGO.AddComponent<AudioListener>();
            if (!camGO.TryGetComponent(out UniversalAdditionalCameraData urp)) urp = camGO.AddComponent<UniversalAdditionalCameraData>();
            urp.renderPostProcessing = false;
            camGO.AddComponent<ARCameraManager>();
            camGO.AddComponent<ARCameraBackground>();
            var occlusion = camGO.AddComponent<AROcclusionManager>();
            occlusion.requestedEnvironmentDepthMode = EnvironmentDepthMode.Best;
            occlusion.requestedOcclusionPreferenceMode = OcclusionPreferenceMode.PreferEnvironmentOcclusion;
            occlusion.requestedHumanDepthMode = HumanSegmentationDepthMode.Disabled;
            occlusion.requestedHumanStencilMode = HumanSegmentationStencilMode.Disabled;

            var tpd = camGO.AddComponent<TrackedPoseDriver>();
            tpd.positionInput = Action("Device Position", "Vector3", "<XRHMD>/centerEyePosition", "<HandheldARInputDevice>/devicePosition");
            tpd.rotationInput = Action("Device Rotation", "Quaternion", "<XRHMD>/centerEyeRotation", "<HandheldARInputDevice>/deviceRotation");

            origin.Camera = cam;
            origin.CameraFloorOffsetObject = offset.gameObject;
            origin.RequestedTrackingOriginMode = XROrigin.TrackingOriginMode.Device;
            origin.CameraYOffset = 0f;

            var planes = originGO.AddComponent<ARPlaneManager>();
            planes.planePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(CipherAssetBuilder.PlanePrefabPath);
            planes.requestedDetectionMode = PlaneDetectionMode.Horizontal | PlaneDetectionMode.Vertical;
            originGO.AddComponent<ARRaycastManager>();

            // ---- интерфейс ----
            var ui = BuildUI();

            // ---- игра ----
            var gameGO = new GameObject("Cipher Game");
            gameGO.AddComponent<Cipher.ARInput>();
            var finder = gameGO.AddComponent<Cipher.SurfaceFinder>();
            finder.planeManager = planes;
            var game = gameGO.AddComponent<Cipher.CipherGame>();
            game.caseFile = caseFile;
            game.ui = ui;
            game.surfaces = finder;
            game.occlusion = occlusion;

            Directory.CreateDirectory("Assets/Scenes");
            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            Selection.activeGameObject = gameGO;
            Debug.Log("[Cipher] Сцена для iPhone создана: " + ScenePath + ". Нажмите Play — в редакторе работает XR Simulation (ПКМ + WASD — ходить, ЛКМ — касание).");
        }

        static InputActionProperty Action(string name, string type, params string[] bindings)
        {
            var a = new InputAction(name, InputActionType.Value, expectedControlType: type);
            foreach (var b in bindings) a.AddBinding(b);
            return new InputActionProperty(a);
        }

        // ================= UI =================

        static readonly Color Accent = new Color(0.31f, 0.93f, 1f);
        static readonly Color Glass = new Color(0.035f, 0.06f, 0.1f, 0.86f);
        static readonly Color GlassStrong = new Color(0.03f, 0.05f, 0.085f, 0.975f);
        static readonly Color TextMain = new Color(0.94f, 0.97f, 1f);
        static readonly Color TextSoft = new Color(0.66f, 0.74f, 0.84f);
        static readonly Color Ink = new Color(0.02f, 0.06f, 0.09f);
        static readonly Color UV = new Color(0.62f, 0.38f, 1f);

        static void LoadUiAssets()
        {
            regular = AssetDatabase.LoadAssetAtPath<Font>(CipherAssetBuilder.FontRegular);
            bold = AssetDatabase.LoadAssetAtPath<Font>(CipherAssetBuilder.FontBold);
            mono = AssetDatabase.LoadAssetAtPath<Font>(CipherAssetBuilder.FontMono);
            Sprite Sp(string n) => AssetDatabase.LoadAssetAtPath<Sprite>(CipherAssetBuilder.TexPath(n));
            card = Sp("Card"); outline = Sp("CardOutline"); pill = Sp("Pill");
            ringThin = Sp("RingThin"); ringThick = Sp("RingThick"); dot = Sp("Dot"); vignette = Sp("Vignette");
            iconUV = Sp("IconUV"); iconCase = Sp("IconCase"); iconClose = Sp("IconClose");
        }

        static Cipher.CipherUI BuildUI()
        {
            var es = new GameObject("EventSystem");
            es.AddComponent<EventSystem>();
            es.AddComponent<InputSystemUIInputModule>();

            var canvasGO = new GameObject("Cipher UI");
            var canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGO.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1170, 2532);
            scaler.matchWidthOrHeight = 0.5f;
            canvasGO.AddComponent<GraphicRaycaster>();
            var ui = canvasGO.AddComponent<Cipher.CipherUI>();

            // ---- УФ-режим: фиолетовая виньетка по краям ----
            var uv = Rect("UV Overlay", canvasGO.transform);
            Stretch(uv);
            ui.uvOverlay = uv.gameObject.AddComponent<CanvasGroup>();
            ui.uvOverlay.alpha = 0; ui.uvOverlay.blocksRaycasts = false; ui.uvOverlay.interactable = false;
            var tint = Img("Tint", uv, null, new Color(UV.r, UV.g, UV.b, 0.1f)); Stretch(tint.rectTransform); tint.raycastTarget = false;
            var vig = Img("Vignette", uv, vignette, new Color(0.45f, 0.2f, 0.95f, 1f)); Stretch(vig.rectTransform); vig.raycastTarget = false;
            uv.gameObject.SetActive(false);

            var safe = Rect("Safe Area", canvasGO.transform);
            Stretch(safe);
            safe.gameObject.AddComponent<Cipher.SafeArea>();

            BuildCrosshair(ui, safe);
            BuildHud(ui, safe);
            BuildBottomSheet(ui, safe);
            BuildMessageSheet(ui, safe);
            BuildCaseSheet(ui, safe);
            return ui;
        }

        static void BuildCrosshair(Cipher.CipherUI ui, RectTransform parent)
        {
            var root = Rect("Crosshair", parent);
            Place(root, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(220, 220));
            ui.crosshairRoot = root;
            var spin = Rect("Spin", root); Stretch(spin);
            // тёмная подложка, чтобы прицел был виден на светлом фоне
            var shadow = Img("Shadow", spin, ringThick, new Color(0, 0, 0, 0.35f));
            Place(shadow.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(88, 88));
            shadow.raycastTarget = false;
            var ring = Img("Ring", spin, ringThin, Color.white); Place(ring.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(78, 78));
            var parts = new System.Collections.Generic.List<Graphic> { ring };
            for (int i = 0; i < 4; i++)
            {
                var tick = Img("Tick", spin, pill, Color.white);
                var rt = tick.rectTransform;
                Place(rt, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(5, 20));
                rt.localRotation = Quaternion.Euler(0, 0, i * 90f);
                rt.anchoredPosition = (Vector2)(rt.localRotation * new Vector3(0, 56, 0));
                parts.Add(tick);
            }
            var d = Img("Dot", root, dot, Color.white); Place(d.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(9, 9));
            parts.Add(d);
            foreach (var g in parts) g.raycastTarget = false;
            ui.crosshairParts = parts.ToArray();

            ui.progressRing = Img("Scan Progress", root, ringThick, Accent);
            Place(ui.progressRing.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(128, 128));
            ui.progressRing.type = Image.Type.Filled;
            ui.progressRing.fillMethod = Image.FillMethod.Radial360;
            ui.progressRing.fillOrigin = (int)Image.Origin360.Top;
            ui.progressRing.fillClockwise = true;
            ui.progressRing.raycastTarget = false;

            // подсказка-«таблетка» под прицелом, ширина подстраивается под текст
            var hint = Img("Hint", parent, pill, Glass);
            hint.type = Image.Type.Sliced;
            hint.raycastTarget = false;
            var hrt = hint.rectTransform;
            hrt.anchorMin = hrt.anchorMax = new Vector2(0.5f, 0.5f);
            hrt.pivot = new Vector2(0.5f, 1f);
            hrt.anchoredPosition = new Vector2(0, -130);
            var hl = hint.gameObject.AddComponent<HorizontalLayoutGroup>();
            hl.padding = new RectOffset(44, 44, 22, 22);
            hl.childControlWidth = hl.childControlHeight = true;
            hl.childForceExpandWidth = hl.childForceExpandHeight = false;
            var hf = hint.gameObject.AddComponent<ContentSizeFitter>();
            hf.horizontalFit = hf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            ui.hintText = Txt("Text", hint.transform, "Подсказка", 36, regular, TextMain, TextAnchor.MiddleCenter);
            ui.hintText.horizontalOverflow = HorizontalWrapMode.Overflow;
            ui.hintPill = hint.gameObject;
            hint.gameObject.SetActive(false);
            root.gameObject.SetActive(false);
        }

        static void BuildHud(Cipher.CipherUI ui, RectTransform parent)
        {
            var hud = Rect("HUD", parent);
            Stretch(hud);
            ui.hud = hud.gameObject;

            // ---- счётчик улик сверху ----
            var top = GlassCard("Counter", hud, Glass);
            var trt = top.rectTransform;
            trt.anchorMin = trt.anchorMax = new Vector2(0.5f, 1f);
            trt.pivot = new Vector2(0.5f, 1f);
            trt.anchoredPosition = new Vector2(0, -36);
            trt.sizeDelta = new Vector2(560, 132);
            var label = Txt("Label", top.transform, "УЛИКИ", 28, mono, Accent, TextAnchor.MiddleLeft);
            Anchor(label.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(44, -30), new Vector2(240, 44), new Vector2(0, 1));
            ui.counterText = Txt("Value", top.transform, "0 / 6", 46, bold, TextMain, TextAnchor.MiddleRight);
            Anchor(ui.counterText.rectTransform, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-44, -22), new Vector2(240, 60), new Vector2(1, 1));
            var track = Img("Track", top.transform, pill, new Color(1, 1, 1, 0.1f));
            track.type = Image.Type.Sliced;
            Anchor(track.rectTransform, new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 30), new Vector2(-88, 10), new Vector2(0.5f, 0));
            ui.counterFill = Img("Fill", track.transform, pill, Accent);
            ui.counterFill.type = Image.Type.Filled;
            ui.counterFill.fillMethod = Image.FillMethod.Horizontal;
            ui.counterFill.fillAmount = 0;
            Stretch(ui.counterFill.rectTransform);

            // ---- нижние кнопки ----
            ui.uvButton = DockButton("UV Button", hud, iconUV, "УФ-ФИЛЬТР", new Vector2(0, 0), new Vector2(0.5f, 0), new Vector2(36, 40), new Vector2(-14, 190), out ui.uvButtonLabel, out ui.uvButtonBg);
            ui.caseButton = DockButton("Case Button", hud, iconCase, "ДЕЛО", new Vector2(0.5f, 0), new Vector2(1, 0), new Vector2(14, 40), new Vector2(-36, 190), out _, out _);
            hud.gameObject.SetActive(false);
        }

        static Button DockButton(string name, Transform parent, Sprite icon, string label, Vector2 aMin, Vector2 aMax, Vector2 offMin, Vector2 offMax, out Text text, out Image bg)
        {
            bg = GlassCard(name, parent, Glass);
            var rt = bg.rectTransform;
            rt.anchorMin = aMin; rt.anchorMax = aMax;
            rt.offsetMin = offMin; rt.offsetMax = offMax;
            var b = bg.gameObject.AddComponent<Button>();
            b.targetGraphic = bg;
            var c = b.colors; c.pressedColor = new Color(0.7f, 0.7f, 0.7f); c.highlightedColor = Color.white; b.colors = c;

            var row = Rect("Content", bg.transform); Stretch(row);
            var hl = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            hl.childAlignment = TextAnchor.MiddleCenter;
            hl.spacing = 20;
            hl.childControlWidth = hl.childControlHeight = true;
            hl.childForceExpandWidth = hl.childForceExpandHeight = false;
            var ic = Img("Icon", row, icon, TextMain);
            ic.raycastTarget = false;
            var le = ic.gameObject.AddComponent<LayoutElement>(); le.preferredWidth = le.preferredHeight = 56;
            text = Txt("Label", row, label, 38, bold, TextMain, TextAnchor.MiddleCenter);
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            return b;
        }

        static RectTransform Sheet(string name, RectTransform parent, Color color, int top, int bottom)
        {
            var bg = GlassCard(name, parent, color);
            var rt = bg.rectTransform;
            rt.anchorMin = new Vector2(0, 0); rt.anchorMax = new Vector2(1, 0);
            rt.pivot = new Vector2(0.5f, 0);
            rt.offsetMin = new Vector2(28, 28); rt.offsetMax = new Vector2(-28, 600);
            var vl = bg.gameObject.AddComponent<VerticalLayoutGroup>();
            vl.padding = new RectOffset(56, 56, top, bottom);
            vl.spacing = 26;
            vl.childControlWidth = vl.childControlHeight = true;
            vl.childForceExpandWidth = true; vl.childForceExpandHeight = false;
            bg.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            bg.gameObject.AddComponent<CanvasGroup>();
            // «ручка» сверху, как у нижних листов iOS
            var handle = Img("Handle", bg.transform, pill, new Color(1, 1, 1, 0.22f));
            handle.type = Image.Type.Sliced;
            handle.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            Anchor(handle.rectTransform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -24), new Vector2(96, 10), new Vector2(0.5f, 1));
            return rt;
        }

        static void BuildBottomSheet(Cipher.CipherUI ui, RectTransform parent)
        {
            var rt = Sheet("Bottom Sheet", parent, GlassStrong, 60, 50);
            ui.bottomSheet = rt;
            ui.bottomTitle = Txt("Title", rt, "Сканирование комнаты", 50, bold, TextMain, TextAnchor.UpperLeft);
            ui.bottomText = Txt("Text", rt, "Медленно поводите телефоном по комнате.", 36, regular, TextSoft, TextAnchor.UpperLeft);

            var stats = Rect("Stats", rt);
            var hl = stats.gameObject.AddComponent<HorizontalLayoutGroup>();
            hl.spacing = 20;
            hl.childControlWidth = hl.childControlHeight = true;
            hl.childForceExpandWidth = true; hl.childForceExpandHeight = true;
            stats.gameObject.AddComponent<LayoutElement>().preferredHeight = 150;
            ui.statTables = Stat(stats, "СТОЛЫ");
            ui.statWalls = Stat(stats, "СТЕНЫ");
            ui.statFloors = Stat(stats, "ПОЛ");
            ui.statsRow = stats.gameObject;

            ui.bottomButton = PrimaryButton("Button", rt, "Разместить улики", out ui.bottomButtonLabel, out _);
            rt.gameObject.SetActive(false);
        }

        static Text Stat(RectTransform parent, string label)
        {
            var chip = Img(label, parent, card, new Color(1, 1, 1, 0.05f));
            chip.type = Image.Type.Sliced;
            var vl = chip.gameObject.AddComponent<VerticalLayoutGroup>();
            vl.childAlignment = TextAnchor.MiddleCenter;
            vl.padding = new RectOffset(10, 10, 18, 18);
            vl.spacing = 4;
            vl.childControlWidth = vl.childControlHeight = true;
            vl.childForceExpandWidth = true; vl.childForceExpandHeight = false;
            var value = Txt("Value", chip.transform, "0", 56, bold, Accent, TextAnchor.MiddleCenter);
            Txt("Label", chip.transform, label, 24, mono, TextSoft, TextAnchor.MiddleCenter);
            return value;
        }

        static void BuildMessageSheet(Cipher.CipherUI ui, RectTransform parent)
        {
            var rt = Sheet("Message Sheet", parent, GlassStrong, 66, 52);
            ui.messageSheet = rt;
            ui.messageEyebrow = Txt("Eyebrow", rt, "УЛИКА 1 ИЗ 6", 28, mono, Accent, TextAnchor.UpperLeft);
            ui.messageTitle = Txt("Title", rt, "Заголовок", 62, bold, TextMain, TextAnchor.UpperLeft);
            ui.messageTitle.lineSpacing = 1f;
            ui.messageBody = Txt("Body", rt, "Текст сообщения.", 38, regular, new Color(0.85f, 0.9f, 0.97f), TextAnchor.UpperLeft);
            ui.messageBody.lineSpacing = 1.15f;

            var lesson = Img("Lesson", rt, card, new Color(Accent.r, Accent.g, Accent.b, 0.1f));
            lesson.type = Image.Type.Sliced;
            var hl = lesson.gameObject.AddComponent<HorizontalLayoutGroup>();
            hl.padding = new RectOffset(28, 32, 26, 28);
            hl.spacing = 26;
            hl.childControlWidth = hl.childControlHeight = true;
            hl.childForceExpandWidth = false; hl.childForceExpandHeight = true;
            ui.lessonBar = Img("Bar", lesson.transform, pill, Accent);
            ui.lessonBar.type = Image.Type.Sliced;
            var ble = ui.lessonBar.gameObject.AddComponent<LayoutElement>(); ble.preferredWidth = 8; ble.minWidth = 8;
            ui.lessonText = Txt("Text", lesson.transform, "<b>Как защититься</b>\nСовет.", 34, regular, TextMain, TextAnchor.UpperLeft);
            ui.lessonText.lineSpacing = 1.12f;
            ui.lessonText.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1;
            ui.lessonBlock = lesson.gameObject;

            ui.messageButton = PrimaryButton("Button", rt, "Далее", out ui.messageButtonLabel, out ui.messageButtonBg);
            rt.gameObject.SetActive(false);
        }

        static void BuildCaseSheet(Cipher.CipherUI ui, RectTransform parent)
        {
            var bg = GlassCard("Case Sheet", parent, GlassStrong);
            var rt = bg.rectTransform;
            rt.anchorMin = new Vector2(0, 0); rt.anchorMax = new Vector2(1, 1);
            rt.offsetMin = new Vector2(28, 28); rt.offsetMax = new Vector2(-28, -28);
            bg.gameObject.AddComponent<CanvasGroup>();
            ui.caseSheet = rt;

            var eyebrow = Txt("Eyebrow", rt, "МАТЕРИАЛЫ РАССЛЕДОВАНИЯ", 28, mono, Accent, TextAnchor.UpperLeft);
            Anchor(eyebrow.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(56, -60), new Vector2(-220, 40), new Vector2(0, 1));
            var title = Txt("Title", rt, "Найденные улики", 62, bold, TextMain, TextAnchor.UpperLeft);
            Anchor(title.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(56, -108), new Vector2(-220, 80), new Vector2(0, 1));

            var close = Img("Close", rt, pill, new Color(1, 1, 1, 0.08f));
            close.type = Image.Type.Sliced;
            Anchor(close.rectTransform, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-44, -50), new Vector2(110, 110), new Vector2(1, 1));
            ui.caseCloseButton = close.gameObject.AddComponent<Button>();
            ui.caseCloseButton.targetGraphic = close;
            var x = Img("Icon", close.transform, iconClose, TextMain);
            Place(x.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(56, 56));
            x.raycastTarget = false;

            var scroll = new GameObject("Scroll", typeof(RectTransform), typeof(RectMask2D), typeof(ScrollRect));
            scroll.transform.SetParent(rt, false);
            var srt = (RectTransform)scroll.transform;
            srt.anchorMin = Vector2.zero; srt.anchorMax = Vector2.one;
            srt.offsetMin = new Vector2(40, 40); srt.offsetMax = new Vector2(-40, -220);
            var content = Rect("List", srt);
            content.anchorMin = new Vector2(0, 1); content.anchorMax = new Vector2(1, 1); content.pivot = new Vector2(0.5f, 1);
            content.offsetMin = content.offsetMax = Vector2.zero;
            var vl = content.gameObject.AddComponent<VerticalLayoutGroup>();
            vl.spacing = 22;
            vl.padding = new RectOffset(0, 0, 0, 40);
            vl.childControlWidth = vl.childControlHeight = true;
            vl.childForceExpandWidth = true; vl.childForceExpandHeight = false;
            content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            var sr = scroll.GetComponent<ScrollRect>();
            sr.content = content; sr.horizontal = false; sr.vertical = true;
            sr.movementType = ScrollRect.MovementType.Elastic;
            sr.scrollSensitivity = 30;
            ui.caseList = content;

            ui.caseEmpty = Txt("Empty", content, "Пока ничего не найдено. Осмотрите комнату и изучите подозрительные предметы.", 36, regular, TextSoft, TextAnchor.UpperLeft);

            // шаблон записи
            var entry = Img("Entry Template", content, card, new Color(1, 1, 1, 0.045f));
            entry.type = Image.Type.Sliced;
            var ehl = entry.gameObject.AddComponent<HorizontalLayoutGroup>();
            ehl.padding = new RectOffset(30, 34, 30, 32);
            ehl.spacing = 28;
            ehl.childControlWidth = ehl.childControlHeight = true;
            ehl.childForceExpandWidth = false; ehl.childForceExpandHeight = true;
            var bar = Img("Bar", entry.transform, pill, Accent);
            bar.type = Image.Type.Sliced;
            var bl = bar.gameObject.AddComponent<LayoutElement>(); bl.preferredWidth = 8; bl.minWidth = 8;
            var col = Rect("Column", entry.transform);
            var cvl = col.gameObject.AddComponent<VerticalLayoutGroup>();
            cvl.spacing = 10;
            cvl.childControlWidth = cvl.childControlHeight = true;
            cvl.childForceExpandWidth = true; cvl.childForceExpandHeight = false;
            col.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1;
            Txt("Title", col, "1. Улика", 42, bold, TextMain, TextAnchor.UpperLeft);
            var body = Txt("Body", col, "Описание улики.", 34, regular, TextSoft, TextAnchor.UpperLeft);
            body.lineSpacing = 1.12f;
            ui.caseEntryTemplate = entry.gameObject;
            entry.gameObject.SetActive(false);

            rt.gameObject.SetActive(false);
        }

        // ---- помощники для UI ----

        static Image GlassCard(string name, Transform parent, Color color)
        {
            var bg = Img(name, parent, card, color);
            bg.type = Image.Type.Sliced;
            var o = Img("Outline", bg.transform, outline, new Color(Accent.r, Accent.g, Accent.b, 0.22f));
            o.type = Image.Type.Sliced;
            o.raycastTarget = false;
            o.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            Stretch(o.rectTransform);
            return bg;
        }

        static Button PrimaryButton(string name, Transform parent, string label, out Text text, out Image bg)
        {
            bg = Img(name, parent, card, Accent);
            bg.type = Image.Type.Sliced;
            bg.gameObject.AddComponent<LayoutElement>().preferredHeight = 140;
            var b = bg.gameObject.AddComponent<Button>();
            b.targetGraphic = bg;
            var c = b.colors;
            c.pressedColor = new Color(0.78f, 0.78f, 0.78f);
            c.disabledColor = new Color(0.45f, 0.45f, 0.45f, 0.45f);
            b.colors = c;
            text = Txt("Label", bg.transform, label, 40, bold, Ink, TextAnchor.MiddleCenter);
            Stretch(text.rectTransform);
            return b;
        }

        static RectTransform Rect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        static Image Img(string name, Transform parent, Sprite sprite, Color color)
        {
            var rt = Rect(name, parent);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = sprite;
            img.color = color;
            return img;
        }

        static Text Txt(string name, Transform parent, string text, int size, Font font, Color color, TextAnchor anchor)
        {
            var rt = Rect(name, parent);
            var t = rt.gameObject.AddComponent<Text>();
            t.text = text;
            t.font = font;
            t.fontSize = size;
            t.color = color;
            t.alignment = anchor;
            t.supportRichText = true;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.lineSpacing = 1.1f;
            t.raycastTarget = false;
            return t;
        }

        static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }

        static void Anchor(RectTransform rt, Vector2 aMin, Vector2 aMax, Vector2 pos, Vector2 size, Vector2 pivot)
        {
            rt.anchorMin = aMin; rt.anchorMax = aMax; rt.pivot = pivot;
            rt.anchoredPosition = pos; rt.sizeDelta = size;
        }

        static void Place(RectTransform rt, Vector2 anchor, Vector2 pos, Vector2 size)
        {
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
        }
    }
}
