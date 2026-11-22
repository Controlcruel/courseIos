using System.Collections.Generic;
using UnityEngine;

namespace Cipher
{
    /// <summary>
    /// Портал в будущее. Невидимый диск-маска помечает пиксели стенсилом,
    /// и «мир будущего» видно только сквозь него. Если игрок шагнёт в портал,
    /// будущее окружает его со всех сторон.
    /// </summary>
    public class Portal : MonoBehaviour
    {
        public const float Radius = 0.8f;
        public bool PlayerInside { get; private set; }

        Transform ring1, ring2, disc, inside;
        readonly List<Material> insideMats = new List<Material>();
        float open;
        Transform head;

        /// <summary>Урок, который показывается на табличке внутри «будущего».</summary>
        public struct Lesson { public string Title, Text; public Color Color; }

        IList<Lesson> lessons;
        string futureTitle;

        public static Portal Create(Transform parent, Vector3 pos, Quaternion rot, Transform head, IList<Lesson> lessons, string futureTitle)
        {
            var go = new GameObject("Portal");
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(pos, rot);
            var p = go.AddComponent<Portal>();
            p.head = head;
            p.lessons = lessons;
            p.futureTitle = futureTitle;
            p.Build();
            return p;
        }

        void Build()
        {
            var t = transform;
            var col = gameObject.AddComponent<SphereCollider>();
            col.radius = Radius;

            // маска
            disc = CipherVisuals.Prim(PrimitiveType.Cylinder, t, Vector3.zero, new Vector3(Radius * 2, 0.0005f, Radius * 2), CipherVisuals.PortalMask()).transform;
            disc.localRotation = Quaternion.Euler(90, 0, 0);

            // светящиеся кольца
            ring1 = new GameObject("Ring1").transform; ring1.SetParent(t, false);
            ring2 = new GameObject("Ring2").transform; ring2.SetParent(t, false);
            var cyan = CipherVisuals.Hologram(new Color(0.3f, 0.95f, 1f), 0.8f, 1.5f, 1.4f);
            var violet = CipherVisuals.Hologram(new Color(0.75f, 0.4f, 1f), 0.7f, 1.5f, 1.3f);
            for (int i = 0; i < 40; i++)
            {
                float a = i / 40f * Mathf.PI * 2;
                var seg = CipherVisuals.Prim(PrimitiveType.Cube, ring1, new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0) * (Radius + 0.03f),
                    new Vector3(0.025f, 0.11f, 0.04f), i % 2 == 0 ? cyan : violet);
                seg.transform.localRotation = Quaternion.Euler(0, 0, a * Mathf.Rad2Deg);
            }
            for (int i = 0; i < 18; i++)
            {
                float a = i / 18f * Mathf.PI * 2;
                CipherVisuals.Prim(PrimitiveType.Sphere, ring2, new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0) * (Radius + 0.12f),
                    Vector3.one * 0.03f, cyan);
            }
            CipherVisuals.Text(t, "ПОРТАЛ В БУДУЩЕЕ", new Vector3(0, Radius + 0.25f, 0), 0.05f, new Color(0.5f, 1f, 1f), TextAnchor.MiddleCenter, false, FontStyle.Bold)
                .transform.localRotation = Quaternion.identity;

            BuildFuture();
            transform.localScale = Vector3.one * 0.001f;
        }

        Material Track(Material m) { insideMats.Add(m); return m; }

        void BuildFuture()
        {
            inside = new GameObject("Future").transform;
            inside.SetParent(transform, false);
            inside.localPosition = new Vector3(0, 0, 3f);
            var f = inside;

            // «небо»: сфера, видимая изнутри
            var skyMat = Track(CipherVisuals.Flat(new Color(0.02f, 0.06f, 0.12f, 1f), true, 2500));
            skyMat.SetFloat("_Cull", 1f);
            CipherVisuals.Prim(PrimitiveType.Sphere, f, Vector3.zero, Vector3.one * 16f, skyMat);

            // звёзды
            var starMat = Track(CipherVisuals.Hologram(Color.white, 1f, 1f, 1.5f, 0f, true));
            var rnd = new System.Random(7);
            for (int i = 0; i < 160; i++)
            {
                var d = new Vector3((float)rnd.NextDouble() * 2 - 1, (float)rnd.NextDouble() * 1.2f - 0.2f, (float)rnd.NextDouble() * 2 - 1).normalized;
                CipherVisuals.Prim(PrimitiveType.Cube, f, d * 7.5f, Vector3.one * (0.02f + (float)rnd.NextDouble() * 0.04f), starMat);
            }

            // сетка-пол
            var gridMat = Track(CipherVisuals.Flat(new Color(0.2f, 1f, 0.6f, 0.35f), true));
            float floorY = -1.5f;
            for (int i = -10; i <= 10; i++)
            {
                CipherVisuals.Prim(PrimitiveType.Cube, f, new Vector3(i * 0.6f, floorY, 0), new Vector3(0.01f, 0.003f, 12f), gridMat);
                CipherVisuals.Prim(PrimitiveType.Cube, f, new Vector3(0, floorY, i * 0.6f), new Vector3(12f, 0.003f, 0.01f), gridMat);
            }

            // щит в центре
            var green = new Color(0.25f, 1f, 0.55f);
            var shield = new GameObject("Shield").transform; shield.SetParent(f, false); shield.localPosition = new Vector3(0, 0.1f, 0.5f);
            CipherVisuals.Prim(PrimitiveType.Sphere, shield, Vector3.zero, new Vector3(0.9f, 1.1f, 0.15f), Track(CipherVisuals.Hologram(green, 0.35f, 2f, 1f, 50f, true)));
            CipherVisuals.Prim(PrimitiveType.Sphere, shield, new Vector3(0, 0, -0.02f), new Vector3(0.55f, 0.7f, 0.1f), Track(CipherVisuals.Hologram(green, 0.7f, 1.5f, 1.3f, 0f, true)));
            shield.gameObject.AddComponent<Spinner>().speed = new Vector3(0, 20, 0);
            var title = CipherVisuals.Text(f, futureTitle, new Vector3(0, 1.05f, 0.5f), 0.16f, green, TextAnchor.MiddleCenter, true, FontStyle.Bold);
            Track(title.GetComponent<Renderer>().sharedMaterial);

            // защищённые узлы сети вокруг щита
            var nodeMat = Track(CipherVisuals.Hologram(green, 0.6f, 1.5f, 1.2f, 0f, true));
            for (int i = 0; i < 6; i++)
            {
                float a = i / 6f * Mathf.PI * 2;
                var p = new Vector3(Mathf.Cos(a) * 1.4f, -0.4f + Mathf.Sin(a * 2) * 0.2f, 0.5f + Mathf.Sin(a) * 1.4f);
                CipherVisuals.Prim(PrimitiveType.Cube, f, p, Vector3.one * 0.14f, nodeMat).AddComponent<Spinner>().speed = new Vector3(20, 45, 0);
                var lr = CipherVisuals.Line(f, new Color(0.3f, 1f, 0.6f, 0.6f), 0.008f, true);
                Track(lr.sharedMaterial);
                lr.useWorldSpace = false;
                lr.positionCount = 2;
                lr.SetPosition(0, p);
                lr.SetPosition(1, new Vector3(0, 0.1f, 0.5f));
            }

            // таблички с уроками
            var clues = lessons;
            for (int i = 0; i < clues.Count; i++)
            {
                float a = Mathf.Lerp(-65f, 65f, i / (float)Mathf.Max(1, clues.Count - 1)) * Mathf.Deg2Rad;
                var plaque = new GameObject("Lesson_" + i).transform;
                plaque.SetParent(f, false);
                plaque.localPosition = new Vector3(Mathf.Sin(a) * 2.1f, -0.35f + (i % 2) * 0.1f, 0.3f + Mathf.Cos(a) * 2.1f);
                plaque.localRotation = Quaternion.LookRotation(plaque.localPosition - new Vector3(0, plaque.localPosition.y, -3f));
                CipherVisuals.Prim(PrimitiveType.Quad, plaque, Vector3.zero, new Vector3(1.0f, 0.42f, 1),
                    Track(CipherVisuals.Flat(new Color(0.02f, 0.12f, 0.08f, 0.85f), true)));
                var h = CipherVisuals.Text(plaque, (i + 1) + ". " + clues[i].Title, new Vector3(-0.46f, 0.17f, -0.005f), 0.045f, clues[i].Color, TextAnchor.UpperLeft, true, FontStyle.Bold);
                Track(h.GetComponent<Renderer>().sharedMaterial);
                var body = CipherVisuals.Text(plaque, CipherVisuals.Wrap(clues[i].Text, CipherVisuals.CharsFor(0.92f, 0.03f)),
                    new Vector3(-0.46f, 0.08f, -0.005f), 0.03f, Color.white, TextAnchor.UpperLeft, true);
                Track(body.GetComponent<Renderer>().sharedMaterial);
            }
        }

        void Update()
        {
            open = Mathf.MoveTowards(open, 1f, Time.deltaTime * 0.8f);
            float e = 1 - Mathf.Pow(1 - open, 3);
            transform.localScale = Vector3.one * Mathf.Max(0.001f, e);

            ring1.Rotate(0, 0, 25 * Time.deltaTime, Space.Self);
            ring2.Rotate(0, 0, -40 * Time.deltaTime, Space.Self);
            float pulse = 1f + Mathf.Sin(Time.time * 3f) * 0.02f;
            ring2.localScale = Vector3.one * pulse;

            // игрок шагнул сквозь портал → будущее вокруг него
            if (head)
            {
                float z = transform.InverseTransformPoint(head.position).z;
                Vector2 xy = transform.InverseTransformPoint(head.position);
                bool inside = z > 0.05f || (PlayerInside && z > -0.05f);
                if (!PlayerInside && z > 0.05f && xy.magnitude > Radius * 1.5f) inside = false; // обошёл сбоку — не считается
                if (inside != PlayerInside)
                {
                    PlayerInside = inside;
                    foreach (var m in insideMats) m.SetFloat("_StencilComp", inside ? CipherVisuals.StencilAlways : CipherVisuals.StencilEqual);
                    CipherAudio.Portal();
                }
            }
        }
    }
}
