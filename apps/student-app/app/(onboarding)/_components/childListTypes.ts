/**
 * In-memory child draft used by the Add-Child list editor before submission.
 * Carries the form values plus a client id and per-child submit status.
 */
import type { AddChildFormValues } from '@learnexia/shared';

export type ChildSubmitStatus = 'idle' | 'pending' | 'success' | 'error';

export interface ChildDraft {
  /** Client-only id for list keys + edit/remove targeting. */
  localId: string;
  values: AddChildFormValues;
  status: ChildSubmitStatus;
  /** i18n key for a per-child error (status === 'error'). */
  errorKey?: string;
}

let counter = 0;
export function nextLocalId(): string {
  counter += 1;
  return `child-${counter}-${Date.now()}`;
}
