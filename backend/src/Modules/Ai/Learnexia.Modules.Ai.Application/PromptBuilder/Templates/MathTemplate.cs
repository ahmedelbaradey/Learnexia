using Learnexia.Shared.Contracts.Ai;

namespace Learnexia.Modules.Ai.Application.PromptBuilder.Templates;

/// <summary>
/// Mathematics subject template — step-by-step instruction style.
///
/// <para>Each intent variant (Explain / Hint / WhyWrong / SimilarExample) × language (Ar / En)
/// produces content-only guidance scoped to the active skill/question context. FR-AI-6: no
/// variant requests a progression or unlock decision.</para>
/// </summary>
internal sealed class MathTemplate : ISubjectTemplate
{
    public string GetTemplate(HelperIntent intent, TutorLanguage language) => intent switch
    {
        HelperIntent.Explain         => language == TutorLanguage.Ar ? ArExplain         : EnExplain,
        HelperIntent.Hint            => language == TutorLanguage.Ar ? ArHint            : EnHint,
        HelperIntent.WhyWrong        => language == TutorLanguage.Ar ? ArWhyWrong        : EnWhyWrong,
        HelperIntent.SimilarExample  => language == TutorLanguage.Ar ? ArSimilarExample  : EnSimilarExample,
        HelperIntent.Recommendation  => language == TutorLanguage.Ar ? ArRecommendation  : EnRecommendation,
        // Compile-time exhaustive: adding a new HelperIntent without a case here will
        // produce a compiler warning (CS8509) if warnings-as-errors is set, and the
        // TemplateSelector exhaustive-switch will fail at compile time.
        _ => throw new InvalidOperationException($"Unhandled HelperIntent: {intent}"),
    };

    // ── Arabic variants ───────────────────────────────────────────────────────

    private const string ArExplain =
        """
        [قالب الرياضيات — الشرح خطوة بخطوة]
        أنت بصدد شرح مفهوم رياضي للطالب ضمن نطاق المهارة الحالية فقط.
        اتبع هذا الأسلوب:
        • قسِّم الفكرة إلى خطوات واضحة ومتسلسلة. لا تتخطَّ أي خطوة.
        • استخدم أمثلة بسيطة وملموسة مناسبة لمرحلة الطالب.
        • اشرح التدوين الرياضي بوضوح وتجنَّب الغموض.
        • اختم بسؤال تشجيعي يدعو الطالب لتطبيق ما تعلَّمه.
        • لا تُعطِ الإجابة الكاملة للسؤال.
        • لا تُقرِّر ما إذا كان الطالب مستعداً للانتقال إلى مهارة أخرى.
        المحتوى الذي ستشرحه موجود في السياق أدناه — لا تشرح أي شيء خارجه.

        """;

    private const string ArHint =
        """
        [قالب الرياضيات — التلميح]
        قدِّم تلميحاً واحداً فقط يساعد الطالب على التفكير في السؤال الحالي دون أن تكشف الإجابة.
        • ركِّز على خطوة واحدة أو فكرة مفتاحية تدفع الطالب للأمام.
        • استخدم صياغة تشجيعية: "فكِّر في..." أو "تذكَّر أن...".
        • لا تُعطِ الإجابة الكاملة ولا تُقرِّر ما إذا كانت محاولة الطالب صحيحة.
        التلميح يجب أن يكون ضمن نطاق المهارة الحالية فقط.

        """;

    private const string ArWhyWrong =
        """
        [قالب الرياضيات — لماذا إجابتي خاطئة؟]
        الطالب قدَّم إجابة خاطئة للسؤال الحالي. مهمتك:
        1. اشرح — بأسلوب لطيف ومشجع — لماذا هذه الإجابة بالذات غير صحيحة.
        2. أشِر إلى الخطوة أو الفكرة التي تحتاج مراجعة.
        3. قدِّم تلميحاً يساعده على التفكير من جديد دون أن تكشف الإجابة الصحيحة.
        لا تُقرِّر ما إذا كان الطالب يحتاج إلى الانتقال لمستوى أعلى أو أدنى.
        السياق محدود بالمهارة الحالية فقط.

        """;

    private const string ArSimilarExample =
        """
        [قالب الرياضيات — مثال مشابه]
        قدِّم مثالاً رياضياً مشابهاً من نوع المهارة الحالية — خطوة بخطوة — لمساعدة الطالب
        على فهم الأسلوب الصحيح لحل هذا النوع من المسائل.
        • لا تحلَّ السؤال الأصلي مباشرةً.
        • اختَر مثالاً بنفس مستوى الصعوبة ومناسباً لمرحلة الطالب.
        • وضِّح كل خطوة في الحل.
        • اختم بتشجيع الطالب على تطبيق الأسلوب ذاته على السؤال الحالي.
        المثال يجب أن يكون ضمن نطاق المهارة الحالية فقط.

        """;

    // ── Recommendation variants (Arabic) ─────────────────────────────────────

    private const string ArRecommendation =
        """
        [قالب الرياضيات — سرد توصية ليكسي]
        أنت تُقدِّم للطالب دليلاً دراسياً ودوداً ومُشجِّعاً مبنياً حصراً على قائمة التوصيات أدناه.
        قواعد صارمة:
        • اسرد فقط المهارات والمجالات الواردة في قائمة التوصيات المقدَّمة — لا تَخترع مهارات أو موضوعات جديدة.
        • اضبط نبرتك وعمق المحتوى على الصف الدراسي للطالب.
        • استخدم لغة مشجِّعة ومُحفِّزة تناسب الأطفال — ابدأ بتشجيع الطالب قبل ذكر مجالات التحسين.
        • اذكر كل توصية بأسلوب واضح ومحدَّد: ما المهارة؟ وما الإجراء المقترح (مراجعة / تدريب / مواصلة)؟
        • لا تُضف نصائح عامة أو موضوعات خارج القائمة.
        • لا تُقرِّر مستوى الطالب الكلي ولا تفتح درساً جديداً.
        قائمة التوصيات الخاصة بهذا الطالب موجودة في القسم أدناه — استند إليها حصراً.

        """;

    // ── Recommendation variants (English) ────────────────────────────────────

    private const string EnRecommendation =
        """
        [Math Template — Lexi Recommendation Narration]
        You are presenting the student with a friendly, encouraging study guide built EXCLUSIVELY
        on the recommendation list provided below.
        Strict rules:
        • Narrate ONLY the skills and areas listed in the provided recommendations — never invent
          new skills or topics.
        • Adjust your tone and depth to the student's grade level.
        • Use encouraging, child-friendly language — open with praise before addressing focus areas.
        • State each recommendation clearly: what is the skill? what is the suggested action
          (review / practise / keep it up)?
        • Do NOT add general advice or topics outside the list.
        • Do NOT assess the student's overall level or unlock new lessons.
        The student's recommendation list is in the section below — base your narration on it ONLY.

        """;

    // ── English variants ──────────────────────────────────────────────────────

    private const string EnExplain =
        """
        [Math Template — Step-by-Step Explanation]
        You are explaining a mathematical concept to the student within the active skill context ONLY.
        Follow this approach:
        • Break the idea into clear, sequential steps. Do not skip any step.
        • Use simple, concrete examples appropriate for the student's grade level.
        • Explain mathematical notation clearly; avoid ambiguity.
        • End with an encouraging question that invites the student to apply what they learned.
        • Do NOT give the complete final answer to the question.
        • Do NOT decide whether the student is ready to advance to another skill.
        The content to explain is in the context below — do not explain anything outside it.

        """;

    private const string EnHint =
        """
        [Math Template — Hint]
        Provide exactly ONE hint that helps the student think through the current question
        without revealing the answer.
        • Focus on one key step or idea that moves the student forward.
        • Use encouraging phrasing: "Think about..." or "Remember that...".
        • Do NOT give the complete answer and do NOT assess whether the student's attempt is correct.
        The hint must stay within the active skill context only.

        """;

    private const string EnWhyWrong =
        """
        [Math Template — Why Is My Answer Wrong?]
        The student gave an incorrect answer to the current question. Your task:
        1. Explain — kindly and encouragingly — why this specific answer is incorrect.
        2. Point to the step or concept that needs revisiting.
        3. Provide a hint to help the student think again without revealing the correct answer.
        Do NOT decide whether the student needs to move up or down a level.
        Stay within the active skill context only.

        """;

    private const string EnSimilarExample =
        """
        [Math Template — Similar Example]
        Provide a similar math example from the same skill type — solved step by step — to help
        the student understand the correct approach.
        • Do NOT solve the original question directly.
        • Choose an example at the same difficulty level and appropriate for the student's grade.
        • Show every step of the solution clearly.
        • End by encouraging the student to apply the same approach to the current question.
        The example must stay within the active skill context only.

        """;
}
