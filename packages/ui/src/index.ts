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

export { ChildCard } from './components/ChildCard';
export type {
  ChildCardProps,
  ChildCardVariant,
  ChildCardChild,
} from './components/ChildCard';

// --- P1-11 parent-dashboard primitives ----------------------------------
export { Avatar } from './components/Avatar';
export type { AvatarProps, AvatarSize, AvatarColor } from './components/Avatar';

export { KPIStatCard } from './components/KPIStatCard';
export type { KPIStatCardProps, KPIStatVariant } from './components/KPIStatCard';

export { MasteryBar } from './components/MasteryBar';
export type { MasteryBarProps } from './components/MasteryBar';

export { GradientBox } from './components/GradientBox';
export type { GradientBoxProps } from './components/GradientBox';
