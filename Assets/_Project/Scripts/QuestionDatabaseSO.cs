using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "LocalQuestionDB", menuName = "PolySwipe/Question Database")]
public class QuestionDatabaseSO : ScriptableObject
{
    [Tooltip("Danh sách câu hỏi có sẵn để load ngay lập tức")]
    public List<QuestionCardSO> defaultQuestions;
}