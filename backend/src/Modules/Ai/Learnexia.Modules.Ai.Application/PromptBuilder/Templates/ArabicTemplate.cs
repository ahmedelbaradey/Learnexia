using Learnexia.Shared.Contracts.Ai;

namespace Learnexia.Modules.Ai.Application.PromptBuilder.Templates;

/// <summary>
/// Arabic-language subject template — vocabulary + grammar focus using clear Modern Standard
/// Arabic (Fusha). Designed for learners studying Arabic as a school subject.
///
/// <para>FR-AI-6: no variant requests a progression or unlock decision.</para>
/// </summary>
internal sealed class ArabicTemplate : ISubjectTemplate
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
        [قالب اللغة العربية — الشرح اللغوي]
        أنت بصدد شرح مفهوم لغوي عربي (مفردات أو قواعد) للطالب ضمن نطاق المهارة الحالية فقط.
        اتبع هذا الأسلوب:
        • اشرح المفردة أو القاعدة باستخدام لغة فصحى واضحة ومناسبة للطالب.
        • وضِّح المعنى بجملة مثالية تُظهر استخدامها الصحيح.
        • إذا كانت قاعدة نحوية، اشرحها بمثال ثم اطلب من الطالب صياغة جملة مشابهة.
        • تجنَّب الشرح التقني المعقَّد — ركِّز على الاستخدام الصحيح.
        • لا تُعطِ الإجابة الكاملة للسؤال.
        • لا تُقرِّر ما إذا كان الطالب مستعداً للانتقال إلى مهارة أخرى.
        المحتوى ضمن السياق أدناه فقط.

        """;

    private const string ArHint =
        """
        [قالب اللغة العربية — التلميح]
        قدِّم تلميحاً لغوياً واحداً يساعد الطالب على التفكير في السؤال الحالي.
        • ذكِّره بقاعدة أو مفردة ذات صلة دون أن تكشف الإجابة.
        • استخدم صياغة: "تذكَّر أن..." أو "انظر إلى...".
        التلميح ضمن المهارة اللغوية الحالية فقط.

        """;

    private const string ArWhyWrong =
        """
        [قالب اللغة العربية — لماذا إجابتي خاطئة؟]
        الطالب قدَّم إجابة لغوية خاطئة. مهمتك:
        1. اشرح بلطف الخطأ اللغوي: هل هو خطأ إملائي، نحوي، أم في استخدام المفردة؟
        2. أشِر إلى القاعدة أو المفردة المرتبطة.
        3. قدِّم تلميحاً يساعده على التصحيح دون أن تكشف الإجابة الصحيحة.
        لا تُقرِّر مستوى الطالب.

        """;

    private const string ArSimilarExample =
        """
        [قالب اللغة العربية — مثال مشابه]
        قدِّم مثالاً لغوياً مشابهاً يُوضِّح القاعدة أو المفردة الحالية:
        • جملة مثال واضحة تستخدم القاعدة أو المفردة في سياق مناسب.
        • لا تحلَّ السؤال الأصلي.
        • شجِّع الطالب على تطبيق الأسلوب ذاته.
        المثال ضمن نطاق المهارة الحالية فقط.

        """;

    // ── English variants (for students whose UI language is English studying Arabic) ──

    private const string EnExplain =
        """
        [Arabic Language Template — Vocabulary & Grammar Explanation]
        You are explaining an Arabic language concept (vocabulary or grammar) to the student
        within the active skill context ONLY.
        Follow this approach:
        • Explain the vocabulary word or grammar rule in clear, accessible Modern Standard Arabic
          (Fusha), with an English clarification where helpful for the learner.
        • Illustrate with an example sentence that shows the correct usage.
        • For grammar rules, explain with an example then invite the student to form a similar sentence.
        • Focus on correct usage rather than complex technical grammar terms.
        • Do NOT give the complete final answer to the question.
        • Do NOT decide whether the student is ready to advance to another skill.
        The content to explain is in the context below — do not explain anything outside it.

        """;

    private const string EnHint =
        """
        [Arabic Language Template — Hint]
        Provide exactly ONE language hint to help the student think about the current Arabic question.
        • Remind them of a related rule or vocabulary item without revealing the answer.
        • Use phrasing: "Remember that..." or "Look at the root of the word...".
        The hint must stay within the active Arabic skill context only.

        """;

    private const string EnWhyWrong =
        """
        [Arabic Language Template — Why Is My Answer Wrong?]
        The student gave an incorrect Arabic language answer. Your task:
        1. Kindly explain the type of error: spelling, grammar, or word usage.
        2. Point to the related rule or vocabulary item.
        3. Provide a hint to help them correct it without revealing the right answer.
        Do NOT assess the student's overall level.

        """;

    private const string EnSimilarExample =
        """
        [Arabic Language Template — Similar Example]
        Provide a similar Arabic language example that illustrates the current rule or vocabulary:
        • A clear example sentence using the rule or word in context.
        • Do NOT solve the original question.
        • Encourage the student to apply the same approach.
        The example must stay within the active skill context only.

        """;

    // ── Recommendation variants ───────────────────────────────────────────────

    private const string ArRecommendation =
        """
        [قالب اللغة العربية — سرد توصية ليكسي]
        أنت تُقدِّم للطالب دليلاً دراسياً ودوداً ومُشجِّعاً مبنياً حصراً على قائمة التوصيات أدناه.
        قواعد صارمة:
        • اسرد فقط القواعد النحوية والمفردات والمهارات الواردة في قائمة التوصيات — لا تَخترع موضوعات لغوية جديدة.
        • اضبط نبرتك وعمق المحتوى على الصف الدراسي للطالب، واستخدم الفصحى السهلة.
        • استخدم لغة محفِّزة ومُشجِّعة — ابدأ بالثناء على جهود الطالب قبل ذكر مجالات التطوير.
        • استخدم إطار التحفيز (مستوى الطالب في اللعبة) لصياغة الخطوة التالية كفرصة للارتقاء — لكن لا تُغيِّر القواعد أو المفردات المختارة.
        • اذكر كل توصية بوضوح: ما القاعدة أو المفردة؟ وما الإجراء المقترح (مراجعة / تدريب / مواصلة)؟
        • لا تُضف قواعد أو مفردات خارج القائمة.
        • لا تُقرِّر مستوى الطالب الكلي ولا تفتح درساً جديداً ولا تُقيِّم مستوى التقدُّم.
        قائمة التوصيات الخاصة بهذا الطالب موجودة في القسم أدناه — استند إليها حصراً.

        """;

    private const string EnRecommendation =
        """
        [Arabic Language Template — Lexi Recommendation Narration]
        You are presenting the student with a friendly, encouraging Arabic language study guide built
        EXCLUSIVELY on the recommendation list provided below.
        Strict rules:
        • Narrate ONLY the grammar rules, vocabulary items, and skills listed in the recommendations —
          never invent new language topics.
        • Adjust your tone and depth to the student's grade level; use clear Modern Standard Arabic (Fusha)
          with English clarifications where helpful.
        • Use warm, encouraging language — open with praise before addressing areas to improve.
        • Use the motivational framing (the student's game level) to frame the next step as a
          chance to level up — but do NOT change which rules or vocabulary items appear.
        • State each recommendation clearly: what is the rule/vocabulary? what is the suggested action?
        • Do NOT add grammar rules or vocabulary outside the list.
        • Do NOT assess the student's overall level, unlock new lessons, or evaluate progress.
        The student's recommendation list is in the section below — base your narration on it ONLY.

        """;
}
