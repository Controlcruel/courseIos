using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.XR.ARFoundation;

namespace Cipher.EditorTools
{
    /// <summary>
    /// Создаёт ресурсы игры: текстуры (отпечаток пальца, кольца прицела), материалы,
    /// префабы улик-заготовок и файл «Дело» с сюжетом. Уже существующие файлы НЕ перезаписываются,
    /// чтобы не стереть ваши правки (кроме пункта «пересоздать»).
    /// </summary>
    public static class CipherAssetBuilder
    {
        public const string Root = "Assets/Cipher/Generated";
        public const string TexDir = Root + "/Textures";
        public const string MatDir = Root + "/Materials";
        public const string PrefabDir = "Assets/Cipher/Prefabs";
        public const string CluesDir = PrefabDir + "/Clues";
        public const string CasePath = "Assets/Cipher/Data/Case_NovaSoft.asset";
        public const string PlanePrefabPath = PrefabDir + "/ScanPlane.prefab";
        public const string FontRegular = "Assets/Cipher/Fonts/Inter-Regular.otf";
        public const string FontBold = "Assets/Cipher/Fonts/Inter-Bold.otf";
        public const string FontMono = "Assets/Cipher/Fonts/DejaVuSansMono.ttf";

        static bool force;

        [MenuItem("Cipher/2. Создать ресурсы и улики", priority = 2)]
        public static void BuildMissing() { force = false; BuildAll(); }

        [MenuItem("Cipher/Пересоздать улики и «Дело» (сотрёт правки)", priority = 30)]
        public static void Rebuild()
        {
            if (!EditorUtility.DisplayDialog("Пересоздать?", "Префабы улик и файл «Дело» будут созданы заново. Ваши изменения в них пропадут.", "Пересоздать", "Отмена")) return;
            force = true; BuildAll(); force = false;
        }

        public static void BuildAll()
        {
            foreach (var d in new[] { TexDir, MatDir, CluesDir, "Assets/Cipher/Data" }) EnsureFolder(d);
            BuildTextures();
            BuildPlanePrefab();
            var clues = BuildCluePrefabs();
            BuildCase(clues);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[Cipher] Ресурсы готовы: текстуры, материалы, " + clues.Count + " улик, файл «Дело». Префабы улик: " + CluesDir);
        }

        // ================= текстуры =================

        public static string TexPath(string name) => TexDir + "/" + name + ".png";

        static void BuildTextures()
        {
            WriteTexture("Fingerprint", Fingerprint(256, 320), false, Vector4.zero);
            WriteTexture("RingThin", Ring(256, 0.035f), true, Vector4.zero);
            WriteTexture("RingThick", Ring(256, 0.1f), true, Vector4.zero);
            WriteTexture("Dot", Ring(64, 0.5f), true, Vector4.zero);
            WriteTexture("Rounded", Rounded(96, 30), true, new Vector4(36, 36, 36, 36));
            WriteTexture("Card", Rounded(128, 44), true, new Vector4(50, 50, 50, 50));
            WriteTexture("CardOutline", RoundedOutline(128, 44, 2.5f), true, new Vector4(50, 50, 50, 50));
            WriteTexture("Pill", Rounded(128, 64), true, new Vector4(63, 63, 63, 63));
            WriteTexture("Vignette", Vignette(256), true, Vector4.zero);
            WriteTexture("IconUV", IconUV(128), true, Vector4.zero);
            WriteTexture("IconCase", IconCase(128), true, Vector4.zero);
            WriteTexture("IconClose", IconClose(128), true, Vector4.zero);
        }

        static void WriteTexture(string name, Texture2D tex, bool sprite, Vector4 border)
        {
            string path = TexPath(name);
            if (File.Exists(path) && !force) return;
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path);
            var ti = (TextureImporter)AssetImporter.GetAtPath(path);
            ti.alphaIsTransparency = true;
            ti.wrapMode = TextureWrapMode.Clamp;
            ti.mipmapEnabled = !sprite;
            if (sprite)
            {
                ti.textureType = TextureImporterType.Sprite;
                ti.spriteImportMode = SpriteImportMode.Single;
                ti.spriteBorder = border;
            }
            ti.SaveAndReimport();
        }

        static Texture2D Ring(int size, float thickness)
        {
            var t = new Texture2D(size, size, TextureFormat.RGBA32, false);
            float outer = 0.5f, inner = 0.5f - thickness, aa = 1.5f / size;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float d = Vector2.Distance(new Vector2((x + 0.5f) / size, (y + 0.5f) / size), new Vector2(0.5f, 0.5f));
                    float a = Mathf.Clamp01((outer - d) / aa) * Mathf.Clamp01((d - inner) / aa);
                    t.SetPixel(x, y, new Color(1, 1, 1, a));
                }
            t.Apply();
            return t;
        }

        static Texture2D Rounded(int size, int radius)
        {
            var t = new Texture2D(size, size, TextureFormat.RGBA32, false);
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float cx = Mathf.Clamp(x + 0.5f, radius, size - radius), cy = Mathf.Clamp(y + 0.5f, radius, size - radius);
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(cx, cy));
                    t.SetPixel(x, y, new Color(1, 1, 1, Mathf.Clamp01(radius - d + 0.5f)));
                }
            t.Apply();
            return t;
        }

        static float RoundedDist(float x, float y, int size, int radius)
        {
            float cx = Mathf.Clamp(x, radius, size - radius), cy = Mathf.Clamp(y, radius, size - radius);
            return Vector2.Distance(new Vector2(x, y), new Vector2(cx, cy)) - radius; // <0 внутри
        }

        static Texture2D RoundedOutline(int size, int radius, float width)
        {
            var t = new Texture2D(size, size, TextureFormat.RGBA32, false);
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float d = RoundedDist(x + 0.5f, y + 0.5f, size, radius);
                    float a = Mathf.Clamp01(0.5f - d) * Mathf.Clamp01(d + width + 0.5f);
                    t.SetPixel(x, y, new Color(1, 1, 1, a));
                }
            t.Apply();
            return t;
        }

        static Texture2D Vignette(int size)
        {
            var t = new Texture2D(size, size, TextureFormat.RGBA32, false);
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float dx = (x + 0.5f) / size - 0.5f, dy = (y + 0.5f) / size - 0.5f;
                    float d = Mathf.Sqrt(dx * dx + dy * dy) * 2f;
                    t.SetPixel(x, y, new Color(1, 1, 1, Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.45f, 1.25f, d)) * 0.85f));
                }
            t.Apply();
            return t;
        }

        static Texture2D Icon(int size, System.Func<float, float, float> sdf)
        {
            // sdf: расстояние до фигуры в долях размера (отрицательное — внутри)
            var t = new Texture2D(size, size, TextureFormat.RGBA32, false);
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float d = sdf((x + 0.5f) / size, (y + 0.5f) / size) * size;
                    t.SetPixel(x, y, new Color(1, 1, 1, Mathf.Clamp01(0.5f - d)));
                }
            t.Apply();
            return t;
        }

        static float Seg(float px, float py, float ax, float ay, float bx, float by, float r)
        {
            var p = new Vector2(px, py); var a = new Vector2(ax, ay); var b = new Vector2(bx, by);
            float h = Mathf.Clamp01(Vector2.Dot(p - a, b - a) / Vector2.Dot(b - a, b - a));
            return Vector2.Distance(p, a + (b - a) * h) - r;
        }

        static float SdBox(float px, float py, float cx, float cy, float hx, float hy, float r)
        {
            float qx = Mathf.Abs(px - cx) - hx + r, qy = Mathf.Abs(py - cy) - hy + r;
            return new Vector2(Mathf.Max(qx, 0), Mathf.Max(qy, 0)).magnitude + Mathf.Min(Mathf.Max(qx, qy), 0) - r;
        }

        /// <summary>«Солнце» — УФ-лампа.</summary>
        static Texture2D IconUV(int size) => Icon(size, (x, y) =>
        {
            float d = Mathf.Abs(Vector2.Distance(new Vector2(x, y), new Vector2(0.5f, 0.5f)) - 0.17f) - 0.045f;
            for (int i = 0; i < 8; i++)
            {
                float a = i * Mathf.PI / 4f;
                float c = Mathf.Cos(a), s = Mathf.Sin(a);
                d = Mathf.Min(d, Seg(x, y, 0.5f + c * 0.3f, 0.5f + s * 0.3f, 0.5f + c * 0.41f, 0.5f + s * 0.41f, 0.04f));
            }
            return d;
        });

        /// <summary>Папка с делом.</summary>
        static Texture2D IconCase(int size) => Icon(size, (x, y) =>
        {
            float body = SdBox(x, y, 0.5f, 0.44f, 0.36f, 0.25f, 0.06f);
            float tab = SdBox(x, y, 0.3f, 0.7f, 0.16f, 0.07f, 0.04f);
            float shape = Mathf.Min(body, tab);
            float hole = SdBox(x, y, 0.5f, 0.42f, 0.29f, 0.18f, 0.03f);
            float outline = Mathf.Max(shape, -hole);
            float line = Seg(x, y, 0.3f, 0.46f, 0.7f, 0.46f, 0.03f);
            float line2 = Seg(x, y, 0.3f, 0.35f, 0.58f, 0.35f, 0.03f);
            return Mathf.Min(outline, Mathf.Min(line, line2));
        });

        static Texture2D IconClose(int size) => Icon(size, (x, y) =>
            Mathf.Min(Seg(x, y, 0.3f, 0.3f, 0.7f, 0.7f, 0.045f), Seg(x, y, 0.3f, 0.7f, 0.7f, 0.3f, 0.045f)));

        /// <summary>Процедурный отпечаток пальца: «петля» из изогнутых линий с разрывами.</summary>
        static Texture2D Fingerprint(int w, int h)
        {
            var t = new Texture2D(w, h, TextureFormat.RGBA32, true);
            var rnd = new System.Random(417);
            float ox = (float)rnd.NextDouble() * 100f, oy = (float)rnd.NextDouble() * 100f;
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float nx = (x - w * 0.5f) / (w * 0.44f), ny = (y - h * 0.48f) / (h * 0.46f);
                    float r = Mathf.Sqrt(nx * nx + ny * ny);
                    // ядро «петли» чуть ниже центра
                    float cx = nx, cy = ny + 0.18f;
                    float core = Mathf.Sqrt(cx * cx * 1.6f + cy * cy);
                    float ang = Mathf.Atan2(cy, cx);
                    float warp = 0.05f * Mathf.Sin(ang * 2f + core * 5f) + 0.06f * (Mathf.PerlinNoise(ox + x * 0.02f, oy + y * 0.02f) - 0.5f);
                    float d = core + warp + 0.12f * Mathf.Max(0, ny - 0.2f);
                    float ridge = 0.5f + 0.5f * Mathf.Sin(d * Mathf.PI * 2f * 13f);
                    float v = Mathf.SmoothStep(0.35f, 0.75f, ridge);
                    float breaks = Mathf.PerlinNoise(ox + x * 0.09f, oy + y * 0.09f);
                    if (breaks > 0.72f) v *= 0.15f;
                    float mask = 1f - Mathf.SmoothStep(0.82f, 1f, r);
                    float dust = 0.65f + 0.35f * Mathf.PerlinNoise(ox + x * 0.2f, oy + y * 0.2f);
                    t.SetPixel(x, y, new Color(1, 1, 1, Mathf.Clamp01(v * mask * dust)));
                }
            t.Apply();
            return t;
        }

        // ================= материалы =================

        public static Material Mat(string name, Shader shader, System.Action<Material> setup)
        {
            string path = MatDir + "/" + name + ".mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m && !force) return m;
            if (!m) { m = new Material(shader); AssetDatabase.CreateAsset(m, path); }
            else m.shader = shader;
            setup(m);
            EditorUtility.SetDirty(m);
            return m;
        }

        static Shader S(string name)
        {
            var s = Shader.Find(name);
            if (!s) Debug.LogError("[Cipher] Не найден шейдер " + name);
            return s;
        }

        public static Material Holo(string name, Color c, float alpha = 0.5f, float scan = 0f) => Mat(name, S("Cipher/Hologram"), m =>
        {
            m.SetColor("_Color", c);
            m.SetColor("_RimColor", Color.Lerp(c, Color.white, 0.6f));
            m.SetFloat("_Alpha", alpha);
            m.SetFloat("_RimPower", 2f);
            m.SetFloat("_Glow", 1f);
            m.SetFloat("_Scan", scan);
        });

        public static Material Lit(string name, Color c, float smooth = 0.3f, float metal = 0f) => Mat(name, S("Universal Render Pipeline/Lit"), m =>
        {
            m.SetColor("_BaseColor", c);
            m.SetFloat("_Smoothness", smooth);
            m.SetFloat("_Metallic", metal);
        });

        public static Material FlatMat(string name, Color c, Texture tex = null, bool zwrite = false, int queue = 3000) => Mat(name, S("Cipher/Flat"), m =>
        {
            m.SetColor("_Color", c);
            if (tex) m.SetTexture("_MainTex", tex);
            m.SetFloat("_ZWrite", zwrite ? 1 : 0);
            m.SetFloat("_Cull", 0);
            m.renderQueue = queue;
        });

        // ================= префаб плоскости =================

        static void BuildPlanePrefab()
        {
            if (File.Exists(PlanePrefabPath) && !force) return;
            var go = new GameObject("ScanPlane");
            go.AddComponent<MeshFilter>();
            go.AddComponent<MeshRenderer>().sharedMaterial = FlatMat("PlaneScan", new Color(0.3f, 0.95f, 1f, 0.16f));
            var lr = go.AddComponent<LineRenderer>();
            lr.sharedMaterial = FlatMat("PlaneBorder", new Color(0.3f, 0.95f, 1f, 0.7f));
            lr.widthMultiplier = 0.006f;
            lr.useWorldSpace = false;
            lr.loop = true;
            go.AddComponent<ARPlaneMeshVisualizer>();
            PrefabUtility.SaveAsPrefabAsset(go, PlanePrefabPath);
            Object.DestroyImmediate(go);
        }

        // ================= улики =================

        static Font font;

        static List<CipherClue> BuildCluePrefabs()
        {
            font = AssetDatabase.LoadAssetAtPath<Font>(FontBold);
            var list = new List<CipherClue>
            {
                Clue("01_PhishingPrintout", BuildPrintout, c =>
                {
                    c.title = "Письмо «от поддержки»";
                    c.time = "18:02";
                    c.description = "Распечатка письма, которое Марина Белова получила в 18:02: «Срочно смените пароль и установите обновление " +
                                    "бухгалтерской программы с флешки из посылки». Отправитель support@nova-soft.co, а настоящий домен компании nova-soft.ru. " +
                                    "Подпись «Отдел ИТ», хотя единственный сисадмин, Олег Ковалёв, всегда подписывается своим именем.";
                    c.lesson = "Проверяй адрес отправителя и не переходи по ссылкам из «срочных» писем. Сомневаешься — позвони отправителю.";
                    c.color = new Color(1f, 0.82f, 0.25f);
                    c.surface = SurfaceType.Table; c.attackStep = 1; c.scanTime = 1.0f;
                }),
                Clue("02_PasswordSticker", BuildSticker, c =>
                {
                    c.title = "Стикер с паролем";
                    c.time = "18:05";
                    c.description = "На стене у рабочего места стикер: «Nova2024». В 18:05 Марина ввела этот пароль на поддельной странице из письма. " +
                                    "Тот же пароль стоит у неё на почте и в базе клиентов, двухфакторной защиты нет.";
                    c.lesson = "Используй сложные уникальные пароли, храни их в менеджере паролей и включай двухфакторную аутентификацию.";
                    c.color = new Color(1f, 0.55f, 0.2f);
                    c.surface = SurfaceType.Wall; c.randomYaw = false; c.surfaceOffset = 0.008f; c.attackStep = 2; c.scanTime = 1.0f;
                }),
                Clue("03_UsbDrive", BuildUsb, c =>
                {
                    c.title = "Флешка из посылки";
                    c.time = "18:10";
                    c.description = "Флешку принёс курьер в 17:50 в посылке «от партнёра». В 18:10 Марина, как велело письмо, запустила с неё " +
                                    "invoice_update.exe. Это бэкдор: программа для скрытого удалённого управления компьютером. " +
                                    "В свойствах файла указан автор: DErshov-PC.";
                    c.lesson = "Не вставляй флешки из непроверенных посылок и не запускай неизвестные файлы. Обновления ставит только сисадмин.";
                    c.color = new Color(1f, 0.25f, 0.3f);
                    c.surface = SurfaceType.Floor; c.attackStep = 3; c.scanTime = 1.2f;
                }),
                Clue("04_NetworkTraffic", BuildTraffic, c =>
                {
                    c.title = "Ночной трафик";
                    c.time = "03:12";
                    c.description = "В 03:12 компьютер Марины сам начал передавать данные на сервер 203.0.113.66. В офисе в это время никого не было: " +
                                    "бэкдором управляли удалённо. Оповещение о тревоге ушло сисадмину только через 20 минут.";
                    c.lesson = "Настрой межсетевой экран и мгновенные оповещения о необычной активности в сети, особенно ночью.";
                    c.color = new Color(0.25f, 0.85f, 1f);
                    c.surface = SurfaceType.Wall; c.randomYaw = false; c.surfaceOffset = 0.18f; c.attackStep = 4; c.scanTime = 1.2f;
                }),
                Clue("05_DatabaseLeak", BuildDatabase, c =>
                {
                    c.title = "Журнал сервера";
                    c.time = "03:15";
                    c.description = "С 03:15 под учётной записью Марины выгружено 12 000 из 40 000 записей клиентов. В 03:41 выгрузка оборвалась: " +
                                    "«сетевой кабель отключён». Ещё одна строка: в 02:50 с того же адреса 203.0.113.66 пытались войти " +
                                    "под учётной записью d.ershov, заблокированной месяц назад.";
                    c.lesson = "Давай сотрудникам только нужный доступ, шифруй данные и отключай учётные записи уволенных в день ухода.";
                    c.color = new Color(0.7f, 0.4f, 1f);
                    c.surface = SurfaceType.Table; c.attackStep = 5; c.scanTime = 1.2f;
                }),
                Clue("06_Fingerprint", BuildFingerprint, c =>
                {
                    c.title = "Отпечаток пальца";
                    c.time = "";
                    c.description = "Под УФ-светом у сетевого разъёма на столе Марины проявился отпечаток. Совпадение с базой сотрудников: " +
                                    "Олег Ковалёв, системный администратор, 87%. Значит, ночью он был у этого компьютера. Но когда именно?";
                    c.lesson = "Физический доступ тоже угроза: блокируй компьютер (Win + L), когда отходишь от рабочего места.";
                    c.color = new Color(0.4f, 1f, 1f);
                    c.surface = SurfaceType.Table; c.onlyVisibleInUV = true; c.isAttackStep = false;
                    c.scanDistance = 1.0f; c.scanTime = 1.8f; c.surfaceOffset = 0.012f;
                }),
                Clue("07_AccessLog", BuildAccessLog, c =>
                {
                    c.title = "Журнал пропусков";
                    c.time = "03:38";
                    c.description = "Терминал пропусков у двери. Записи за ночь: Марина Белова вышла в 18:30. Олег Ковалёв вошёл в 03:38 " +
                                    "и вышел в 04:20. Больше в офис никто не входил.";
                    c.lesson = "При тревоге сразу отключай заражённый компьютер от сети: каждая минута промедления — новые украденные записи.";
                    c.color = new Color(0.35f, 1f, 0.55f);
                    c.surface = SurfaceType.Wall; c.randomYaw = false; c.surfaceOffset = 0.01f; c.isAttackStep = false; c.scanTime = 1.2f;
                }),
            };
            return list;
        }

        static CipherClue Clue(string name, System.Action<Transform> buildModel, System.Action<CipherClue> setup)
        {
            string path = CluesDir + "/" + name + ".prefab";
            var existing = AssetDatabase.LoadAssetAtPath<CipherClue>(path);
            if (existing && !force) return existing;

            var root = new GameObject(name);
            var model = new GameObject("Model").transform;
            model.SetParent(root.transform, false);
            buildModel(model);
            var clue = root.AddComponent<CipherClue>();
            clue.model = model;
            setup(clue);
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            return prefab.GetComponent<CipherClue>();
        }

        static GameObject Box(Transform parent, PrimitiveType type, Vector3 pos, Vector3 scale, Material mat, Vector3 euler = default)
        {
            var go = GameObject.CreatePrimitive(type);
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localEulerAngles = euler;
            go.transform.localScale = scale;
            var r = go.GetComponent<Renderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return go;
        }

        /// <summary>Текст, лежащий на поверхности (читается сверху; верх строки смотрит по оси Z).</summary>
        static TextMesh FlatText(Transform parent, string text, Vector3 pos, float letterHeight, Color color, TextAnchor anchor = TextAnchor.MiddleCenter)
        {
            var go = new GameObject("Text");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localEulerAngles = new Vector3(90, 0, 0);
            var tm = go.AddComponent<TextMesh>();
            tm.font = font;
            tm.fontSize = 64;
            tm.characterSize = letterHeight * 10f / tm.fontSize;
            tm.anchor = anchor;
            tm.alignment = TextAlignment.Left;
            tm.color = color;
            tm.text = text;
            go.GetComponent<MeshRenderer>().sharedMaterial = font.material;
            return tm;
        }

        // ---- модели (всё «лежит на поверхности»: ось Y — вверх от поверхности) ----

        static void BuildPrintout(Transform m)
        {
            Box(m, PrimitiveType.Cube, new Vector3(0, 0.001f, 0), new Vector3(0.21f, 0.002f, 0.297f), Lit("Paper", new Color(0.95f, 0.95f, 0.92f), 0.1f));
            var ink = new Color(0.12f, 0.12f, 0.15f);
            FlatText(m, "От: support@nova-soft.co", new Vector3(-0.09f, 0.0035f, 0.12f), 0.011f, ink, TextAnchor.MiddleLeft);
            FlatText(m, "СРОЧНО! Смените пароль", new Vector3(-0.09f, 0.0035f, 0.09f), 0.016f, new Color(0.75f, 0.1f, 0.1f), TextAnchor.MiddleLeft);
            var lineMat = Lit("PaperLines", new Color(0.55f, 0.55f, 0.58f), 0.1f);
            for (int i = 0; i < 7; i++)
                Box(m, PrimitiveType.Cube, new Vector3(-0.005f, 0.0025f, 0.055f - i * 0.022f), new Vector3(i == 6 ? 0.1f : 0.17f, 0.0005f, 0.004f), lineMat);
            Box(m, PrimitiveType.Cube, new Vector3(-0.04f, 0.0025f, -0.11f), new Vector3(0.09f, 0.0006f, 0.022f), Lit("LinkButton", new Color(0.15f, 0.4f, 0.95f), 0.2f));
            FlatText(m, "Войти", new Vector3(-0.04f, 0.0035f, -0.11f), 0.011f, Color.white);
        }

        static void BuildSticker(Transform m)
        {
            Box(m, PrimitiveType.Cube, new Vector3(0, 0.0005f, 0), new Vector3(0.076f, 0.001f, 0.076f), Lit("StickyNote", new Color(1f, 0.9f, 0.3f), 0.05f));
            FlatText(m, "пароль:", new Vector3(0, 0.0015f, 0.016f), 0.009f, new Color(0.25f, 0.2f, 0.4f));
            FlatText(m, "Nova2024", new Vector3(0, 0.0015f, -0.006f), 0.014f, new Color(0.1f, 0.1f, 0.5f));
        }

        static void BuildUsb(Transform m)
        {
            Box(m, PrimitiveType.Cube, new Vector3(0, 0.005f, 0), new Vector3(0.022f, 0.01f, 0.055f), Lit("UsbBody", new Color(0.1f, 0.12f, 0.18f), 0.5f));
            Box(m, PrimitiveType.Cube, new Vector3(0, 0.0045f, 0.0365f), new Vector3(0.012f, 0.0045f, 0.018f), Lit("UsbMetal", new Color(0.85f, 0.85f, 0.88f), 0.8f, 1f));
            Box(m, PrimitiveType.Sphere, new Vector3(0, 0.0105f, -0.018f), Vector3.one * 0.004f, Holo("UsbLed", new Color(1f, 0.2f, 0.25f), 0.9f));
            FlatText(m, ".EXE", new Vector3(0, 0.0105f, 0.0f), 0.006f, new Color(1f, 0.35f, 0.35f));
        }

        static void BuildTraffic(Transform m)
        {
            var c = new Color(0.25f, 0.85f, 1f);
            Box(m, PrimitiveType.Sphere, Vector3.zero, Vector3.one * 0.09f, Holo("TrafficCore", c, 0.45f, 60f));
            var packet = Holo("TrafficPacket", Color.Lerp(c, Color.white, 0.3f), 0.85f);
            for (int i = 0; i < 4; i++)
            {
                var orbit = new GameObject("Orbit").transform;
                orbit.SetParent(m, false);
                orbit.localEulerAngles = new Vector3(i * 45f, i * 70f, 0);
                Box(orbit, PrimitiveType.Cube, new Vector3(0.1f, 0, 0), Vector3.one * 0.018f, packet);
                orbit.gameObject.AddComponent<Spinner>().speed = new Vector3(0, 140f + i * 30f, 0);
            }
        }

        static void BuildDatabase(Transform m)
        {
            var c = new Color(0.7f, 0.4f, 1f);
            var a = Holo("DatabaseLayer", c, 0.45f, 60f);
            var b = Holo("DatabaseCore", c, 0.85f);
            for (int i = 0; i < 3; i++)
                Box(m, PrimitiveType.Cylinder, new Vector3(0, 0.02f + i * 0.045f, 0), new Vector3(0.12f, 0.016f, 0.12f), i == 1 ? b : a);
        }

        static void BuildFingerprint(Transform m)
        {
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(TexPath("Fingerprint"));
            Box(m, PrimitiveType.Quad, new Vector3(0, 0.001f, 0), new Vector3(0.11f, 0.13f, 1), FlatMat("FingerprintGlow", new Color(0.3f, 0.9f, 1f, 0.12f)), new Vector3(90, 0, 0));
            Box(m, PrimitiveType.Quad, new Vector3(0, 0.002f, 0), new Vector3(0.07f, 0.0875f, 1), FlatMat("Fingerprint", new Color(0.55f, 1f, 1f, 1f), tex, false, 3001), new Vector3(90, 0, 0));
        }

        static void BuildAccessLog(Transform m)
        {
            Box(m, PrimitiveType.Cube, new Vector3(0, 0.008f, 0), new Vector3(0.11f, 0.016f, 0.17f), Lit("AccessBody", new Color(0.14f, 0.15f, 0.18f), 0.5f, 0.3f));
            Box(m, PrimitiveType.Cube, new Vector3(0, 0.0165f, 0.03f), new Vector3(0.09f, 0.001f, 0.075f), Lit("AccessScreen", new Color(0.02f, 0.08f, 0.05f), 0.9f));
            var g = new Color(0.35f, 1f, 0.55f);
            FlatText(m, "ВХОД / ВЫХОД", new Vector3(-0.04f, 0.018f, 0.058f), 0.008f, g, TextAnchor.MiddleLeft);
            FlatText(m, "18:30  Белова   ↑", new Vector3(-0.04f, 0.018f, 0.038f), 0.007f, g, TextAnchor.MiddleLeft);
            FlatText(m, "03:38  Ковалёв  ↓", new Vector3(-0.04f, 0.018f, 0.022f), 0.007f, g, TextAnchor.MiddleLeft);
            FlatText(m, "04:20  Ковалёв  ↑", new Vector3(-0.04f, 0.018f, 0.006f), 0.007f, g, TextAnchor.MiddleLeft);
            Box(m, PrimitiveType.Cylinder, new Vector3(0, 0.0165f, -0.045f), new Vector3(0.045f, 0.001f, 0.045f), Holo("AccessReader", g, 0.6f));
            Box(m, PrimitiveType.Sphere, new Vector3(0.04f, 0.017f, -0.07f), Vector3.one * 0.006f, Holo("AccessLed", new Color(1f, 0.3f, 0.3f), 0.9f));
        }

        // ================= «Дело» =================

        static void BuildCase(List<CipherClue> clues)
        {
            var existing = AssetDatabase.LoadAssetAtPath<CaseFile>(CasePath);
            if (existing && !force) return;
            var c = existing ? existing : ScriptableObject.CreateInstance<CaseFile>();
            c.gameTitle = "CIPHER";
            c.caseTitle = "Утечка данных в «Нова Софт»";
            c.briefing = "Агент, этой ночью из компании «Нова Софт» утекла база клиентов. Под подозрением трое: " +
                         "бухгалтер Марина Белова, сисадмин Олег Ковалёв и Денис Ершов, разработчик, уволенный месяц назад.\n\n" +
                         "Следы атаки остались прямо в этой комнате. Найдите улики, восстановите ход атаки и назовите виновного. " +
                         "Следите за временем событий: оно есть у каждой улики. Некоторые следы видны только в ультрафиолете.";
            c.clues = clues;
            c.boardTask = "Восстановите ход атаки: выбирайте улики по порядку, от первого шага злоумышленника к последнему.";
            c.solvedText = "Цепочка сложилась: поддельное письмо → украденный пароль → бэкдор с флешки → ночное подключение → выгрузка базы. " +
                           "Осталось понять, кто за этим стоит.";
            c.suspectTask = "Сравните время событий и следы на уликах. Наведите прицел на подозреваемого и коснитесь экрана.";
            c.suspects = new List<Suspect>
            {
                new Suspect
                {
                    name = "Марина Белова", role = "бухгалтер",
                    suspicion = "Её пароль, её компьютер, флешка из её посылки.",
                    explanation = "Марина ушла из офиса в 18:30, а выгрузка началась в 03:15 через удалённый доступ. " +
                                  "Она жертва обмана: письмо и посылку подготовили специально для неё.",
                    color = new Color(1f, 0.82f, 0.25f)
                },
                new Suspect
                {
                    name = "Олег Ковалёв", role = "системный администратор",
                    suspicion = "Его отпечаток у компьютера, ночью он был в офисе.",
                    explanation = "Олег вошёл в 03:38, когда утечка уже шла. В 03:41 выгрузка оборвалась: это он по тревоге выдернул " +
                                  "сетевой кабель и спас 28 000 записей. Отпечаток остался именно тогда.",
                    color = new Color(0.4f, 1f, 1f)
                },
                new Suspect
                {
                    name = "Денис Ершов", role = "бывший разработчик",
                    suspicion = "Уволен месяц назад, знает внутренние системы компании.",
                    explanation = "Два следа ведут к Денису: автор бэкдора на флешке — DErshov-PC, а ночью с того же адреса 203.0.113.66, " +
                                  "куда ушли данные, пытались войти под его старой учётной записью. Он подготовил письмо и посылку, " +
                                  "а ночью управлял бэкдором из дома.",
                    isCulprit = true,
                    color = new Color(1f, 0.3f, 0.35f)
                },
            };
            c.futureTitle = "2030 · СИСТЕМА ЗАЩИЩЕНА";
            c.finalText = "Денис Ершов задержан. Благодаря Олегу 28 000 записей остались в безопасности. Марина прошла обучение и теперь " +
                          "первой замечает поддельные письма. В «Нова Софт» включили двухфакторную защиту, оповещения о ночном трафике " +
                          "и отключают доступ уволенным в тот же день.";
            if (!existing) AssetDatabase.CreateAsset(c, CasePath);
            EditorUtility.SetDirty(c);
        }

        public static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }
    }
}
