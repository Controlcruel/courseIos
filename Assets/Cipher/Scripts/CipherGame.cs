using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

namespace Cipher
{
    /// <summary>
    /// Главный скрипт игры «Cipher» (мобильная AR-версия для iPhone).
    /// Этапы: брифинг → сканирование комнаты → поиск и изучение улик → доска расследования →
    /// «Кто это сделал?» → портал → финал.
    /// Сюжет и улики берутся из файла «Дело» (CaseFile), интерфейс — из Canvas «Cipher UI».
    /// </summary>
    [RequireComponent(typeof(ARInput))]
    public class CipherGame : MonoBehaviour
    {
        public enum Stage { Intro, Scanning, Search, Inspect, Board, Suspects, Portal, Final }

        [Header("Ссылки")]
        public CaseFile caseFile;
        public CipherUI ui;
        public SurfaceFinder surfaces;
        [Tooltip("Окклюзия (лидар): улики прячутся за реальными предметами")]
        public AROcclusionManager occlusion;

        [Header("Сканирование комнаты")]
        [Tooltip("Минимум секунд сканирования, прежде чем можно продолжить")]
        public float minScanSeconds = 4f;
        [Tooltip("Через сколько секунд продолжить можно в любом случае")]
        public float maxScanSeconds = 15f;

        [Header("Доска и портал")]
        [Tooltip("Расстояние до доски расследования, м")]
        public float boardDistance = 2.0f;
        [Tooltip("Расстояние до портала, если перед игроком нет стены, м")]
        public float portalDistance = 2.2f;

        public Stage State { get; private set; }

        Transform head;
        ARInput input;
        Transform world;
        readonly List<CipherClue> spawned = new List<CipherClue>();
        readonly List<CipherClue> collected = new List<CipherClue>();
        CipherClue target;
        float scanProgress, scanStart, startTime;
        bool uv;
        EvidenceBoard board;
        SuspectBoard suspectBoard;
        Portal portal;
        int mistakes;
        float lastFoundTime;

        [Header("Подсказки")]
        [Tooltip("Через сколько секунд без находок показать, где ближайшая улика")]
        public float hintAfterSeconds = 40f;

        static readonly Color Cyan = new Color(0.3f, 0.95f, 1f);
        static readonly Color Violet = new Color(0.7f, 0.4f, 1f);
        static readonly Color Green = new Color(0.35f, 1f, 0.55f);

        void Start()
        {
            head = Camera.main ? Camera.main.transform : transform;
            input = GetComponent<ARInput>();
            if (!surfaces) surfaces = GetComponent<SurfaceFinder>();
            CipherAudio.Init(head);
            CipherClue.UVMode = false;
            ui.UVPressed += ToggleUV;
            ui.CasePressed += OpenCase;
            ui.ShowCrosshair(false);
            ShowIntro();
        }

        void ShowIntro()
        {
            State = Stage.Intro;
            ui.ShowMessage(caseFile.gameTitle + " · AR-расследование", caseFile.caseTitle, caseFile.briefing, null, "Начать расследование", Cyan, StartScanning);
        }

        // ---------- 1. Сканирование комнаты ----------

        void StartScanning()
        {
            State = Stage.Scanning;
            scanStart = Time.time;
            surfaces.ShowPlanes = true;
            CipherAudio.Scan();
        }

        void UpdateScanning()
        {
            float t = Time.time - scanStart;
            bool ready = (t >= minScanSeconds && surfaces.PlaneCount > 0) || t >= maxScanSeconds;
            ui.ShowScan(surfaces.TableCount, surfaces.WallCount, surfaces.FloorCount, ready, PlaceClues);
        }

        void PlaceClues()
        {
            ui.HideBottom();
            surfaces.ShowPlanes = false;
            surfaces.ResetPlacement();
            world = new GameObject("Investigation").transform;
            spawned.Clear(); collected.Clear();
            foreach (var prefab in caseFile.clues)
            {
                if (!prefab) continue;
                var clue = Instantiate(prefab, world);
                clue.name = prefab.name;
                var plane = surfaces.Place(clue, head);
                if (plane) clue.gameObject.AddComponent<StickToPlane>().Init(plane);
                spawned.Add(clue);
            }
            startTime = Time.time;
            lastFoundTime = Time.time;
            ui.ShowHud(true);
            ui.ShowCrosshair(true);
            ui.SetCounter(0, spawned.Count);
            State = Stage.Search;
            CipherAudio.Success();
            ui.ShowMessage("Улики размещены", "Осмотрите комнату",
                "Наведите прицел на подозрительный предмет, подойдите ближе и удерживайте палец на экране, чтобы изучить его.\n\n" +
                "Некоторые следы не видны глазом — включите УФ-фильтр.",
                null, "Понятно", Cyan, null);
        }

        // ---------- 2. Поиск улик ----------

        void UpdateSearch()
        {
            if (ui.BlockingPanelOpen) { ui.SetProgress(-1); return; }

#if UNITY_EDITOR
            if (ARInput.EditorKey('u')) ToggleUV();
            if (ARInput.EditorKey('c')) { var next = spawned.FirstOrDefault(c => !c.Collected); if (next) { Inspect(next); return; } }
#endif
            CipherClue hit = null;
            float dist = 0;
            if (Physics.Raycast(input.Ray, out var h, 8f))
            {
                var c = h.collider.GetComponentInParent<CipherClue>();
                if (c && !c.Collected && c.VisibleNow) { hit = c; dist = h.distance; }
            }

            if (hit != target)
            {
                if (target) target.SetHover(false);
                target = hit;
                if (target) { target.SetHover(true); CipherAudio.Hover(); }
                scanProgress = 0;
            }

            if (!target)
            {
                ui.SetCrosshair(0);
                ui.SetProgress(-1);
                ui.SetHint(IdleHint() ?? (uv ? "УФ-фильтр включён: ищите светящиеся следы" : ""));
            }
            else if (dist > target.scanDistance)
            {
                ui.SetCrosshair(2);
                ui.SetProgress(-1);
                ui.SetHint("Подойдите ближе");
            }
            else
            {
                ui.SetCrosshair(1);
                ui.SetHint("Удерживайте палец на экране, чтобы изучить");
                if (input.Held)
                {
                    scanProgress += Time.deltaTime / Mathf.Max(0.1f, target.scanTime);
                    if (scanProgress >= 1f) { Inspect(target); return; }
                }
                else scanProgress = 0;
                ui.SetProgress(scanProgress);
            }
        }

        /// <summary>Если игрок долго ничего не находит — подсказать направление к ближайшей улике.</summary>
        string IdleHint()
        {
            if (Time.time - lastFoundTime < hintAfterSeconds) return null;
            var left = spawned.Where(c => c && !c.Collected).ToList();
            if (left.Count == 0) return null;
            if (!uv && left.All(c => c.onlyVisibleInUV)) return "Подсказка: оставшиеся следы видны только в УФ-фильтре";
            var pool = uv ? left : left.Where(c => !c.onlyVisibleInUV).ToList();
            var near = pool.OrderBy(c => Vector3.Distance(head.position, c.transform.position)).First();
            Vector3 to = near.transform.position - head.position;
            float dist = to.magnitude;
            Vector3 flat = Flat(to);
            float angle = Vector3.SignedAngle(Flat(head.forward), flat, Vector3.up);
            string dir = Mathf.Abs(angle) < 30f ? "впереди" : Mathf.Abs(angle) > 135f ? "позади вас" : angle > 0 ? "справа" : "слева";
            string height = to.y < -0.9f ? ", внизу" : to.y > 0.6f ? ", выше" : "";
            return $"Подсказка: улика {dir}{height}, примерно {Mathf.Max(1, Mathf.RoundToInt(dist))} м";
        }

        void Inspect(CipherClue c)
        {
            State = Stage.Inspect;
            ui.SetProgress(-1);
            ui.SetHint("");
            CipherAudio.Scan();
            string when = string.IsNullOrEmpty(c.time) ? "" : " · " + c.time;
            ui.ShowMessage($"Улика {collected.Count + 1} из {spawned.Count}{when}", c.title, c.description, c.lesson, "Добавить в дело", c.color, () => Collect(c));
        }

        void Collect(CipherClue c)
        {
            ui.HideMessage();
            if (target == c) target = null;
            c.Collect(head);
            collected.Add(c);
            lastFoundTime = Time.time;
            ui.SetCounter(collected.Count, spawned.Count);
            CipherAudio.Collect();
            if (collected.Count >= spawned.Count) StartCoroutine(OpenBoard());
            else State = Stage.Search;
        }

        void ToggleUV()
        {
            uv = !uv;
            CipherClue.UVMode = uv;
            ui.SetUV(uv);
            CipherAudio.Select();
        }

        void OpenCase()
        {
            // хронология: улики по времени событий, ночные часы (00:00–11:59) идут после вечерних
            ui.ShowCase(collected.OrderBy(c => TimeKey(c.time)).Select(c => new CaseEntry
            {
                Title = (string.IsNullOrEmpty(c.time) ? "" : c.time + " · ") + c.title,
                Body = c.description,
                Color = c.color
            }).Concat(Dossier()).ToList());
        }

        /// <summary>Досье подозреваемых — в конце списка дела, чтобы игрок сверял улики с людьми.</summary>
        IEnumerable<CaseEntry> Dossier()
        {
            if (caseFile.suspects == null) yield break;
            foreach (var s in caseFile.suspects)
                yield return new CaseEntry { Title = "Подозреваемый · " + s.name, Body = s.role + ". " + s.suspicion, Color = s.color };
        }

        /// <summary>Ключ сортировки времени «ЧЧ:ММ» для одной ночи: с 12:00 до 11:59 следующего дня.</summary>
        static int TimeKey(string t)
        {
            if (string.IsNullOrEmpty(t) || t.Length < 4 || !t.Contains(":")) return 100000;
            var parts = t.Split(':');
            if (!int.TryParse(parts[0], out int h) || !int.TryParse(parts[1], out int m)) return 100000;
            int minutes = h * 60 + m;
            return minutes < 12 * 60 ? minutes + 24 * 60 : minutes;
        }

        // ---------- 3. Доска расследования ----------

        IEnumerator OpenBoard()
        {
            State = Stage.Board;
            if (uv) ToggleUV();
            // доска и портал — «интерфейс», их не должны закрывать реальные предметы
            SetOcclusion(false);
            ui.SetHint("");
            yield return new WaitForSeconds(0.8f);

            var steps = collected.Where(c => c.isAttackStep).OrderBy(c => c.attackStep).ToList();
            if (steps.Count < 2) { StartCoroutine(OpenSuspects()); yield break; }

            Vector3 fwd = Flat(head.forward);
            Vector3 pos = head.position + fwd * boardDistance + Vector3.down * 0.3f;
            board = EvidenceBoard.Create(world, pos, Quaternion.LookRotation(fwd), steps, caseFile.boardTask, caseFile.solvedText);
            board.OnSolved += () => StartCoroutine(OpenSuspects());
            CipherAudio.Success();
            ui.ShowMessage("Все улики собраны", "Доска расследования",
                "Восстановите ход атаки: наводите прицел на карточки и касайтесь экрана, от первого шага злоумышленника к последнему.\n\nПодсказка: у каждой улики есть время события, загляните в дело. Коснитесь последней выбранной карточки ещё раз, чтобы отменить выбор.",
                null, "К доске", new Color(1f, 0.36f, 0.42f), null);
        }

        void UpdateBoard()
        {
            if (!board || ui.BlockingPanelOpen) return;
#if UNITY_EDITOR
            if (ARInput.EditorKey('n')) board.DebugChooseNext();
#endif
            board.HandlePointer(input);
            ui.SetCrosshair(0);
            ui.SetHint("Выбирайте улики по порядку атаки");
        }

        // ---------- 4. Кто это сделал? ----------

        IEnumerator OpenSuspects()
        {
            if (board) { mistakes += board.Mistakes; Destroy(board.gameObject); }
            var suspects = caseFile.suspects;
            if (suspects == null || suspects.Count < 2) { StartCoroutine(OpenPortal()); yield break; }

            State = Stage.Suspects;
            ui.SetHint("");
            yield return new WaitForSeconds(0.4f);

            Vector3 fwd = Flat(head.forward);
            Vector3 pos = head.position + fwd * boardDistance + Vector3.down * 0.3f;
            suspectBoard = SuspectBoard.Create(world, pos, Quaternion.LookRotation(fwd), suspects, caseFile.suspectTask);
            suspectBoard.OnSolved += ShowCulprit;
            CipherAudio.Success();
            ui.ShowMessage("Ход атаки восстановлен", "Кто это сделал?",
                caseFile.solvedText + "\n\nСопоставьте время событий из дела (кнопка со списком улик) и выберите виновного: наведите прицел на карточку подозреваемого и коснитесь экрана.",
                null, "К подозреваемым", new Color(1f, 0.78f, 0.3f), null);
        }

        void UpdateSuspects()
        {
            if (!suspectBoard || ui.BlockingPanelOpen) return;
#if UNITY_EDITOR
            if (ARInput.EditorKey('n')) suspectBoard.DebugChooseCulprit();
            if (ARInput.EditorKey('m')) suspectBoard.DebugChooseInnocent();
#endif
            suspectBoard.HandlePointer(input);
            ui.SetCrosshair(0);
            ui.SetHint("Кто стоит за атакой?");
        }

        void ShowCulprit()
        {
            var culprit = caseFile.suspects.FirstOrDefault(s => s.isCulprit);
            if (suspectBoard) mistakes += suspectBoard.Mistakes;
            ui.SetHint("");
            ui.ShowMessage("Дело раскрыто", culprit != null ? culprit.name : "Виновный найден",
                culprit != null ? culprit.explanation : "", null, "Открыть портал", new Color(1f, 0.36f, 0.42f),
                () => StartCoroutine(OpenPortal()));
        }

        // ---------- 5. Портал и финал ----------

        IEnumerator OpenPortal()
        {
            if (board) { mistakes += board.Mistakes; Destroy(board.gameObject); }
            if (suspectBoard) Destroy(suspectBoard.gameObject);
            State = Stage.Portal;
            ui.SetHint("");
            yield return null;

            // внутренний мир портала не должен прятаться за реальной стеной
            SetOcclusion(false);

            Pose pose;
            if (!surfaces.TryWallInFront(head, out pose))
            {
                Vector3 fwd = Flat(head.forward);
                pose = new Pose(head.position + fwd * portalDistance, Quaternion.LookRotation(fwd));
            }
            // портал не ниже пола и примерно на уровне глаз
            Vector3 p = pose.position;
            p.y = Mathf.Max(surfaces.FloorY + Portal.Radius + 0.1f, head.position.y - 0.15f);
            pose.position = p;

            var lessons = collected.Where(c => !string.IsNullOrEmpty(c.lesson))
                .Select(c => new Portal.Lesson { Title = c.title, Text = c.lesson, Color = c.color }).ToList();
            portal = Portal.Create(world, pose.position, pose.rotation, head, lessons, caseFile.futureTitle);
            CipherAudio.Portal();

            yield return new WaitForSeconds(1.5f);
            ui.ShowBottom("Портал в будущее открыт", "Подойдите ближе и загляните внутрь — или шагните сквозь него с телефоном в руках.",
                "Завершить дело", ShowFinal);
        }

        void ShowFinal()
        {
            State = Stage.Final;
            ui.HideBottom();
            ui.ShowCrosshair(false);
            ui.ShowHud(false);
            float time = Time.time - startTime;
            int min = (int)(time / 60), sec = (int)(time % 60);
            string rank = mistakes == 0 ? "Мастер цифрового расследования" : mistakes <= 2 ? "Опытный детектив" : "Стажёр-следователь";
            int lessons = collected.Count(c => !string.IsNullOrEmpty(c.lesson));
            ui.ShowMessage("Итоги", "Дело закрыто",
                caseFile.finalText + $"\n\n<b>Звание:</b> {rank}\n<b>Время расследования:</b> {min}:{sec:00}\n<b>Ошибок в расследовании:</b> {mistakes}\n<b>Правил защиты изучено:</b> {lessons}",
                null, "Играть снова", Green, Restart);
            CipherAudio.Success();
        }

        void Restart() => SceneManager.LoadScene(SceneManager.GetActiveScene().name);

        // ---------- общий цикл ----------

        void Update()
        {
            switch (State)
            {
                case Stage.Scanning: UpdateScanning(); break;
                case Stage.Search: UpdateSearch(); break;
                case Stage.Board: UpdateBoard(); break;
                case Stage.Suspects: UpdateSuspects(); break;
            }
        }

        void SetOcclusion(bool on)
        {
            if (!occlusion) return;
            occlusion.requestedEnvironmentDepthMode = on ? EnvironmentDepthMode.Best : EnvironmentDepthMode.Disabled;
            occlusion.enabled = on;
        }

        static Vector3 Flat(Vector3 v)
        {
            v.y = 0;
            return v.sqrMagnitude < 0.0001f ? Vector3.forward : v.normalized;
        }
    }
}
