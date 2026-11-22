using System.Collections.Generic;
using UnityEngine;

namespace Cipher
{
    /// <summary>Подозреваемый. Виновен ровно один.</summary>
    [System.Serializable]
    public class Suspect
    {
        public string name = "Имя Фамилия";
        public string role = "Должность";
        [TextArea(2, 4), Tooltip("Почему его подозревают")]
        public string suspicion;
        [TextArea(2, 6), Tooltip("Если виновен — разгадка; если нет — алиби, которое покажет игра")]
        public string explanation;
        public bool isCulprit;
        public Color color = new Color(0.3f, 0.95f, 1f);
    }

    /// <summary>
    /// «Дело» — весь сюжет игры в одном файле. Редактируется прямо в Inspector:
    /// тексты, список улик (префабы), подсказки для доски, подозреваемые и финал.
    /// </summary>
    [CreateAssetMenu(menuName = "Cipher/Дело (сюжет)", fileName = "Case")]
    public class CaseFile : ScriptableObject
    {
        [Header("Заголовок")]
        public string gameTitle = "CIPHER";
        public string caseTitle = "Утечка данных в «Нова Софт»";
        [TextArea(4, 12)] public string briefing;

        [Header("Улики (префабы с компонентом Cipher Clue)")]
        public List<CipherClue> clues = new List<CipherClue>();

        [Header("Доска расследования")]
        [TextArea(2, 6)] public string boardTask;
        [TextArea(2, 6)] public string solvedText;

        [Header("Кто это сделал? (подозреваемые)")]
        [TextArea(2, 6)] public string suspectTask;
        public List<Suspect> suspects = new List<Suspect>();

        [Header("Финал")]
        public string futureTitle = "2030 · СИСТЕМА ЗАЩИЩЕНА";
        [TextArea(2, 6)] public string finalText;
    }
}
