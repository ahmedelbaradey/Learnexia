/**
 * @learnexia/ui — universal Tamagui component library.
 *
 * All components consume tokens from `@learnexia/design-system` (no raw hex/px),
 * use logical RTL props, and meet the kid-accessibility baseline documented in
 * `src/accessibility.md`.
 */
export { Button } from './components/Button';
export type { ButtonProps, ButtonVariant, ButtonSize } from './components/Button';

export { Card } from './components/Card';
export type { CardProps, CardVariant } from './components/Card';

export { XPBar } from './components/XPBar';
export type { XPBarProps } from './components/XPBar';

export { Hearts } from './components/Hearts';
export type { HeartsProps } from './components/Hearts';

export { StreakFlame } from './components/StreakFlame';
export type { StreakFlameProps } from './components/StreakFlame';

export { Badge } from './components/Badge';
export type { BadgeProps, BadgeVariant } from './components/Badge';

export { AITutorBubble } from './components/AITutorBubble';
export type { AITutorBubbleProps, AITutorVariant } from './components/AITutorBubble';

export { RewardPopup } from './components/RewardPopup';
export type { RewardPopupProps, RewardVariant } from './components/RewardPopup';

// --- P1-09 form + onboarding primitives ---------------------------------
export { TextField, FormField } from './components/TextField';
export type { TextFieldProps } from './components/TextField';

export { Select, GradePicker, LanguageSelect } from './components/Select';
export type {
  SelectProps,
  SelectOption,
  SelectValue,
  GradePickerProps,
  LanguageSelectProps,
} from './components/Select';

export { ProgressSteps } from './components/ProgressSteps';
export type { ProgressStepsProps } from './components/ProgressSteps';

export { CheckboxField } from './components/CheckboxField';
export type { CheckboxFieldProps } from './components/CheckboxField';

export { PasswordStrengthMeter, PASSWORD_STRENGTH } from './components/PasswordStrengthMeter';
export type {
  PasswordStrengthMeterProps,
  PasswordStrength,
} from './components/PasswordStrengthMeter';

export { ChildCard } from './components/ChildCard';
export type {
  ChildCardProps,
  ChildCardVariant,
  ChildCardChild,
} from './components/ChildCard';

// --- P1-11 parent-dashboard primitives ----------------------------------
export { Avatar } from './components/Avatar';
export type { AvatarProps, AvatarSize, AvatarColor } from './components/Avatar';

export { AvatarStack } from './components/AvatarStack';
export type { AvatarStackProps, AvatarStackEntry } from './components/AvatarStack';

export { KPIStatCard } from './components/KPIStatCard';
export type { KPIStatCardProps, KPIStatVariant } from './components/KPIStatCard';

export { MasteryBar } from './components/MasteryBar';
export type { MasteryBarProps } from './components/MasteryBar';

export { GradientBox } from './components/GradientBox';
export type { GradientBoxProps } from './components/GradientBox';

export { Tabs } from './components/Tabs';
export type { TabsProps, TabItem, TabValue } from './components/Tabs';

// --- W10 / P2-12 settings tab primitives --------------------------------
export { Switch } from './components/Switch';
export type { SwitchProps } from './components/Switch';

// --- W11 learning primitives (P2-02-FE + P2-03-FE) ----------------------
export { SubjectRow } from './components/SubjectRow';
export type { SubjectRowProps, SubjectKey } from './components/SubjectRow';

export { LessonCard } from './components/LessonCard';
export type { LessonCardProps, LessonState } from './components/LessonCard';

export { SkillTreeNode } from './components/SkillTreeNode';
export type { SkillTreeNodeProps, SkillNodeState } from './components/SkillTreeNode';

export { SegmentedTabs } from './components/SegmentedTabs';
export type { SegmentedTabsProps, SegmentItem, SegmentValue } from './components/SegmentedTabs';

// --- W12 quiz primitives (P2-05-FE + P2-06-FE + P2-07-FE) ---
export { QuestionCard } from './components/QuestionCard';
export type { QuestionCardProps } from './components/QuestionCard';

export { MCQOption } from './components/MCQOption';
export type { MCQOptionProps, MCQOptionState } from './components/MCQOption';

export { TrueFalseChoice } from './components/TrueFalseChoice';
export type { TrueFalseChoiceProps } from './components/TrueFalseChoice';

export { FillInBlank } from './components/FillInBlank';
export type { FillInBlankProps, FillInBlankState } from './components/FillInBlank';

export { MatchingPanel } from './components/MatchingPanel';
export type { MatchingPanelProps } from './components/MatchingPanel';

export { AnswerFeedbackStrip } from './components/AnswerFeedbackStrip';
export type { AnswerFeedbackStripProps } from './components/AnswerFeedbackStrip';

export { AttemptSummaryCard } from './components/AttemptSummaryCard';
export type { AttemptSummaryCardProps } from './components/AttemptSummaryCard';

export { ProgressDots } from './components/ProgressDots';
export type { ProgressDotsProps } from './components/ProgressDots';

// --- W13 home dashboard primitives (P2-09-FE) ---
export { DashboardHeader } from './components/DashboardHeader';
export type { DashboardHeaderProps } from './components/DashboardHeader';

export { ContinueCard } from './components/ContinueCard';
export type { ContinueCardProps } from './components/ContinueCard';
export type { SubjectKey as ContinueCardSubjectKey } from './components/ContinueCard';

export { MissionBanner } from './components/MissionBanner';
export type { MissionBannerProps } from './components/MissionBanner';

// --- P4-08 carryover (batch 2a) — motion foundation, salvaged from the
// retired feat/P4-08-gamification-screens-motion branch (plan L5) ---
export { useReduceMotion } from './hooks/useReduceMotion';

export { useDashboardDiff, ZERO_DIFF } from './hooks/useDashboardDiff';
export type { DashboardLike, DashboardDiff } from './hooks/useDashboardDiff';

export { ConfettiLayer } from './internal/ConfettiLayer';
export type { ConfettiLayerProps, ConfettiPaletteVariant } from './internal/ConfettiLayer';
