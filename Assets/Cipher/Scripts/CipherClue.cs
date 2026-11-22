using System.Collections;
using UnityEngine;

namespace Cipher
{
    public enum SurfaceType
    {
        [InspectorName("Стол")] Table,
        [InspectorName("Стена")] Wall,
        [InspectorName("Пол")] Floor,
        [InspectorName("Любая поверхность")] Any
    }

    /// <summary>
    /// Улика. Вешается на корень префаба. Внешний вид — дочерний объект «Model»
    /// (кубики или скачанная 3D-модель). Всё настраивается в Inspector.
    /// Модель делается так, будто она лежит на столе: ось Y модели — «вверх от поверхности».
    /// На стене та же ось смотрит из стены.
    /// </summary>
    [DisallowMultipleComponent]
    public class CipherClue : MonoBehaviour
    {
        /// <summary>Включён ли УФ-фильтр (переключает игра).</summary>
        public static bool UVMode;

        [Header("Что это за улика")]
        public string title = "Новая улика";
        [Tooltip("Время события для хронологии дела, например 18:02. Пусто — время неизвестно")]
        public string time;
        [TextArea(3, 10), Tooltip("Сведения, которые игрок узнаёт после изучения")]
        public string description;
        [TextArea(2, 6), Tooltip("Как защититься: попадёт в финал «будущего»")]
        public string lesson;
        public Color color = new Color(0.3f, 0.95f, 1f);

        [Header("Где лежит")]
        public SurfaceType surface = SurfaceType.Table;
        [Tooltip("Насколько приподнять над поверхностью, м")]
        public float surfaceOffset = 0.015f;
        [Tooltip("Случайно повернуть улику вокруг оси поверхности")]
        public bool randomYaw = true;

        [Header("Как найти")]
        [Tooltip("Видна только через УФ-фильтр")]
        public bool onlyVisibleInUV;
        [Tooltip("С какого расстояния можно изучить, м")]
        public float scanDistance = 1.5f;
        [Tooltip("Сколько секунд держать палец на экране")]
        public float scanTime = 1.2f;

        [Header("Доска расследования")]
        [Tooltip("Это шаг атаки? Тогда улика появится на доске")]
        public bool isAttackStep = true;
        [Tooltip("Номер шага в правильном порядке: 1, 2, 3…")]
        public int attackStep = 1;

        [Header("Внешний вид")]
        [Tooltip("Дочерний объект с моделью улики")]
        public Transform model;

        public bool Collected { get; private set; }
        /// <summary>Исходный масштаб модели (из префаба).</summary>
        public Vector3 ModelBaseScale => modelScale;
        public bool VisibleNow { get; private set; } = true;

        Vector3 modelScale = Vector3.one;
        Transform halo;
        Material haloMat;
        float hoverK, visibleK;
        bool hover;

        void Awake()
        {
            if (!model && transform.childCount > 0) model = transform.GetChild(0);
            if (model) modelScale = model.localScale;
            EnsureCollider();

            // едва заметное кольцо-подсказка под уликой
            haloMat = CipherVisuals.Flat(new Color(color.r, color.g, color.b, 0f));
            halo = CipherVisuals.Prim(PrimitiveType.Cylinder, transform, new Vector3(0, 0.002f, 0), new Vector3(0.22f, 0.0008f, 0.22f), haloMat).transform;
            halo.name = "Halo";
            visibleK = onlyVisibleInUV ? 0 : 1;
        }

        void EnsureCollider()
        {
            if (GetComponentInChildren<Collider>()) return;
            var rends = GetComponentsInChildren<Renderer>();
            if (rends.Length == 0) { gameObject.AddComponent<SphereCollider>().radius = 0.1f; return; }
            var b = new Bounds(transform.InverseTransformPoint(rends[0].bounds.center), Vector3.zero);
            foreach (var r in rends)
            {
                var wb = r.bounds;
                for (int i = 0; i < 8; i++)
                {
                    var corner = wb.center + Vector3.Scale(wb.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                    b.Encapsulate(transform.InverseTransformPoint(corner));
                }
            }
            var box = gameObject.AddComponent<BoxCollider>();
            box.center = b.center;
            box.size = b.size + Vector3.one * 0.06f; // небольшой запас, чтобы легче попадать прицелом
        }

        public void SetHover(bool h) => hover = h;

        public void Collect(Transform head)
        {
            if (Collected) return;
            Collected = true;
            var stick = GetComponent<StickToPlane>();
            if (stick) Destroy(stick);
            foreach (var c in GetComponentsInChildren<Collider>()) c.enabled = false;
            StartCoroutine(FlyAway(head));
        }

        IEnumerator FlyAway(Transform head)
        {
            Vector3 start = transform.position, startScale = transform.localScale;
            for (float t = 0; t < 1f; t += Time.deltaTime / 0.6f)
            {
                Vector3 target = head.position - head.up * 0.25f + head.forward * 0.2f;
                float e = t * t;
                transform.position = Vector3.Lerp(start, target, e);
                transform.localScale = startScale * (1 - e);
                yield return null;
            }
            gameObject.SetActive(false);
        }

        void Update()
        {
            if (Collected) return;
            var cam = Camera.main;
            float dist = cam ? Vector3.Distance(cam.transform.position, transform.position) : 99f;

            // видимость: УФ-улики проявляются только в УФ-свете и недалеко
            bool shouldShow = !onlyVisibleInUV || (UVMode && dist < 3.5f);
            visibleK = Mathf.MoveTowards(visibleK, shouldShow ? 1 : 0, Time.deltaTime * 3f);
            VisibleNow = visibleK > 0.5f;
            if (model)
            {
                model.gameObject.SetActive(visibleK > 0.01f);
                hoverK = Mathf.MoveTowards(hoverK, hover ? 1 : 0, Time.deltaTime * 6f);
                float pulse = onlyVisibleInUV ? 1f + Mathf.Sin(Time.time * 5f) * 0.04f * visibleK : 1f;
                model.localScale = modelScale * visibleK * pulse * (1f + hoverK * 0.08f);
            }

            // кольцо-подсказка: заметно только вблизи и при наведении
            float near = Mathf.InverseLerp(2.5f, 0.8f, dist);
            float a = (0.05f + 0.1f * Mathf.Sin(Time.time * 2.5f) * 0.5f + 0.5f * hoverK) * near * visibleK;
            haloMat.SetColor("_Color", new Color(color.r, color.g, color.b, Mathf.Clamp01(a)));
        }
    }
}
