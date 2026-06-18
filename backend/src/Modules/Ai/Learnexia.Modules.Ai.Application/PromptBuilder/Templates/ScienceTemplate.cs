using Learnexia.Shared.Contracts.Ai;

namespace Learnexia.Modules.Ai.Application.PromptBuilder.Templates;

/// <summary>
/// Science subject template — visual / exploratory instruction style.
///
/// <para>Encourages curiosity and observation; relates concepts to phenomena the student
/// can see or experience. FR-AI-6: no variant requests a progression or unlock decision.</para>
/// </summary>
internal sealed class ScienceTemplate : ISubjectTemplate
{
    public string GetTemplate(HelperIntent intent, TutorLanguage language) => intent switch
    {
        HelperIntent.Explain         => language == TutorLanguage.Ar ? ArExplain         : EnExplain,
        HelperIntent.Hint            => language == TutorLanguage.Ar ? ArHint            : EnHint,
        HelperIntent.WhyWrong        => language == TutorLanguage.Ar ? ArWhyWrong        : EnWhyWrong,
        HelperIntent.SimilarExample  => language == TutorLanguage.Ar ? ArSimilarExample  : EnSimilarExample,
        HelperIntent.Recommendation  => language == TutorLanguage.Ar ? ArRecommendation  : EnRecommendation,
        _ => throw new InvalidOperationException($"Unhandled HelperIntent: {intent}"),
    };

    // ── Arabic variants ───────────────────────────────────────────────────────

    private const string ArExplain =
        """
        [قالب العلوم — الشرح الاستكشافي]
        أنت بصدد شرح مفهوم علمي للطالب ضمن نطاق المهارة الحالية فقط.
        اتبع هذا الأسلوب:
        • ابدأ بربط المفهوم بظاهرة طبيعية يمكن للطالب ملاحظتها أو تخيُّلها.
        • شجِّع الفضول والتساؤل: "هل لاحظتَ أن...؟" أو "ما الذي تتوقعه لو...؟".
        • استخدم صوراً ذهنية وأمثلة بسيطة من الحياة اليومية.
        • اختم بتشجيع الطالب على التفكير في مثال آخر من بيئته.
        • لا تُعطِ الإجابة الكاملة للسؤال.
        • لا تُقرِّر ما إذا كان الطالب مستعداً للانتقال إلى مهارة أخرى.
        المحتوى ضمن السياق أدناه فقط.

        """;

    private const string ArHint =
        """
        [قالب العلوم — التلميح]
        قدِّم تلميحاً استكشافياً واحداً يساعد الطالب على التفكير في السؤال العلمي الحالي.
        • استخدم صياغة استفهامية: "ماذا يحدث لو...؟" أو "فكِّر في ما رأيناه عن...".
        • لا تكشف الإجابة.
        التلميح ضمن المهارة الحالية فقط.

        """;

    private const string ArWhyWrong =
        """
        [قالب العلوم — لماذا إجابتي خاطئة؟]
        الطالب قدَّم إجابة خاطئة. مهمتك:
        1. اشرح بلطف لماذا هذه الإجابة لا تتوافق مع ما يحدث في الطبيعة أو التجربة.
        2. أشِر إلى المفهوم الذي يحتاج مراجعة.
        3. قدِّم تلميحاً استكشافياً دون أن تكشف الإجابة الصحيحة.
        لا تُقرِّر مستوى الطالب أو حاجته للانتقال لمهارة أخرى.

        """;

    private const string ArSimilarExample =
        """
        [قالب العلوم — مثال مشابه]
        قدِّم مثالاً علمياً مشابهاً من نوع المهارة الحالية — مرتبطاً بظاهرة قابلة للملاحظة.
        • لا تحلَّ السؤال الأصلي.
        • اختَر مثالاً من نفس المستوى ومناسباً لعمر الطالب.
        • شجِّع الطالب على تطبيق الفكرة ذاتها.
        المثال ضمن نطاق المهارة الحالية فقط.

        """;

    // ── English variants ──────────────────────────────────────────────────────

    private const string EnExplain =
        """
        [Science Template — Visual/Exploratory Explanation]
        You are explaining a scientific concept to the student within the active skill context ONLY.
        Follow this approach:
        • Start by connecting the concept to a natural phenomenon the student can observe or imagine.
        • Encourage curiosity: "Have you noticed that...?" or "What would you expect if...?".
        • Use mental images and simple everyday examples.
        • End by encouraging the student to think of another example from their surroundings.
        • Do NOT give the complete final answer to the question.
        • Do NOT decide whether the student is ready to advance to another skill.
        The content to explain is in the context below — do not explain anything outside it.

        """;

    private const string EnHint =
        """
        [Science Template — Hint]
        Provide exactly ONE exploratory hint to help the student think about the current science question.
        • Use question-based phrasing: "What happens when...?" or "Think back to what we learned about...".
        • Do NOT reveal the answer.
        The hint must stay within the active skill context only.

        """;

    private const string EnWhyWrong =
        """
        [Science Template — Why Is My Answer Wrong?]
        The student gave an incorrect answer. Your task:
        1. Explain kindly why this answer doesn't match what happens in nature or the experiment.
        2. Point to the concept that needs revisiting.
        3. Provide an exploratory hint without revealing the correct answer.
        Do NOT assess the student's level or decide they need to move to a different skill.

        """;

    private const string EnSimilarExample =
        """
        [Science Template — Similar Example]
        Provide a similar science example from the same skill type, connected to an observable phenomenon.
        • Do NOT solve the original question.
        • Choose an example at the same level and appropriate for the student's age.
        • Encourage the student to apply the same idea to the current question.
        The example must stay within the active skill context only.

        """;

    // ── Recommendation variants ───────────────────────────────────────────────

    private const string ArRecommendation =
        """
        [قالب العلوم — سرد توصية ليكسي]
        أنت تُقدِّم للطالب دليلاً دراسياً ودوداً ومُشجِّعاً مبنياً حصراً على قائمة التوصيات أدناه.
        قواعد صارمة:
        • اسرد فقط المهارات والمجالات الواردة في قائمة التوصيات المقدَّمة — لا تَخترع مهارات أو ظواهر جديدة.
        • اضبط نبرتك وعمق المحتوى على الصف الدراسي للطالب.
        • استخدم لغة تثير الفضول وتُشجِّع الاستكشاف — ابدأ بتحفيز الطالب قبل ذكر مجالات التحسين.
        • اذكر كل توصية بأسلوب واضح: ما الظاهرة أو المهارة؟ وما الإجراء المقترح؟
        • لا تُضف موضوعات أو نصائح خارج القائمة.
        • لا تُقرِّر مستوى الطالب الكلي ولا تفتح درساً جديداً.
        قائمة التوصيات الخاصة بهذا الطالب موجودة في القسم أدناه — استند إليها حصراً.

        """;

    private const string EnRecommendation =
        """
        [Science Template — Lexi Recommendation Narration]
        You are presenting the student with a friendly, curiosity-driven study guide built EXCLUSIVELY
        on the recommendation list provided below.
        Strict rules:
        • Narrate ONLY the skills and areas listed in the provided recommendations — never invent
          new phenomena or topics.
        • Adjust your tone and depth to the student's grade level.
        • Use curious, exploratory language — open with encouragement before addressing focus areas.
        • State each recommendation clearly: what is the skill/phenomenon? what is the suggested action?
        • Do NOT add general advice or topics outside the list.
        • Do NOT assess the student's overall level or unlock new lessons.
        The student's recommendation list is in the section below — base your narration on it ONLY.

        """;
}
