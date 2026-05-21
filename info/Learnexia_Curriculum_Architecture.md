# Curriculum Upload & Processing Architecture — Learnexia

# 🎯 السؤال

كيف سيتم:
- رفع المناهج
- فهم المحتوى
- تحويل الكتب إلى Knowledge System

داخل Learnexia؟

وهل سيتم الاعتماد على DeepTutor؟

---

# 🧠 الإجابة المختصرة

Learnexia يحتاج إلى:

> 🧠 Curriculum Intelligence Pipeline

وهو نظام مستقل مسؤول عن:
- فهم المناهج
- تقسيم المحتوى
- بناء Knowledge Graph
- تجهيز المحتوى للـ AI

---

# ❌ المناهج لا تذهب مباشرة إلى الـ AI

---

# ✅ بل تمر بمراحل Processing كاملة

---

# 🏗️ High-Level Architecture

```text
Curriculum Files
        ↓
Document Processing
        ↓
Content Extraction
        ↓
Curriculum Structuring
        ↓
Knowledge Graph Builder
        ↓
Chunking Engine
        ↓
Embeddings Layer
        ↓
Vector Database
        ↓
Retrieval Layer
        ↓
Prompt Builder
        ↓
AI Tutor Engine
```

---

# 🧠 هل سنستخدم DeepTutor؟

## ✅ نعم
لكن:
- كـ inspiration
- وكـ educational intelligence patterns

---

# سيتم استخدامه في:

- RAG flows
- educational reasoning
- tutoring prompts
- lesson decomposition
- curriculum grounding

---

# ❌ لن نعتمد عليه كنظام Curriculum كامل

لأن:
- Learnexia يحتاج Architecture مختلفة
- scalable curriculum ingestion
- structured educational pipelines

---

# 🏗️ المرحلة الأولى

# 1. Curriculum Upload System

# المسؤول عن:

رفع:
- PDF
- DOCX
- Worksheets
- Images
- Slides

---

# من يرفع؟

- Admin
- Teacher
- Content Manager

---

# Metadata Required

```json
{
  "grade": 5,
  "subject": "Math",
  "language": "Arabic",
  "country": "Egypt"
}
```

---

# 🏗️ المرحلة الثانية

# 2. Document Processing Layer

# المسؤول عن:

- OCR
- text extraction
- image extraction
- layout analysis

---

# Recommended Tools

| Task | Tool |
|---|---|
| PDF Parsing | PyMuPDF |
| OCR | Azure Document Intelligence |
| Structured Parsing | Unstructured.io |
| Arabic OCR | Azure OCR |

---

# ⚠️ مهم جدًا

المناهج العربية غالبًا:
- Scanned PDFs
- صور
- formatting ضعيف

لذلك:
## OCR مهم جدًا

---

# 🏗️ المرحلة الثالثة

# 3. Curriculum Structuring Engine

# أهم Layer فعليًا

---

# المسؤول عن:

تحويل:

```text
كتاب PDF
```

إلى:

```text
Grade
  ↓
Subject
  ↓
Unit
  ↓
Lesson
  ↓
Concept
  ↓
Skill
```

---

# مثال

```text
Grade 5
 └── Math
      └── Fractions
            ├── Concepts
            ├── Examples
            ├── Exercises
            └── Skills
```

---

# 🧠 هنا يتم استخدام AI بقوة

## باستخدام:

- semantic parsing
- LLM extraction
- concept mapping
- educational classification

---

# 🏗️ المرحلة الرابعة

# 4. Knowledge Graph Builder

# الهدف

بناء العلاقات بين المفاهيم.

---

# مثال

```text
Numbers
   ↓
Addition
   ↓
Fractions
   ↓
Word Problems
```

---

# يستخدمه:

- Learning Path Engine
- Adaptivity Engine
- Quiz Generator
- AI Tutor

---

# 🧠 DeepTutor مفيد هنا جدًا

خصوصًا في:
- lesson decomposition
- educational reasoning
- knowledge grounding

---

# 🏗️ المرحلة الخامسة

# 5. Chunking Engine

# الهدف

تقسيم المحتوى إلى أجزاء مناسبة للـ RAG.

---

# ❌ لا تستخدم Fixed Chunking

---

# ✅ استخدم Semantic Educational Chunking

---

# مثال

```text
Chunk:
- Concept = Fractions
- Difficulty = Easy
- Examples = Pizza examples
- Exercises = 5 questions
```

---

# لماذا؟

لأن:
- retrieval يصبح أدق
- AI يفهم السياق التعليمي أفضل

---

# 🏗️ المرحلة السادسة

# 6. Embeddings Layer

# الهدف

تحويل المحتوى إلى vectors.

---

# أفضل Models

| Model | Notes |
|---|---|
| OpenAI Embeddings | ممتاز |
| BGE-M3 | قوي multilingual |
| E5 Multilingual | ممتاز |
| Jina Embeddings | جيد |

---

# Recommended

## BGE-M3

لأنه:
- multilingual
- جيد للعربية
- قوي في retrieval

---

# 🏗️ المرحلة السابعة

# 7. Vector Database

# Recommended

## PostgreSQL + pgvector

---

# لماذا؟

- بسيط
- scalable
- PostgreSQL native
- ممتاز للـ MVP

---

# 🏗️ المرحلة الثامنة

# 8. Curriculum Metadata System

# كل Chunk يجب أن يحتوي على:

```json
{
  "grade": 5,
  "subject": "Math",
  "unit": "Fractions",
  "difficulty": "Easy",
  "language": "Arabic",
  "country": "Egypt"
}
```

---

# لماذا هذا مهم؟

لأن:
- retrieval يصبح دقيق
- personalization أفضل
- adaptive learning أسهل

---

# 🏗️ المرحلة التاسعة

# 9. Retrieval Layer

# عند سؤال الطالب:

```text
ما هي الكسور؟
```

---

# النظام يعمل:

```text
Student Profile
        ↓
Grade
        ↓
Subject
        ↓
Retrieve Curriculum Chunks
        ↓
Prompt Builder
        ↓
LLM
```

---

# 🧠 أهم Layer فعليًا

# Prompt Builder

---

# مسؤول عن:

- age
- grade
- subject
- difficulty
- weak areas
- learning style
- language

---

# Example Prompt

```text
Explain fractions to a 9-year-old Arabic-speaking student.

Use:
- simple language
- visual examples
- encouragement
```

---

# 🏗️ AI Tutor Layer

# يستخدم:

- curriculum chunks
- student profile
- learning path
- adaptivity state

---

# لتوليد:

- explanations
- quizzes
- hints
- reinforcement

---

# 🧠 هل نحتاج Fine-Tuning؟

# ❌ ليس في البداية

---

# الأفضل:

## ✅ RAG + Curriculum Intelligence

---

# لماذا؟

لأن:
- أسهل تحديثًا
- أرخص
- dynamic
- scalable

---

# Fine-Tuning يستخدم لاحقًا فقط إذا:

- لديك data ضخمة
- tutoring patterns خاصة جدًا
- scale كبير

---

# 🏗️ Recommended Tech Stack

| Layer | Technology |
|---|---|
| OCR | Azure Document Intelligence |
| Parsing | Unstructured.io |
| Chunking | Custom Semantic Chunker |
| Embeddings | BGE-M3 |
| Vector DB | pgvector |
| AI | GPT/Gemini |
| Backend | ASP.NET Core |
| Frontend | Next.js |

---

# 🧠 Future Architecture

# لاحقًا يمكن إضافة:

## Neo4j Knowledge Graph

---

# لاستخدامه في:

- advanced learning paths
- prerequisite mapping
- concept relationships
- adaptive recommendations

---

# 🧠 أهم Insight

أكبر ميزة تنافسية مستقبلًا ليست:
- الـ UI
- أو الـ LLM

---

# بل:

> 🧠 Curriculum Intelligence Layer

---

# لأنه المسؤول عن:

- فهم المناهج
- بناء skill trees
- adaptivity
- quiz generation
- personalized tutoring

---

# 🧠 العلاقة مع DeepTutor

# DeepTutor يساعد في:

- tutoring logic
- educational reasoning
- RAG flows
- concept decomposition

---

# لكنه ليس:

- curriculum ingestion platform
- educational CMS
- scalable curriculum pipeline

---

# لذلك:

## استخدم أفكاره
وابنِ:
> 🧠 Curriculum Intelligence Platform خاص بـ Learnexia

---

# 🎯 Final Architecture Summary

```text
Curriculum Upload
        ↓
Document Processing
        ↓
AI Curriculum Structuring
        ↓
Knowledge Graph
        ↓
Chunking
        ↓
Embeddings
        ↓
pgvector
        ↓
Retrieval Layer
        ↓
Prompt Builder
        ↓
AI Tutor
```

---

# 🚀 Final Strategic Insight

نجاح Learnexia الحقيقي لن يأتي فقط من:
- AI Models

لكن من:

- Curriculum Intelligence
- Adaptive Learning
- Educational Knowledge Systems
- Gamified Learning Architecture
