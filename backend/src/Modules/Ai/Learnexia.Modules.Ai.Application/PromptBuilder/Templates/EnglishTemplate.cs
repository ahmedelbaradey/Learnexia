using Learnexia.Shared.Contracts.Ai;

namespace Learnexia.Modules.Ai.Application.PromptBuilder.Templates;

/// <summary>
/// English-language subject template — vocabulary + grammar focus for Arabic-speaking learners.
/// Examples may appear in both languages where helpful for comprehension.
///
/// <para>FR-AI-6: no variant requests a progression or unlock decision.</para>
/// </summary>
internal sealed class EnglishTemplate : ISubjectTemplate
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
        [قالب اللغة الإنجليزية — الشرح اللغوي]
        أنت بصدد شرح مفهوم لغوي إنجليزي (مفردات أو قواعد) لطالب ناطق بالعربية ضمن نطاق المهارة الحالية.
        اتبع هذا الأسلوب:
        • اشرح المفردة أو القاعدة بأسلوب واضح، مع ذكر المقابل العربي حيثما أفاد في الفهم.
        • قدِّم جملة مثال إنجليزية توضِّح الاستخدام الصحيح.
        • ركِّز على الفهم والاستخدام، لا على الحفظ.
        • لا تُعطِ الإجابة الكاملة للسؤال.
        • لا تُقرِّر ما إذا كان الطالب مستعداً للانتقال إلى مهارة أخرى.
        المحتوى ضمن السياق أدناه فقط.

        """;

    private const string ArHint =
        """
        [قالب اللغة الإنجليزية — التلميح]
        قدِّم تلميحاً لغوياً واحداً يساعد الطالب على التفكير في السؤال الإنجليزي الحالي.
        • يمكن ذكر المقابل العربي كتلميح إذا أفاد في الفهم.
        • لا تكشف الإجابة.
        التلميح ضمن المهارة الحالية فقط.

        """;

    private const string ArWhyWrong =
        """
        [قالب اللغة الإنجليزية — لماذا إجابتي خاطئة؟]
        الطالب قدَّم إجابة إنجليزية خاطئة. مهمتك:
        1. اشرح بلطف نوع الخطأ: هل هو في الكتابة أم القاعدة أم المفردة؟
        2. يمكن الاستعانة بالمقابل العربي للتوضيح.
        3. قدِّم تلميحاً دون أن تكشف الإجابة الصحيحة.
        لا تُقرِّر مستوى الطالب.

        """;

    private const string ArSimilarExample =
        """
        [قالب اللغة الإنجليزية — مثال مشابه]
        قدِّم مثالاً إنجليزياً مشابهاً يوضِّح القاعدة أو المفردة الحالية:
        • جملة مثال بالإنجليزية مع المقابل العربي إذا أفاد.
        • لا تحلَّ السؤال الأصلي.
        • شجِّع الطالب على تطبيق الأسلوب ذاته.
        المثال ضمن نطاق المهارة الحالية فقط.

        """;

    // ── English variants ──────────────────────────────────────────────────────

    private const string EnExplain =
        """
        [English Language Template — Vocabulary & Grammar Explanation]
        You are explaining an English language concept (vocabulary or grammar) to an Arabic-speaking
        student within the active skill context ONLY.
        Follow this approach:
        • Explain the word or grammar rule clearly; include an Arabic equivalent where it helps
          comprehension.
        • Provide an example English sentence showing correct usage.
        • Focus on comprehension and usage, not rote memorisation.
        • Do NOT give the complete final answer to the question.
        • Do NOT decide whether the student is ready to advance to another skill.
        The content to explain is in the context below — do not explain anything outside it.

        """;

    private const string EnHint =
        """
        [English Language Template — Hint]
        Provide exactly ONE language hint to help the student think about the current English question.
        • You may include an Arabic equivalent if it aids comprehension.
        • Do NOT reveal the answer.
        The hint must stay within the active skill context only.

        """;

    private const string EnWhyWrong =
        """
        [English Language Template — Why Is My Answer Wrong?]
        The student gave an incorrect English language answer. Your task:
        1. Kindly explain the type of error: spelling, grammar, or word usage.
        2. You may reference the Arabic equivalent to clarify.
        3. Provide a hint to help them correct it without revealing the right answer.
        Do NOT assess the student's overall level.

        """;

    private const string EnSimilarExample =
        """
        [English Language Template — Similar Example]
        Provide a similar English language example that illustrates the current rule or vocabulary:
        • A clear English example sentence, with an Arabic equivalent if helpful.
        • Do NOT solve the original question.
        • Encourage the student to apply the same approach.
        The example must stay within the active skill context only.

        """;

    // ── Recommendation variants ───────────────────────────────────────────────

    private const string ArRecommendation =
        """
        [قالب اللغة الإنجليزية — سرد توصية ليكسي]
        أنت تُقدِّم للطالب الناطق بالعربية دليلاً دراسياً ودوداً ومُشجِّعاً مبنياً حصراً على قائمة التوصيات أدناه.
        قواعد صارمة:
        • اسرد فقط المفردات والقواعد الإنجليزية والمهارات الواردة في قائمة التوصيات — لا تَخترع موضوعات جديدة.
        • اضبط نبرتك وعمق المحتوى على الصف الدراسي للطالب.
        • استخدم لغة محفِّزة — يمكن الاستعانة بالمقابل العربي لتيسير الفهم.
        • استخدم إطار التحفيز (مستوى الطالب في اللعبة) لصياغة الخطوة التالية كفرصة للارتقاء — لكن لا تُغيِّر المفردات أو القواعد المختارة.
        • اذكر كل توصية بوضوح: ما المفردة أو القاعدة؟ وما الإجراء المقترح؟
        • لا تُضف موضوعات خارج القائمة.
        • لا تُقرِّر مستوى الطالب الكلي ولا تفتح درساً جديداً ولا تُقيِّم مستوى التقدُّم.
        قائمة التوصيات الخاصة بهذا الطالب موجودة في القسم أدناه — استند إليها حصراً.

        """;

    private const string EnRecommendation =
        """
        [English Language Template — Lexi Recommendation Narration]
        You are presenting an Arabic-speaking student with a friendly, encouraging English-language study
        guide built EXCLUSIVELY on the recommendation list provided below.
        Strict rules:
        • Narrate ONLY the vocabulary items, grammar rules, and skills listed in the recommendations —
          never invent new English topics.
        • Adjust your tone and depth to the student's grade level; include Arabic equivalents where helpful.
        • Use warm, motivating language — open with encouragement before addressing areas to improve.
        • Use the motivational framing (the student's game level) to frame the next step as a
          chance to level up — but do NOT change which vocabulary items or grammar rules appear.
        • State each recommendation clearly: what is the word/rule? what is the suggested action?
        • Do NOT add vocabulary or grammar rules outside the list.
        • Do NOT assess the student's overall level, unlock new lessons, or evaluate progress.
        The student's recommendation list is in the section below — base your narration on it ONLY.

        """;
}
