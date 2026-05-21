# Multimodal Parsing vs Curriculum Ingestion vs Knowledge Graph

# 🎯 السؤال

ما الفرق بين:

- Multimodal Parsing
- Curriculum Ingestion
- Knowledge Graph

داخل Learnexia Architecture؟

---

# 🧠 الفكرة الأساسية

الثلاثة أجزاء مختلفة تمامًا، لكنهم يعملون معًا.

---

# 🏗️ الصورة الكبيرة

```text
Curriculum Files
        ↓
Multimodal Parsing
        ↓
Curriculum Ingestion
        ↓
Knowledge Graph
        ↓
Learning Intelligence
        ↓
AI Tutor
```

---

# 1️⃣ Multimodal Parsing

# 🎯 المسؤول عن:
> فهم الملفات نفسها

---

# ماذا يعني "Multimodal"؟

يعني النظام يفهم:
- Text
- Images
- Tables
- Equations
- Diagrams
- Layouts

---

# مثال

كتاب Science يحتوي على:
- نصوص
- صور
- جداول
- رسومات
- equations

---

# الـ Multimodal Parser يحولها إلى:

```json
{
  "text": "...",
  "images": [...],
  "tables": [...],
  "equations": [...]
}
```

---

# 🧠 دوره الأساسي

## Extraction Layer

---

# الوظائف

- OCR
- PDF parsing
- image extraction
- table extraction
- equation extraction
- layout understanding

---

# مثال

## Input

```text
PDF كتاب رياضيات
```

---

## Output

```text
Title: Fractions
Text: ...
Image: Pizza diagram
Equation: 1/2 + 1/2
```

---

# Tools

| Tool | الاستخدام |
|---|---|
| Azure DI | OCR |
| Unstructured.io | Parsing |
| PyMuPDF | PDF extraction |
| RAG Anything | Multimodal parsing |

---

# 🧠 إذًا:

## Multimodal Parsing =
> فهم الملفات الخام

---

# 2️⃣ Curriculum Ingestion

# 🎯 المسؤول عن:
> تحويل المحتوى إلى نظام تعليمي منظم

---

# بعد الـ Parsing

النظام عنده:
- text
- images
- tables

لكن:
## لا يفهم التعليم بعد

---

# هنا يأتي Curriculum Ingestion

---

# الوظائف

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

## Input

```text
Math Book
```

---

## Output

```json
{
  "grade": 5,
  "subject": "Math",
  "unit": "Fractions",
  "lesson": "Introduction to Fractions",
  "skills": [
    "Recognize fractions",
    "Compare fractions"
  ]
}
```

---

# 🧠 مسؤول عن:

- lesson extraction
- concept extraction
- skill extraction
- curriculum hierarchy
- metadata generation
- chunking
- embeddings preparation

---

# يستخدم AI هنا؟

## ✅ نعم بقوة

---

# باستخدام:

- LLM extraction
- semantic classification
- educational parsing

---

# Tools

| Tool | الاستخدام |
|---|---|
| DeepTutor concepts | Educational reasoning |
| RAGFlow | Ingestion pipelines |
| Custom AI logic | Curriculum mapping |

---

# 🧠 إذًا:

## Curriculum Ingestion =
> تحويل الملفات إلى Educational Knowledge System

---

# 3️⃣ Knowledge Graph

# 🎯 المسؤول عن:
> فهم العلاقات بين المفاهيم

---

# بعد الـ Ingestion

النظام يعرف:
- lessons
- concepts
- skills

لكن:
## لا يعرف العلاقات بينهم

---

# هنا يأتي Knowledge Graph

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

# أو:

```text
Plant Cells
   ↓
Photosynthesis
   ↓
Food Chain
```

---

# 🧠 Knowledge Graph يبني:

- dependencies
- prerequisites
- concept relationships
- learning paths

---

# يستخدم في:

- Adaptive learning
- Learning paths
- Recommendations
- Quiz generation
- Remediation
- Student modeling

---

# مثال عملي

إذا الطالب ضعيف في:

```text
Fractions
```

---

# الـ Knowledge Graph يعرف:

أنه يجب مراجعة:

```text
Division
```

أولًا.

---

# 🧠 إذًا:

## Knowledge Graph =
> فهم العلاقات التعليمية بين المفاهيم

---

# 🏗️ الفرق الكامل

| System | الوظيفة |
|---|---|
| Multimodal Parsing | فهم الملفات |
| Curriculum Ingestion | تحويلها لنظام تعليمي |
| Knowledge Graph | فهم العلاقات التعليمية |

---

# 🧠 Pipeline الكامل

```text
PDF / Images / Docs
        ↓
Multimodal Parsing
        ↓
Extracted Educational Content
        ↓
Curriculum Ingestion
        ↓
Structured Lessons & Skills
        ↓
Knowledge Graph
        ↓
Learning Intelligence
        ↓
AI Tutor + Adaptivity
```

---

# 🎯 مثال حقيقي كامل

# Step 1 — Multimodal Parsing

## النظام يقرأ:

```text
PDF Science Book
```

ويستخرج:
- text
- diagrams
- tables
- equations

---

# Step 2 — Curriculum Ingestion

## النظام يحولها إلى:

```text
Grade 5
  → Science
      → Plants
          → Photosynthesis
```

---

# Step 3 — Knowledge Graph

## النظام يفهم:

```text
Photosynthesis
   requires:
      Plant Cells
      Sunlight
```

---

# 🧠 أيهم أهم؟

## الثلاثة مهمين

لكن:

| الجزء | الأهمية |
|---|---|
| Parsing | Infrastructure |
| Ingestion | Educational Intelligence |
| Knowledge Graph | Adaptive Intelligence |

---

# 🚀 Recommended Stack

| Layer | Recommended |
|---|---|
| Multimodal Parsing | RAG Anything |
| Curriculum Ingestion | RAGFlow + Custom |
| Knowledge Graph | Neo4j لاحقًا |
| Retrieval | pgvector |
| AI Tutor | Custom |

---

# 🧠 أهم Insight

معظم الناس تبني:
- Chatbot
- RAG

---

# لكن Learnexia يحتاج:

> 🧠 Educational Intelligence Architecture

وده يبدأ من:
- Parsing
- ثم Ingestion
- ثم Knowledge Graph
- ثم Adaptive Learning
