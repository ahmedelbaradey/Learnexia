/**
 * auditActionLabels — centralized EN + AR labels for AdminActions.* constants
 * and target entity-type strings.
 *
 * Sourced from:
 *   - backend/src/Shared/Learnexia.Shared.Contracts/Admin/AdminActions.cs
 *   - Design Spec §B.2.1 (action labels) + §B.2.2 (target type labels)
 *
 * Used by: ActionTypeBadge (color keying + label), AuditLogPage (select groups),
 *          AuditEntryDetail (label display).
 *
 * IMPORTANT:
 * - No design patterns (Rule #8). This is a plain const map + helper functions.
 * - Every action constant from AdminActions.cs MUST have an entry here.
 * - Both `en` and `ar` values are REQUIRED for every entry — no missing/empty.
 * - Falls back to the raw action string for any unregistered future constant
 *   (forward-compatible — new P7-13+ actions added after P7-12 still work).
 */

export type Locale = 'en' | 'ar';

// ── Action type → localized label map ─────────────────────────────────────────

/**
 * Map of AdminActions.* constant string → { en, ar } display labels.
 * Wire values are exact strings stored in the audit log `action` column.
 */
export const ACTION_LABELS: Record<string, { en: string; ar: string }> = {
  // P7-01 Subject
  'Subject.Created':    { en: 'Subject Created',    ar: 'إنشاء موضوع' },
  'Subject.Updated':    { en: 'Subject Updated',    ar: 'تحديث موضوع' },
  'Subject.Deleted':    { en: 'Subject Deleted',    ar: 'حذف موضوع' },
  'Subject.Reordered':  { en: 'Subject Reordered',  ar: 'إعادة ترتيب موضوع' },
  'Subject.Activated':  { en: 'Subject Activated',  ar: 'تفعيل موضوع' },
  'Subject.Deactivated': { en: 'Subject Deactivated', ar: 'إلغاء تفعيل موضوع' },

  // P7-01 Unit
  'Unit.Created':    { en: 'Unit Created',    ar: 'إنشاء وحدة' },
  'Unit.Updated':    { en: 'Unit Updated',    ar: 'تحديث وحدة' },
  'Unit.Deleted':    { en: 'Unit Deleted',    ar: 'حذف وحدة' },
  'Unit.Reordered':  { en: 'Unit Reordered',  ar: 'إعادة ترتيب وحدة' },
  'Unit.Activated':  { en: 'Unit Activated',  ar: 'تفعيل وحدة' },
  'Unit.Deactivated': { en: 'Unit Deactivated', ar: 'إلغاء تفعيل وحدة' },

  // P7-02 Lesson
  'Lesson.Created':    { en: 'Lesson Created',    ar: 'إنشاء درس' },
  'Lesson.Updated':    { en: 'Lesson Updated',    ar: 'تحديث درس' },
  'Lesson.Deleted':    { en: 'Lesson Deleted',    ar: 'حذف درس' },
  'Lesson.Reordered':  { en: 'Lesson Reordered',  ar: 'إعادة ترتيب درس' },
  'Lesson.Activated':  { en: 'Lesson Activated',  ar: 'تفعيل درس' },
  'Lesson.Deactivated': { en: 'Lesson Deactivated', ar: 'إلغاء تفعيل درس' },

  // P7-02 ContentBlock
  'ContentBlock.Added':     { en: 'Content Block Added',     ar: 'إضافة كتلة محتوى' },
  'ContentBlock.Updated':   { en: 'Content Block Updated',   ar: 'تحديث كتلة محتوى' },
  'ContentBlock.Deleted':   { en: 'Content Block Deleted',   ar: 'حذف كتلة محتوى' },
  'ContentBlock.Reordered': { en: 'Content Block Reordered', ar: 'إعادة ترتيب كتلة محتوى' },

  // P7-03 Skill
  'Skill.Created':    { en: 'Skill Created',    ar: 'إنشاء مهارة' },
  'Skill.Updated':    { en: 'Skill Updated',    ar: 'تحديث مهارة' },
  'Skill.Deleted':    { en: 'Skill Deleted',    ar: 'حذف مهارة' },
  'Skill.Activated':  { en: 'Skill Activated',  ar: 'تفعيل مهارة' },
  'Skill.Deactivated': { en: 'Skill Deactivated', ar: 'إلغاء تفعيل مهارة' },

  // P7-03 KnowledgeEdge
  'KnowledgeEdge.Added':   { en: 'Knowledge Edge Added',   ar: 'إضافة ارتباط معرفي' },
  'KnowledgeEdge.Removed': { en: 'Knowledge Edge Removed', ar: 'حذف ارتباط معرفي' },

  // P7-04 QuizQuestion
  'QuizQuestion.Added':       { en: 'Quiz Question Added',       ar: 'إضافة سؤال اختبار' },
  'QuizQuestion.Updated':     { en: 'Quiz Question Updated',     ar: 'تحديث سؤال اختبار' },
  'QuizQuestion.Deleted':     { en: 'Quiz Question Deleted',     ar: 'حذف سؤال اختبار' },
  'QuizQuestion.Reordered':   { en: 'Quiz Question Reordered',   ar: 'إعادة ترتيب سؤال' },
  'QuizQuestion.Activated':   { en: 'Quiz Question Activated',   ar: 'تفعيل سؤال اختبار' },
  'QuizQuestion.Deactivated': { en: 'Quiz Question Deactivated', ar: 'إلغاء تفعيل سؤال' },

  // P7-05 Content Lifecycle
  'Content.Published':   { en: 'Content Published',   ar: 'نشر المحتوى' },
  'Content.Archived':    { en: 'Content Archived',    ar: 'أرشفة المحتوى' },
  'Content.Unpublished': { en: 'Content Unpublished', ar: 'إلغاء نشر المحتوى' },
  'Content.RolledBack':  { en: 'Content Rolled Back', ar: 'التراجع عن المحتوى' },

  // P7-06 User Search & Inspect
  'User.Searched': { en: 'User Searched', ar: 'بحث عن مستخدم' },
  'User.Viewed':   { en: 'User Viewed',   ar: 'عرض مستخدم' },

  // P7-07 Account Lifecycle
  'Account.Suspended':   { en: 'Account Suspended',   ar: 'إيقاف حساب' },
  'Account.Reactivated': { en: 'Account Reactivated', ar: 'إعادة تفعيل حساب' },
  'Account.Deleted':     { en: 'Account Deleted',     ar: 'حذف حساب' },

  // P7-08 Child Profile & Grade Override
  'Child.ProfileUpdated':          { en: 'Child Profile Updated',          ar: 'تحديث ملف الطالب' },
  'Child.GradeOverridden':         { en: 'Child Grade Overridden',         ar: 'تجاوز صف الطالب' },
  'Child.LearningLanguageChanged': { en: 'Child Learning Language Changed', ar: 'تغيير لغة الدراسة' },

  // P7-13 Gamification Admin Overrides
  'Gamification.LeagueTierOverridden': { en: 'League Tier Overridden', ar: 'تجاوز مستوى الدوري' },
  'Gamification.StreakFreezeGranted':  { en: 'Streak Freeze Granted',  ar: 'منح تجميد السلسلة' },

  'Badge.Created':     { en: 'Badge Created',     ar: 'إنشاء شارة' },
  'Badge.Updated':     { en: 'Badge Updated',     ar: 'تحديث شارة' },
  'Badge.Activated':   { en: 'Badge Activated',   ar: 'تفعيل شارة' },
  'Badge.Deactivated': { en: 'Badge Deactivated', ar: 'إلغاء تفعيل شارة' },

  'Mission.Created':     { en: 'Mission Created',     ar: 'إنشاء مهمة' },
  'Mission.Updated':     { en: 'Mission Updated',     ar: 'تحديث مهمة' },
  'Mission.Activated':   { en: 'Mission Activated',   ar: 'تفعيل مهمة' },
  'Mission.Deactivated': { en: 'Mission Deactivated', ar: 'إلغاء تفعيل مهمة' },

  'TimedEvent.Created':   { en: 'Timed Event Created',   ar: 'إنشاء حدث مؤقت' },
  'TimedEvent.Updated':   { en: 'Timed Event Updated',   ar: 'تحديث حدث مؤقت' },
  'TimedEvent.Activated': { en: 'Timed Event Activated', ar: 'تفعيل حدث مؤقت' },
  'TimedEvent.Expired':   { en: 'Timed Event Expired',   ar: 'انتهاء حدث مؤقت' },

  // P10-12 Global Settings
  'GlobalSetting.Updated': { en: 'Global Setting Updated', ar: 'تحديث إعداد عام' },

  // P7-09 Moderation Queue
  'ModerationItem.Reviewed': { en: 'Moderation Item Reviewed', ar: 'مراجعة عنصر إشراف' },
};

// ── Target entity type → localized label map ──────────────────────────────────

/**
 * Map of target entity type string → { en, ar } display labels.
 * Wire values are exact strings stored in the audit log `targetEntityType` column.
 */
export const TARGET_TYPE_LABELS: Record<string, { en: string; ar: string }> = {
  Subject:        { en: 'Subject',         ar: 'موضوع' },
  Unit:           { en: 'Unit',            ar: 'وحدة' },
  Lesson:         { en: 'Lesson',          ar: 'درس' },
  ContentBlock:   { en: 'Content Block',   ar: 'كتلة محتوى' },
  Skill:          { en: 'Skill',           ar: 'مهارة' },
  KnowledgeEdge:  { en: 'Knowledge Edge',  ar: 'ارتباط معرفي' },
  QuizQuestion:   { en: 'Quiz Question',   ar: 'سؤال اختبار' },
  Content:        { en: 'Content',         ar: 'محتوى' },
  User:           { en: 'User',            ar: 'مستخدم' },
  Account:        { en: 'Account',         ar: 'حساب' },
  Child:          { en: 'Child',           ar: 'طالب' },
  Gamification:   { en: 'Gamification',    ar: 'مكافآت' },
  Badge:          { en: 'Badge',           ar: 'شارة' },
  Mission:        { en: 'Mission',         ar: 'مهمة' },
  TimedEvent:     { en: 'Timed Event',     ar: 'حدث مؤقت' },
  GlobalSetting:  { en: 'Global Setting',  ar: 'إعداد عام' },
  ModerationItem: { en: 'Moderation Item', ar: 'عنصر إشراف' },
};

// ── Domain groupings for ActionTypeBadge color keying ─────────────────────────

/**
 * Action-type groups for the `<optgroup>` elements in the action-type select.
 * Each group has a localized label and an array of action constant strings.
 */
export interface ActionGroup {
  domain: string;
  label: { en: string; ar: string };
  actions: string[];
}

export const ACTION_GROUPS: ActionGroup[] = [
  {
    domain: 'ContentLifecycle',
    label: { en: 'Content Lifecycle', ar: 'دورة حياة المحتوى' },
    actions: [
      'Subject.Created', 'Subject.Updated', 'Subject.Deleted', 'Subject.Reordered',
      'Subject.Activated', 'Subject.Deactivated',
      'Unit.Created', 'Unit.Updated', 'Unit.Deleted', 'Unit.Reordered',
      'Unit.Activated', 'Unit.Deactivated',
      'Lesson.Created', 'Lesson.Updated', 'Lesson.Deleted', 'Lesson.Reordered',
      'Lesson.Activated', 'Lesson.Deactivated',
      'ContentBlock.Added', 'ContentBlock.Updated', 'ContentBlock.Deleted', 'ContentBlock.Reordered',
      'Skill.Created', 'Skill.Updated', 'Skill.Deleted', 'Skill.Activated', 'Skill.Deactivated',
      'KnowledgeEdge.Added', 'KnowledgeEdge.Removed',
      'QuizQuestion.Added', 'QuizQuestion.Updated', 'QuizQuestion.Deleted',
      'QuizQuestion.Reordered', 'QuizQuestion.Activated', 'QuizQuestion.Deactivated',
      'Content.Published', 'Content.Archived', 'Content.Unpublished', 'Content.RolledBack',
    ],
  },
  {
    domain: 'Account',
    label: { en: 'Account', ar: 'الحساب' },
    actions: [
      'User.Searched', 'User.Viewed',
      'Account.Suspended', 'Account.Reactivated', 'Account.Deleted',
    ],
  },
  {
    domain: 'Child',
    label: { en: 'Child', ar: 'الطالب' },
    actions: [
      'Child.ProfileUpdated', 'Child.GradeOverridden', 'Child.LearningLanguageChanged',
    ],
  },
  {
    domain: 'Gamification',
    label: { en: 'Gamification', ar: 'نظام المكافآت' },
    actions: [
      'Gamification.LeagueTierOverridden', 'Gamification.StreakFreezeGranted',
      'Badge.Created', 'Badge.Updated', 'Badge.Activated', 'Badge.Deactivated',
      'Mission.Created', 'Mission.Updated', 'Mission.Activated', 'Mission.Deactivated',
      'TimedEvent.Created', 'TimedEvent.Updated', 'TimedEvent.Activated', 'TimedEvent.Expired',
    ],
  },
  {
    domain: 'System',
    label: { en: 'System', ar: 'النظام' },
    actions: [
      'GlobalSetting.Updated',
      'ModerationItem.Reviewed',
    ],
  },
];

// ── Helper functions ──────────────────────────────────────────────────────────

/**
 * Get the localized label for an action-type constant.
 * Falls back to the raw action string for any unregistered future constant.
 */
export function getActionLabel(action: string, locale: Locale): string {
  return ACTION_LABELS[action]?.[locale] ?? action;
}

/**
 * Get the localized label for a target entity type string.
 * Falls back to the raw type string for unregistered future entity types.
 */
export function getTargetTypeLabel(type: string, locale: Locale): string {
  return TARGET_TYPE_LABELS[type]?.[locale] ?? type;
}

/**
 * Extract the domain prefix from an action string for badge color keying.
 * Examples:
 *   "Subject.Created"    → "Subject"
 *   "Account.Suspended"  → "Account"
 *   "Badge.Created"      → "Badge"
 *   "UnknownFuture"      → "UnknownFuture" (no dot → returns the whole string)
 */
export function getActionDomain(action: string): string {
  return action.split('.')[0] ?? action;
}
