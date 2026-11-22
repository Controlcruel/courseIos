using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

namespace Cipher
{
    /// <summary>
    /// Находит в комнате столы, стены и пол (по плоскостям, которые видит камера iPhone)
    /// и раскладывает по ним улики. Стол = горизонтальная поверхность на высоте 0,35–1,4 м над полом.
    /// </summary>
    public class SurfaceFinder : MonoBehaviour
    {
        public ARPlaneManager planeManager;

        [Header("Минимальная площадь, м²")]
        public float minTableArea = 0.1f;
        public float minWallArea = 0.4f;
        public float minFloorArea = 0.6f;
        [Tooltip("Минимальное расстояние между уликами, м")]
        public float minSpacing = 0.35f;

        /// <summary>Показывать ли найденные плоскости (подсветка при сканировании комнаты).</summary>
        public bool ShowPlanes { get; set; }
        public float FloorY { get; private set; }
        public int TableCount => tables.Count;
        public int WallCount => walls.Count;
        public int FloorCount => floors.Count;
        public int PlaneCount { get; private set; }

        readonly List<ARPlane> tables = new List<ARPlane>(), walls = new List<ARPlane>(), floors = new List<ARPlane>(), all = new List<ARPlane>();
        readonly List<Vector3> used = new List<Vector3>();
        readonly List<Vector2> poly = new List<Vector2>();

        void Awake()
        {
            if (!planeManager) planeManager = FindFirstObjectByType<ARPlaneManager>();
        }

        void Update() => Classify();

        public void Classify()
        {
            tables.Clear(); walls.Clear(); floors.Clear(); all.Clear();
            PlaneCount = 0;
            var cam = Camera.main;
            float lowest = float.MaxValue;
            if (!planeManager) { FloorY = cam ? cam.transform.position.y - 1.4f : 0f; return; }

            var horizontal = new List<ARPlane>();
            foreach (var p in planeManager.trackables)
            {
                foreach (var r in p.GetComponentsInChildren<Renderer>()) r.enabled = ShowPlanes;
                if (p.subsumedBy != null || p.trackingState == TrackingState.None) continue;
                PlaneCount++;
                all.Add(p);
                float area = p.size.x * p.size.y;
                if (p.alignment == PlaneAlignment.HorizontalUp)
                {
                    horizontal.Add(p);
                    if (area >= minFloorArea * 0.5f) lowest = Mathf.Min(lowest, p.center.y);
                }
                else if (p.alignment == PlaneAlignment.Vertical && area >= minWallArea) walls.Add(p);
            }

            FloorY = lowest < float.MaxValue ? lowest : (cam ? cam.transform.position.y - 1.4f : 0f);
            foreach (var p in horizontal)
            {
                float h = p.center.y - FloorY, area = p.size.x * p.size.y;
                if (h < 0.2f && area >= minFloorArea) floors.Add(p);
                else if (h >= 0.35f && h <= 1.4f && area >= minTableArea) tables.Add(p);
            }
        }

        public void ResetPlacement() => used.Clear();

        /// <summary>Подбирает место для улики. Возвращает плоскость (или null, если положили «в воздух»).</summary>
        public ARPlane Place(CipherClue clue, Transform head)
        {
            Classify();
            var order = new List<List<ARPlane>>();
            switch (clue.surface)
            {
                case SurfaceType.Table: order.Add(tables); order.Add(floors); order.Add(walls); break;
                case SurfaceType.Wall: order.Add(walls); order.Add(tables); order.Add(floors); break;
                case SurfaceType.Floor: order.Add(floors); order.Add(tables); order.Add(walls); break;
                default: order.Add(all); break;
            }

            foreach (var list in order)
            {
                var shuffled = new List<ARPlane>(list);
                for (int i = shuffled.Count - 1; i > 0; i--) { int j = Random.Range(0, i + 1); (shuffled[i], shuffled[j]) = (shuffled[j], shuffled[i]); }
                foreach (var plane in shuffled)
                    if (TryPointOnPlane(plane, out var point))
                    {
                        ApplyPose(clue, plane.transform.up, point, plane.alignment == PlaneAlignment.Vertical, head);
                        used.Add(point);
                        return plane;
                    }
            }

            // поверхностей нет — кладём рядом с игроком
            Vector3 fwd = head.forward; fwd.y = 0;
            if (fwd.sqrMagnitude < 0.001f) fwd = Vector3.forward;
            Vector3 dir = Quaternion.Euler(0, Random.Range(-100f, 100f), 0) * fwd.normalized;
            float y = clue.surface == SurfaceType.Floor ? FloorY : clue.surface == SurfaceType.Table ? FloorY + 0.75f : head.position.y - 0.2f;
            Vector3 pos = new Vector3(head.position.x, y, head.position.z) + dir * Random.Range(1f, 2f);
            ApplyPose(clue, Vector3.up, pos, false, head);
            used.Add(pos);
            return null;
        }

        void ApplyPose(CipherClue clue, Vector3 normal, Vector3 point, bool vertical, Transform head)
        {
            Vector3 forward;
            if (vertical) forward = Vector3.ProjectOnPlane(Vector3.up, normal);                 // на стене — «верх» улики смотрит вверх
            else if (clue.randomYaw) forward = Quaternion.AngleAxis(Random.Range(0f, 360f), normal) * Vector3.ProjectOnPlane(Vector3.forward, normal);
            else forward = Vector3.ProjectOnPlane(head.position - point, normal);
            if (forward.sqrMagnitude < 0.0001f) forward = Vector3.ProjectOnPlane(Vector3.forward, normal);
            clue.transform.SetPositionAndRotation(point + normal * clue.surfaceOffset, Quaternion.LookRotation(forward.normalized, normal));
        }

        bool TryPointOnPlane(ARPlane plane, out Vector3 world)
        {
            world = default;
            NativeArray<Vector2> boundary = plane.boundary;
            if (!boundary.IsCreated || boundary.Length < 3) return false;
            poly.Clear();
            Vector2 min = new Vector2(float.MaxValue, float.MaxValue), max = -min, centroid = Vector2.zero;
            foreach (var v in boundary) { poly.Add(v); min = Vector2.Min(min, v); max = Vector2.Max(max, v); centroid += v; }
            centroid /= poly.Count;

            for (int attempt = 0; attempt < 30; attempt++)
            {
                var p = new Vector2(Random.Range(min.x, max.x), Random.Range(min.y, max.y));
                if (!Inside(p)) continue;
                p = Vector2.Lerp(p, centroid, 0.25f); // подальше от края
                Vector3 w = plane.transform.TransformPoint(new Vector3(p.x, 0, p.y));
                bool free = true;
                foreach (var u in used) if (Vector3.Distance(u, w) < minSpacing) { free = false; break; }
                if (!free) continue;
                world = w;
                return true;
            }
            return false;
        }

        bool Inside(Vector2 p)
        {
            bool inside = false;
            for (int i = 0, j = poly.Count - 1; i < poly.Count; j = i++)
                if ((poly[i].y > p.y) != (poly[j].y > p.y) &&
                    p.x < (poly[j].x - poly[i].x) * (p.y - poly[i].y) / (poly[j].y - poly[i].y) + poly[i].x)
                    inside = !inside;
            return inside;
        }

        /// <summary>Ищет стену перед игроком (для портала).</summary>
        public bool TryWallInFront(Transform head, out Pose pose)
        {
            Classify();
            pose = default;
            Vector3 fwd = head.forward; fwd.y = 0;
            if (fwd.sqrMagnitude < 0.001f) return false;
            fwd.Normalize();
            float best = float.MaxValue;
            foreach (var w in walls)
            {
                Vector3 n = w.transform.up;
                var pl = new Plane(n, w.transform.position);
                var ray = new Ray(head.position, fwd);
                if (!pl.Raycast(ray, out float d) || d < 0.8f || d > 4.5f || d >= best) continue;
                Vector3 hit = ray.GetPoint(d);
                Vector3 local = w.transform.InverseTransformPoint(hit);
                if (Mathf.Abs(local.x) > w.size.x * 0.5f + 0.3f || Mathf.Abs(local.z) > w.size.y * 0.5f + 0.3f) continue;
                if (Vector3.Dot(n, head.position - hit) < 0) n = -n;
                best = d;
                pose = new Pose(hit + n * 0.03f, Quaternion.LookRotation(-n, Vector3.up));
            }
            return best < float.MaxValue;
        }
    }
}
