using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace Cipher
{
    /// <summary>
    /// Материалы, примитивы и текст, которые игра создаёт прямо из кода
    /// (никаких внешних 3D-моделей не нужно).
    /// </summary>
    public static class CipherVisuals
    {
        public const float StencilAlways = 8f;  // рисовать везде
        public const float StencilEqual = 3f;   // рисовать только внутри портала

        static Shader holoShader, flatShader, maskShader;
        static Font font;
        static readonly List<Material> textMaterials = new List<Material>();

        static Shader Holo => holoShader ? holoShader : holoShader = Resources.Load<Shader>("CipherHologram");
        static Shader FlatS => flatShader ? flatShader : flatShader = Resources.Load<Shader>("CipherFlat");
        static Shader Mask => maskShader ? maskShader : maskShader = Resources.Load<Shader>("CipherPortalMask");

        public static Font Font
        {
            get
            {
                if (font) return font;
                try { font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); } catch { }
                if (!font) font = Font.CreateDynamicFontFromOSFont(new[] { "Arial", "Roboto", "Segoe UI" }, 64);
                Font.textureRebuilt += f =>
                {
                    if (f != font) return;
                    foreach (var m in textMaterials) if (m) m.mainTexture = font.material.mainTexture;
                };
                return font;
            }
        }

        // ---------- материалы ----------

        public static Material Hologram(Color color, float alpha = 0.55f, float rimPower = 2f, float glow = 1f, float scan = 0f, bool insidePortal = false)
        {
            var m = new Material(Holo);
            m.SetColor("_Color", color);
            m.SetColor("_RimColor", Color.Lerp(color, Color.white, 0.6f));
            m.SetFloat("_Alpha", alpha);
            m.SetFloat("_RimPower", rimPower);
            m.SetFloat("_Glow", glow);
            m.SetFloat("_Scan", scan);
            m.SetFloat("_StencilComp", insidePortal ? StencilEqual : StencilAlways);
            return m;
        }

        public static Material Flat(Color color, bool insidePortal = false, int queue = 3000)
        {
            var m = new Material(FlatS);
            m.SetColor("_Color", color);
            m.SetFloat("_StencilComp", insidePortal ? StencilEqual : StencilAlways);
            m.renderQueue = queue;
            return m;
        }

        /// <summary>Непрозрачный для глубины фон панели: то, что за панелью, не просвечивает.</summary>
        public static Material PanelBackground(Color color)
        {
            var m = Flat(color, false, 2990);
            m.SetFloat("_ZWrite", 1f);
            return m;
        }

        public static Material TextMaterial(Color color, bool insidePortal = false)
        {
            var m = Flat(color, insidePortal, 3010);
            m.mainTexture = Font.material.mainTexture;
            textMaterials.Add(m);
            return m;
        }

        public static Material PortalMask() => new Material(Mask);

        // ---------- объекты ----------

        public static GameObject Prim(PrimitiveType type, Transform parent, Vector3 localPos, Vector3 localScale, Material mat,
                                      bool keepCollider = false, string name = null)
        {
            var go = GameObject.CreatePrimitive(type);
            if (name != null) go.name = name;
            if (!keepCollider) Object.Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = localScale;
            var r = go.GetComponent<Renderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            return go;
        }

        /// <summary>3D-текст. letterHeight — высота букв в метрах.</summary>
        public static TextMesh Text(Transform parent, string text, Vector3 localPos, float letterHeight, Color color,
                                    TextAnchor anchor = TextAnchor.MiddleCenter, bool insidePortal = false, FontStyle style = FontStyle.Normal)
        {
            var go = new GameObject("Text");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            var tm = go.AddComponent<TextMesh>();
            tm.font = Font;
            tm.fontSize = 64;
            tm.fontStyle = style;
            tm.characterSize = letterHeight * 10f / tm.fontSize;
            tm.anchor = anchor;
            tm.alignment = anchor == TextAnchor.UpperLeft || anchor == TextAnchor.MiddleLeft ? TextAlignment.Left : TextAlignment.Center;
            tm.richText = false;
            tm.text = text;
            tm.color = Color.white;
            go.GetComponent<MeshRenderer>().sharedMaterial = TextMaterial(color, insidePortal);
            return tm;
        }

        public static void SetTextColor(TextMesh tm, Color c) => tm.GetComponent<MeshRenderer>().sharedMaterial.SetColor("_Color", c);

        /// <summary>Перенос строк по словам: TextMesh сам этого не умеет.</summary>
        public static string Wrap(string text, int maxChars)
        {
            var sb = new StringBuilder();
            foreach (var paragraph in text.Split('\n'))
            {
                int line = 0;
                foreach (var word in paragraph.Split(' '))
                {
                    if (word.Length == 0) continue;
                    if (line > 0 && line + 1 + word.Length > maxChars) { sb.Append('\n'); line = 0; }
                    else if (line > 0) { sb.Append(' '); line++; }
                    sb.Append(word); line += word.Length;
                }
                sb.Append('\n');
            }
            return sb.ToString().TrimEnd('\n');
        }

        public static int LineCount(string s) => string.IsNullOrEmpty(s) ? 0 : s.Split('\n').Length;

        /// <summary>Сколько символов помещается в строку заданной ширины.</summary>
        public static int CharsFor(float width, float letterHeight) => Mathf.Max(8, Mathf.FloorToInt(width / (letterHeight * 0.56f)));

        public static LineRenderer Line(Transform parent, Color color, float width, bool insidePortal = false)
        {
            var go = new GameObject("Line");
            go.transform.SetParent(parent, false);
            var lr = go.AddComponent<LineRenderer>();
            lr.sharedMaterial = Flat(color, insidePortal);
            lr.widthMultiplier = width;
            lr.useWorldSpace = true;
            lr.numCapVertices = 4;
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lr.receiveShadows = false;
            return lr;
        }
    }
}
