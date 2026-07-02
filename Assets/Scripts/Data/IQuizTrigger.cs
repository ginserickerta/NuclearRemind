namespace NuclearReMind
{
    /// <summary>
    /// สิ่งที่สามารถ "ปล่อยควิซ" ได้ (Dilemma/Crisis/Decree/Tech ฯลฯ) — V4 §16
    /// QuizManager.EnqueueQuizzes(IQuizTrigger) เรียก GetLinkedQuizzes() เพื่อเอาควิซที่ผูกไว้เข้าคิวถาม
    /// </summary>
    public interface IQuizTrigger
    {
        QuizQuestionSO[] GetLinkedQuizzes();
    }
}
