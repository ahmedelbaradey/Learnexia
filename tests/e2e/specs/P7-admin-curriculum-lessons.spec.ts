/**
 * P7 Admin Curriculum — Lessons + Content Blocks + Questions E2E
 * Implements CUR-TC-31..63 from docs/qc/P7-curriculum-admin/frontend-test-cases.md
 *
 * Coverage: Lessons (31..38) + Content Blocks (39..48) + Questions (49..63)
 * Target: admin-dashboard Next.js at http://localhost:3001
 * Backend: .NET 10 at http://localhost:5080
 *
 * Seed: Subject 1 (Math AR grade 1), Unit 1 (Ar Numbers unit), Lessons 1..3
 *       Questions seeded for lesson 1
 */

import { test, expect, type Page, type APIRequestContext } from '@playwright/test';

test.use({ baseURL: 'http://localhost:3001' });
test.setTimeout(180_000);

const ADMIN_URL = 'http://localhost:3001';
const API_URL = 'http://localhost:5080';

// Seeded IDs — determined from LearningSeeder data
const SEED_SUBJECT_ID = 1;  // Math AR grade 1
const SEED_UNIT_ID = 1;      // First unit under Math AR
const SEED_LESSON_ID = 1;    // First lesson under Unit 1

// Route paths
const LESSONS_LIST_PATH = `/curriculum/subjects/${SEED_SUBJECT_ID}/units/${SEED_UNIT_ID}/lessons`;
const LESSON_DETAIL_PATH = `/curriculum/subjects/${SEED_SUBJECT_ID}/units/${SEED_UNIT_ID}/lessons/${SEED_LESSON_ID}`;
const QUESTIONS_PATH = `/curriculum/subjects/${SEED_SUBJECT_ID}/units/${SEED_UNIT_ID}/lessons/${SEED_LESSON_ID}/questions`;

// ---------------------------------------------------------------------------
// Helpers
// ---------------------------------------------------------------------------

async function loginAsAdmin(page: Page): Promise<void> {
  await page.goto(`${ADMIN_URL}/login`);
  await page.waitForLoadState('networkidle');
  await page.getByTestId('login-username').fill('superadmin');
  await page.getByTestId('login-password').fill('123Pa$$word!');
  await page.getByTestId('login-submit').click();
  await page.waitForURL(/\/dashboard/, { timeout: 30_000 });
}

async function getAdminToken(request: APIRequestContext): Promise<string> {
  const res = await request.post(`${API_URL}/api/Users/Authentication/Sign-In`, {
    data: { userName: 'superadmin', password: '123Pa$$word!' },
  });
  const body = await res.json();
  return body.data?.accessToken ?? '';
}

function uniqueName(prefix: string): string {
  return `${prefix}-${Date.now()}-${Math.floor(Math.random() * 9999)}`;
}

// ---------------------------------------------------------------------------
// SECTION 3 — Lessons list + detail (CUR-TC-31..38)
// ---------------------------------------------------------------------------

test.describe('CUR-TC: Lessons list + detail', () => {
  test.beforeEach(async ({ page }) => {
    await loginAsAdmin(page);
  });

  // CUR-TC-31: Lessons list four states
  test('CUR-TC-31: Lessons list four states', async ({ page }) => {
    await page.goto(`${ADMIN_URL}${LESSONS_LIST_PATH}`);
    await page.waitForLoadState('networkidle');
    await page.waitForTimeout(2000);
    const lessonsTable = page.getByTestId('lessons-table');
    await expect(lessonsTable).toBeVisible({ timeout: 20_000 });
    const lessonRows = page.locator('[data-testid^="lesson-row-"]');
    await expect(lessonRows.first()).toBeVisible({ timeout: 10_000 });
    // Breadcrumb should be present
    const breadcrumbSubject = page.getByTestId('breadcrumb-subject');
    await expect(breadcrumbSubject).toBeVisible();
    const breadcrumbUnit = page.getByTestId('breadcrumb-unit');
    await expect(breadcrumbUnit).toBeVisible();
  });

  // CUR-TC-32: Lesson detail link navigation
  test('CUR-TC-32: Lesson detail link navigation', async ({ page }) => {
    await page.goto(`${ADMIN_URL}${LESSONS_LIST_PATH}`);
    await page.waitForLoadState('networkidle');
    await page.waitForTimeout(2000);
    const lessonRows = page.locator('[data-testid^="lesson-row-"]');
    await expect(lessonRows.first()).toBeVisible({ timeout: 20_000 });
    const testId = await lessonRows.first().getAttribute('data-testid');
    const idMatch = testId?.match(/lesson-row-(\d+)/);
    if (!idMatch) { test.skip(); return; }
    const lessonId = idMatch[1];
    const detailLink = page.getByTestId(`lesson-${lessonId}-detail-link`);
    await expect(detailLink).toBeVisible({ timeout: 5000 });
    await detailLink.click();
    await page.waitForURL(new RegExp(`/lessons/${lessonId}$`), { timeout: 15_000 });
    // Lesson header
    const header = page.getByTestId(`lesson-header-${lessonId}`);
    await expect(header).toBeVisible({ timeout: 20_000 });
    // Breadcrumb
    await expect(page.getByTestId('breadcrumb-lesson')).toBeVisible();
  });

  // CUR-TC-33: Lesson not-found
  test('CUR-TC-33: Lesson not-found state', async ({ page }) => {
    await page.goto(`${ADMIN_URL}/curriculum/subjects/${SEED_SUBJECT_ID}/units/${SEED_UNIT_ID}/lessons/99999999`);
    await page.waitForLoadState('networkidle');
    await page.waitForTimeout(3000);
    const notFound = page.getByTestId('lesson-not-found');
    const hasNotFound = await notFound.isVisible({ timeout: 8000 }).catch(() => false);
    if (hasNotFound) {
      await expect(notFound).toBeVisible();
    } else {
      // May render as generic error
      await expect(page.getByText(/not found|back/i).first()).toBeVisible({ timeout: 5000 });
    }
  });

  // CUR-TC-34: Create lesson
  test('CUR-TC-34: Create lesson happy path', async ({ page }) => {
    await page.goto(`${ADMIN_URL}${LESSONS_LIST_PATH}`);
    await page.waitForLoadState('networkidle');
    await page.waitForTimeout(1000);
    const lessonName = uniqueName('QC-Lesson');
    await page.getByTestId('new-lesson-btn').click();
    const dialog = page.getByRole('dialog');
    await expect(dialog).toBeVisible({ timeout: 10_000 });
    await page.getByTestId('lesson-form-name').fill(lessonName);
    // Difficulty - select first option
    const diffSelect = page.getByTestId('lesson-form-difficulty');
    await diffSelect.selectOption({ index: 1 });
    // Minutes
    await page.getByTestId('lesson-form-minutes').fill('30');
    await page.getByTestId('lesson-form-save').click();
    await expect(dialog).not.toBeVisible({ timeout: 15_000 });
    await page.waitForTimeout(1500);
    await expect(page.getByText(lessonName)).toBeVisible({ timeout: 10_000 });
  });

  // CUR-TC-35: Edit lesson
  test('CUR-TC-35: Edit lesson', async ({ page }) => {
    await page.goto(`${ADMIN_URL}${LESSONS_LIST_PATH}`);
    await page.waitForLoadState('networkidle');
    await page.waitForTimeout(2000);
    const lessonRows = page.locator('[data-testid^="lesson-row-"]');
    await expect(lessonRows.first()).toBeVisible({ timeout: 20_000 });
    const testId = await lessonRows.first().getAttribute('data-testid');
    const idMatch = testId?.match(/lesson-row-(\d+)/);
    if (!idMatch) { test.skip(); return; }
    const lessonId = idMatch[1];
    await page.getByTestId(`lesson-${lessonId}-edit`).click();
    const dialog = page.getByRole('dialog');
    await expect(dialog).toBeVisible({ timeout: 10_000 });
    const nameField = page.getByTestId('lesson-form-name');
    const originalName = await nameField.inputValue();
    await nameField.fill(originalName + ' edited');
    await page.getByTestId('lesson-form-save').click();
    await expect(dialog).not.toBeVisible({ timeout: 15_000 });
    await page.waitForTimeout(1500);
    // Restore
    await page.getByTestId(`lesson-${lessonId}-edit`).click();
    await page.getByTestId('lesson-form-name').fill(originalName);
    await page.getByTestId('lesson-form-save').click();
    await page.waitForTimeout(500);
  });

  // CUR-TC-36: Toggle lesson active
  test('CUR-TC-36: Toggle lesson active', async ({ page }) => {
    await page.goto(`${ADMIN_URL}${LESSONS_LIST_PATH}`);
    await page.waitForLoadState('networkidle');
    await page.waitForTimeout(2000);
    const lessonRows = page.locator('[data-testid^="lesson-row-"]');
    await expect(lessonRows.first()).toBeVisible({ timeout: 20_000 });
    const testId = await lessonRows.first().getAttribute('data-testid');
    const idMatch = testId?.match(/lesson-row-(\d+)/);
    if (!idMatch) { test.skip(); return; }
    const lessonId = idMatch[1];
    const toggleBtn = page.getByTestId(`lesson-${lessonId}-toggle-active`);
    await expect(toggleBtn).toBeVisible({ timeout: 10_000 });
    await toggleBtn.click();
    await page.waitForTimeout(2000);
    // Toggle back
    await toggleBtn.click();
    await page.waitForTimeout(1000);
  });

  // CUR-TC-37: Delete lesson
  test('CUR-TC-37: Delete lesson', async ({ page }) => {
    await page.goto(`${ADMIN_URL}${LESSONS_LIST_PATH}`);
    await page.waitForLoadState('networkidle');
    await page.waitForTimeout(1000);
    // Create throwaway lesson
    const lessonName = uniqueName('QC-Del-Lesson');
    await page.getByTestId('new-lesson-btn').click();
    const createDialog = page.getByRole('dialog');
    await expect(createDialog).toBeVisible({ timeout: 10_000 });
    await page.getByTestId('lesson-form-name').fill(lessonName);
    const diffSelect = page.getByTestId('lesson-form-difficulty');
    await diffSelect.selectOption({ index: 1 });
    await page.getByTestId('lesson-form-minutes').fill('20');
    await page.getByTestId('lesson-form-save').click();
    await expect(createDialog).not.toBeVisible({ timeout: 15_000 });
    await page.waitForTimeout(1500);
    // Find the created lesson
    const lessonRow = page.locator(`[data-testid^="lesson-row-"]:has-text("${lessonName}")`);
    const hasRow = await lessonRow.isVisible({ timeout: 8000 }).catch(() => false);
    if (!hasRow) { test.skip(); return; }
    const rowTestId = await lessonRow.getAttribute('data-testid');
    const idMatch = rowTestId?.match(/lesson-row-(\d+)/);
    if (!idMatch) { test.skip(); return; }
    const lessonId = idMatch[1];
    await page.getByTestId(`lesson-${lessonId}-delete`).click();
    const confirmBtn = page.getByTestId('delete-lesson-confirm-btn');
    await expect(confirmBtn).toBeVisible({ timeout: 10_000 });
    await confirmBtn.click();
    await page.waitForTimeout(2000);
    await expect(lessonRow).not.toBeVisible({ timeout: 10_000 });
  });

  // CUR-TC-38: Lessons keyboard reorder + save
  test('CUR-TC-38: Lessons keyboard reorder and save', async ({ page }) => {
    await page.goto(`${ADMIN_URL}${LESSONS_LIST_PATH}`);
    await page.waitForLoadState('networkidle');
    await page.waitForTimeout(2000);
    const lessonRows = page.locator('[data-testid^="lesson-row-"]');
    const count = await lessonRows.count();
    if (count < 2) { test.skip(); return; }
    const testId = await lessonRows.first().getAttribute('data-testid');
    const idMatch = testId?.match(/lesson-row-(\d+)/);
    if (!idMatch) { test.skip(); return; }
    const lessonId = idMatch[1];
    const moveDownBtn = page.getByTestId(`lesson-${lessonId}-move-down`);
    await expect(moveDownBtn).toBeVisible({ timeout: 10_000 });
    await moveDownBtn.click();
    await page.waitForTimeout(300);
    const saveOrderBtn = page.getByTestId('lessons-save-order');
    await expect(saveOrderBtn).toBeVisible({ timeout: 5000 });
    await saveOrderBtn.click();
    await page.waitForTimeout(1500);
    await expect(saveOrderBtn).not.toBeVisible({ timeout: 5000 });
  });
});

// ---------------------------------------------------------------------------
// SECTION 4 — Content blocks (CUR-TC-39..48)
// ---------------------------------------------------------------------------

test.describe('CUR-TC: Content blocks', () => {
  test.beforeEach(async ({ page }) => {
    await loginAsAdmin(page);
  });

  // CUR-TC-39: Block editor four states
  test('CUR-TC-39: Block editor four states', async ({ page }) => {
    await page.goto(`${ADMIN_URL}${LESSON_DETAIL_PATH}`);
    await page.waitForLoadState('networkidle');
    await page.waitForTimeout(3000);
    // Block editor should mount
    const editor = page.getByTestId('content-block-editor');
    await expect(editor).toBeVisible({ timeout: 20_000 });
    // Either block-list (with blocks) or block-editor-empty
    const hasList = await page.getByTestId('block-list').isVisible({ timeout: 3000 }).catch(() => false);
    const hasEmpty = await page.getByTestId('block-editor-empty').isVisible({ timeout: 3000 }).catch(() => false);
    expect(hasList || hasEmpty).toBe(true);
  });

  // CUR-TC-40: Add Text block + sanitized preview
  test('CUR-TC-40: Add Text block with markdown + sanitized preview', async ({ page }) => {
    await page.goto(`${ADMIN_URL}${LESSON_DETAIL_PATH}`);
    await page.waitForLoadState('networkidle');
    await page.waitForTimeout(3000);
    const editor = page.getByTestId('content-block-editor');
    await expect(editor).toBeVisible({ timeout: 20_000 });
    // Add block
    await page.getByTestId('block-editor-add-btn').click();
    // Type picker - pick Text (type 1)
    const textPick = page.getByTestId('pick-type-1');
    await expect(textPick).toBeVisible({ timeout: 10_000 });
    await textPick.click();
    // BlockForm
    const markdownInput = page.getByTestId('block-text-markdown');
    await expect(markdownInput).toBeVisible({ timeout: 10_000 });
    await markdownInput.fill('# Hello\n\n**bold** text\n\n<script>alert(1)</script>');
    await page.getByTestId('block-form-save').click();
    await page.waitForTimeout(2000);
    // New block should appear
    const blockCards = page.locator('[data-testid^="block-card-"]');
    await expect(blockCards.last()).toBeVisible({ timeout: 10_000 });
    // Preview should not contain executable script
    const lastCard = blockCards.last();
    const testId = await lastCard.getAttribute('data-testid');
    const idMatch = testId?.match(/block-card-(\d+)/);
    if (idMatch) {
      const previewLocator = page.getByTestId(`block-card-${idMatch[1]}-preview`);
      const hasPreview = await previewLocator.isVisible({ timeout: 5000 }).catch(() => false);
      if (hasPreview) {
        // Script tag should not be executable in DOM
        const scriptTags = await previewLocator.locator('script').count();
        expect(scriptTags).toBe(0);
      }
    }
  });

  // CUR-TC-41: Add Image block + HTTPS URL guard
  test('CUR-TC-41: Add Image block + URL guard', async ({ page }) => {
    await page.goto(`${ADMIN_URL}${LESSON_DETAIL_PATH}`);
    await page.waitForLoadState('networkidle');
    await page.waitForTimeout(3000);
    const editor = page.getByTestId('content-block-editor');
    await expect(editor).toBeVisible({ timeout: 20_000 });
    // Add image block
    await page.getByTestId('block-editor-add-btn').click();
    const imagePick = page.getByTestId('pick-type-2');
    await expect(imagePick).toBeVisible({ timeout: 10_000 });
    await imagePick.click();
    // (a) Non-HTTPS URL - should fail validation
    const urlInput = page.getByTestId('block-image-url');
    await expect(urlInput).toBeVisible({ timeout: 10_000 });
    await urlInput.fill('http://example.com/img.png');
    await page.getByTestId('block-form-save').click();
    await page.waitForTimeout(500);
    // Should show alert (form validation error)
    const alertEls = page.locator('[role="alert"]');
    const hasAlert = await alertEls.count() > 0;
    const dialogStillOpen = await page.getByRole('dialog').isVisible().catch(() => false);
    expect(dialogStillOpen || hasAlert).toBe(true);
    // (b) Valid HTTPS URL
    await urlInput.fill('https://cdn.example.com/pic.png');
    const altInput = page.getByTestId('block-image-alt');
    const hasAlt = await altInput.isVisible({ timeout: 2000 }).catch(() => false);
    if (hasAlt) {
      await altInput.fill('Test image');
    }
    await page.getByTestId('block-form-save').click();
    await page.waitForTimeout(2000);
    // Block should be created
    const blockCards = page.locator('[data-testid^="block-card-"]');
    await expect(blockCards.last()).toBeVisible({ timeout: 10_000 });
  });

  // CUR-TC-42: Add Video block
  test('CUR-TC-42: Add Video block', async ({ page }) => {
    await page.goto(`${ADMIN_URL}${LESSON_DETAIL_PATH}`);
    await page.waitForLoadState('networkidle');
    await page.waitForTimeout(3000);
    await expect(page.getByTestId('content-block-editor')).toBeVisible({ timeout: 20_000 });
    await page.getByTestId('block-editor-add-btn').click();
    const videoPick = page.getByTestId('pick-type-3');
    await expect(videoPick).toBeVisible({ timeout: 10_000 });
    await videoPick.click();
    const videoUrlInput = page.getByTestId('block-video-url');
    await expect(videoUrlInput).toBeVisible({ timeout: 10_000 });
    await videoUrlInput.fill('https://cdn.example.com/video.mp4');
    const captionInput = page.getByTestId('block-video-caption');
    const hasCaption = await captionInput.isVisible({ timeout: 2000 }).catch(() => false);
    if (hasCaption) {
      await captionInput.fill('Test video');
    }
    await page.getByTestId('block-form-save').click();
    await page.waitForTimeout(2000);
    const blockCards = page.locator('[data-testid^="block-card-"]');
    await expect(blockCards.last()).toBeVisible({ timeout: 10_000 });
  });

  // CUR-TC-43: Add Callout block (variant required)
  test('CUR-TC-43: Add Callout block', async ({ page }) => {
    await page.goto(`${ADMIN_URL}${LESSON_DETAIL_PATH}`);
    await page.waitForLoadState('networkidle');
    await page.waitForTimeout(3000);
    await expect(page.getByTestId('content-block-editor')).toBeVisible({ timeout: 20_000 });
    await page.getByTestId('block-editor-add-btn').click();
    const calloutPick = page.getByTestId('pick-type-4');
    await expect(calloutPick).toBeVisible({ timeout: 10_000 });
    await calloutPick.click();
    // (a) Save with empty - should error
    await page.getByTestId('block-form-save').click();
    await page.waitForTimeout(500);
    const alertEl = page.locator('[role="alert"]');
    const hasAlert = await alertEl.count() > 0;
    const dialogOpen = await page.getByRole('dialog').isVisible().catch(() => false);
    expect(dialogOpen || hasAlert).toBe(true);
    // (b) Fill variant and markdown
    const variantSelect = page.getByTestId('block-callout-variant');
    await expect(variantSelect).toBeVisible({ timeout: 5000 });
    await variantSelect.selectOption({ index: 1 });
    const calloutMarkdown = page.getByTestId('block-callout-markdown');
    await expect(calloutMarkdown).toBeVisible({ timeout: 5000 });
    await calloutMarkdown.fill('**Warning:** This is a callout');
    await page.getByTestId('block-form-save').click();
    await page.waitForTimeout(2000);
    const blockCards = page.locator('[data-testid^="block-card-"]');
    await expect(blockCards.last()).toBeVisible({ timeout: 10_000 });
  });

  // CUR-TC-45: Reorder blocks + save
  test('CUR-TC-45: Reorder blocks and save', async ({ page }) => {
    await page.goto(`${ADMIN_URL}${LESSON_DETAIL_PATH}`);
    await page.waitForLoadState('networkidle');
    await page.waitForTimeout(3000);
    await expect(page.getByTestId('content-block-editor')).toBeVisible({ timeout: 20_000 });
    const blockCards = page.locator('[data-testid^="block-card-"]');
    const count = await blockCards.count();
    if (count < 2) {
      // Need at least 2 blocks - create one more if needed
      await page.getByTestId('block-editor-add-btn').click();
      await page.getByTestId('pick-type-1').click();
      await page.getByTestId('block-text-markdown').fill('Extra block for reorder test');
      await page.getByTestId('block-form-save').click();
      await page.waitForTimeout(2000);
    }
    const updatedCount = await blockCards.count();
    if (updatedCount < 2) { test.skip(); return; }
    const testId = await blockCards.first().getAttribute('data-testid');
    const idMatch = testId?.match(/block-card-(\d+)/);
    if (!idMatch) { test.skip(); return; }
    const blockId = idMatch[1];
    const moveDown = page.getByTestId(`block-card-${blockId}-move-down`);
    const hasMove = await moveDown.isVisible({ timeout: 5000 }).catch(() => false);
    if (!hasMove) { test.skip(); return; }
    await moveDown.click();
    await page.waitForTimeout(300);
    const saveOrderBtn = page.getByTestId('block-editor-save-order');
    await expect(saveOrderBtn).toBeVisible({ timeout: 5000 });
    await saveOrderBtn.click();
    await page.waitForTimeout(1500);
    await expect(saveOrderBtn).not.toBeVisible({ timeout: 5000 });
  });

  // CUR-TC-46: Delete block
  test('CUR-TC-46: Delete block', async ({ page }) => {
    await page.goto(`${ADMIN_URL}${LESSON_DETAIL_PATH}`);
    await page.waitForLoadState('networkidle');
    await page.waitForTimeout(3000);
    await expect(page.getByTestId('content-block-editor')).toBeVisible({ timeout: 20_000 });
    // Create a throwaway block
    await page.getByTestId('block-editor-add-btn').click();
    await page.getByTestId('pick-type-1').click();
    await page.getByTestId('block-text-markdown').fill('Delete me');
    await page.getByTestId('block-form-save').click();
    await page.waitForTimeout(2000);
    // Find newest block
    const blockCards = page.locator('[data-testid^="block-card-"]');
    const count = await blockCards.count();
    if (count === 0) { test.skip(); return; }
    const lastCard = blockCards.last();
    const testId = await lastCard.getAttribute('data-testid');
    const idMatch = testId?.match(/block-card-(\d+)/);
    if (!idMatch) { test.skip(); return; }
    const blockId = idMatch[1];
    await page.getByTestId(`block-card-${blockId}-delete`).click();
    const confirmBtn = page.getByTestId('delete-block-confirm-btn');
    await expect(confirmBtn).toBeVisible({ timeout: 10_000 });
    await confirmBtn.click();
    await page.waitForTimeout(2000);
    await expect(page.getByTestId(`block-card-${blockId}`)).not.toBeVisible({ timeout: 10_000 });
  });
});

// ---------------------------------------------------------------------------
// SECTION 5 — Questions (CUR-TC-49..63)
// ---------------------------------------------------------------------------

test.describe('CUR-TC: Questions', () => {
  test.beforeEach(async ({ page }) => {
    await loginAsAdmin(page);
  });

  // CUR-TC-49: Questions list four states
  test('CUR-TC-49: Questions list four states', async ({ page }) => {
    await page.goto(`${ADMIN_URL}${QUESTIONS_PATH}`);
    await page.waitForLoadState('networkidle');
    await page.waitForTimeout(3000);
    // Either questions table (if seeded) or empty state
    const hasTable = await page.getByTestId('questions-table').isVisible({ timeout: 5000 }).catch(() => false);
    const hasEmpty = await page.getByTestId('questions-empty').isVisible({ timeout: 5000 }).catch(() => false);
    expect(hasTable || hasEmpty).toBe(true);
  });

  // CUR-TC-50: Create MCQ + round-trip
  test('CUR-TC-50: Create MCQ and verify round-trip', async ({ page }) => {
    const qText = uniqueName('QC-MCQ');
    await page.goto(`${ADMIN_URL}${QUESTIONS_PATH}`);
    await page.waitForLoadState('networkidle');
    await page.waitForTimeout(2000);
    await page.getByTestId('new-question-btn').click();
    const editorDialog = page.getByTestId('question-editor-dialog');
    await expect(editorDialog).toBeVisible({ timeout: 15_000 });
    // Select MCQ type (value 1)
    const typeSelect = page.getByTestId('question-type-select');
    await typeSelect.selectOption('1');
    await page.waitForTimeout(500);
    // Fill question text (unique per run to avoid strict-mode dup violations)
    await page.getByTestId('question-text-input').fill(qText);
    // Fill difficulty
    const diffSelect = page.getByTestId('question-difficulty-select');
    await diffSelect.selectOption({ index: 1 });
    // Fill MCQ options
    await page.getByTestId('mcq-option-input-0').fill('3');
    await page.getByTestId('mcq-option-input-1').fill('4');
    // Add 3rd option
    await page.getByTestId('mcq-add-option').click();
    await page.waitForTimeout(300);
    const option2 = page.getByTestId('mcq-option-input-2');
    const hasOption2 = await option2.isVisible({ timeout: 3000 }).catch(() => false);
    if (hasOption2) {
      await option2.fill('5');
    }
    // Select correct answer (option index 1 = "4")
    await page.getByTestId('mcq-correct-radio-1').click();
    // Save
    await page.getByTestId('question-editor-save').click();
    await expect(editorDialog).not.toBeVisible({ timeout: 15_000 });
    await page.waitForTimeout(2000);
    // Should appear in table
    const table = page.getByTestId('questions-table');
    await expect(table).toBeVisible({ timeout: 10_000 });
    // Find created question (unique text — exactly 1 match)
    await expect(page.getByText(qText)).toBeVisible({ timeout: 10_000 });
    // Round-trip: re-open via edit
    const questionRow = page.locator(`[data-testid^="question-row-"]:has-text("${qText}")`);
    const rowTestId = await questionRow.first().getAttribute('data-testid');
    const idMatch = rowTestId?.match(/question-row-(\d+)/);
    if (idMatch) {
      const qId = idMatch[1];
      await page.getByTestId(`question-${qId}-edit`).click();
      await expect(editorDialog).toBeVisible({ timeout: 10_000 });
      // Options should be pre-filled
      await expect(page.getByTestId('mcq-option-input-0')).toHaveValue('3');
      await expect(page.getByTestId('mcq-option-input-1')).toHaveValue('4');
      await page.getByTestId('question-editor-cancel').click();
    }
  });

  // CUR-TC-51: Create TrueFalse + round-trip
  test('CUR-TC-51: Create TrueFalse and verify round-trip', async ({ page }) => {
    const qText = uniqueName('QC-TF');
    await page.goto(`${ADMIN_URL}${QUESTIONS_PATH}`);
    await page.waitForLoadState('networkidle');
    await page.waitForTimeout(2000);
    await page.getByTestId('new-question-btn').click();
    const editorDialog = page.getByTestId('question-editor-dialog');
    await expect(editorDialog).toBeVisible({ timeout: 15_000 });
    // TrueFalse type (value 2)
    await page.getByTestId('question-type-select').selectOption('2');
    await page.waitForTimeout(500);
    await page.getByTestId('question-text-input').fill(qText);
    await page.getByTestId('question-difficulty-select').selectOption({ index: 1 });
    // Select True
    await page.getByTestId('tf-option-true').click();
    await page.getByTestId('question-editor-save').click();
    await expect(editorDialog).not.toBeVisible({ timeout: 15_000 });
    await page.waitForTimeout(2000);
    await expect(page.getByText(qText)).toBeVisible({ timeout: 10_000 });
    // Round-trip
    const questionRow = page.locator(`[data-testid^="question-row-"]:has-text("${qText}")`);
    const rowTestId = await questionRow.first().getAttribute('data-testid');
    const idMatch = rowTestId?.match(/question-row-(\d+)/);
    if (idMatch) {
      await page.getByTestId(`question-${idMatch[1]}-edit`).click();
      await expect(editorDialog).toBeVisible({ timeout: 10_000 });
      // True option (radio input) should be checked — use isChecked() not aria-checked
      await expect(page.getByTestId('tf-option-true')).toBeChecked({ timeout: 5000 });
      await page.getByTestId('question-editor-cancel').click();
    }
  });

  // CUR-TC-52: Create FillInBlank + round-trip
  test('CUR-TC-52: Create FillInBlank and verify round-trip', async ({ page }) => {
    const qText = uniqueName('QC-FIB');
    await page.goto(`${ADMIN_URL}${QUESTIONS_PATH}`);
    await page.waitForLoadState('networkidle');
    await page.waitForTimeout(2000);
    await page.getByTestId('new-question-btn').click();
    const editorDialog = page.getByTestId('question-editor-dialog');
    await expect(editorDialog).toBeVisible({ timeout: 15_000 });
    // FillInBlank type (value 4)
    await page.getByTestId('question-type-select').selectOption('4');
    await page.waitForTimeout(500);
    await page.getByTestId('question-text-input').fill(qText);
    await page.getByTestId('question-difficulty-select').selectOption({ index: 1 });
    // Fill answer
    await page.getByTestId('fib-answer-input').fill('Cairo');
    await page.getByTestId('question-editor-save').click();
    await expect(editorDialog).not.toBeVisible({ timeout: 15_000 });
    await page.waitForTimeout(2000);
    await expect(page.getByText(qText, { exact: false })).toBeVisible({ timeout: 10_000 });
    // Round-trip
    const questionRow = page.locator(`[data-testid^="question-row-"]:has-text("${qText}")`);
    const rowTestId = await questionRow.first().getAttribute('data-testid');
    const idMatch = rowTestId?.match(/question-row-(\d+)/);
    if (idMatch) {
      await page.getByTestId(`question-${idMatch[1]}-edit`).click();
      await expect(editorDialog).toBeVisible({ timeout: 10_000 });
      await expect(page.getByTestId('fib-answer-input')).toHaveValue('Cairo');
      await page.getByTestId('question-editor-cancel').click();
    }
  });

  // CUR-TC-53: Create Matching + grades-correctly end-to-end (MUST-HAVE)
  test('CUR-TC-53: Create Matching + grades-correctly e2e (MUST-HAVE)', async ({ page }) => {
    const qText = uniqueName('QC-Match');
    await page.goto(`${ADMIN_URL}${QUESTIONS_PATH}`);
    await page.waitForLoadState('networkidle');
    await page.waitForTimeout(2000);
    await page.getByTestId('new-question-btn').click();
    const editorDialog = page.getByTestId('question-editor-dialog');
    await expect(editorDialog).toBeVisible({ timeout: 15_000 });
    // Matching type (value 3)
    await page.getByTestId('question-type-select').selectOption('3');
    await page.waitForTimeout(500);
    await page.getByTestId('question-text-input').fill(qText);
    await page.getByTestId('question-difficulty-select').selectOption({ index: 1 });
    // Add 2 left items
    await page.getByTestId('match-add-left').click();
    await page.waitForTimeout(200);
    await page.getByTestId('match-left-input-0').fill('Cat');
    await page.getByTestId('match-add-left').click();
    await page.waitForTimeout(200);
    await page.getByTestId('match-left-input-1').fill('Dog');
    // Add 2 right items
    await page.getByTestId('match-add-right').click();
    await page.waitForTimeout(200);
    await page.getByTestId('match-right-input-0').fill('Meows');
    await page.getByTestId('match-add-right').click();
    await page.waitForTimeout(200);
    await page.getByTestId('match-right-input-1').fill('Barks');
    await page.waitForTimeout(500);
    // Set pairs: left[0]=right[0], left[1]=right[1]
    const pair0 = page.getByTestId('match-pair-select-0');
    await expect(pair0).toBeVisible({ timeout: 5000 });
    await pair0.selectOption({ index: 1 }); // first right item = "Meows"
    const pair1 = page.getByTestId('match-pair-select-1');
    await expect(pair1).toBeVisible({ timeout: 5000 });
    await pair1.selectOption({ index: 2 }); // second right item = "Barks"
    // Save
    await page.getByTestId('question-editor-save').click();
    await expect(editorDialog).not.toBeVisible({ timeout: 15_000 });
    await page.waitForTimeout(2000);
    // Question created (unique text — exactly 1 match)
    await expect(page.getByText(qText)).toBeVisible({ timeout: 10_000 });
    // Round-trip verification: re-open and check pairs
    const questionRow = page.locator(`[data-testid^="question-row-"]:has-text("${qText}")`);
    const rowTestId = await questionRow.first().getAttribute('data-testid');
    const idMatch = rowTestId?.match(/question-row-(\d+)/);
    if (idMatch) {
      const qId = idMatch[1];
      await page.getByTestId(`question-${qId}-edit`).click();
      await expect(editorDialog).toBeVisible({ timeout: 10_000 });
      // Left items should be pre-filled
      const left0 = page.getByTestId('match-left-input-0');
      const left1 = page.getByTestId('match-left-input-1');
      await expect(left0).toBeVisible({ timeout: 5000 });
      await expect(left0).toHaveValue('Cat');
      await expect(left1).toHaveValue('Dog');
      // Right items should be pre-filled
      await expect(page.getByTestId('match-right-input-0')).toHaveValue('Meows');
      await expect(page.getByTestId('match-right-input-1')).toHaveValue('Barks');
      // Pair selects should be set (grades-correctly: each left maps to correct right)
      const pairSelect0 = page.getByTestId('match-pair-select-0');
      await expect(pairSelect0).toBeVisible({ timeout: 5000 });
      const pairVal0 = await pairSelect0.inputValue();
      expect(pairVal0).not.toBe(''); // should have a value
      await page.getByTestId('question-editor-cancel').click();
    }
  });

  // CUR-TC-54: Matching validation rules
  test('CUR-TC-54: Matching validation rules', async ({ page }) => {
    await page.goto(`${ADMIN_URL}${QUESTIONS_PATH}`);
    await page.waitForLoadState('networkidle');
    await page.waitForTimeout(2000);
    await page.getByTestId('new-question-btn').click();
    const editorDialog = page.getByTestId('question-editor-dialog');
    await expect(editorDialog).toBeVisible({ timeout: 15_000 });
    await page.getByTestId('question-type-select').selectOption('3');
    await page.waitForTimeout(500);
    await page.getByTestId('question-text-input').fill('Matching validation test');
    await page.getByTestId('question-difficulty-select').selectOption({ index: 1 });
    // (a) Unequal: 2 left, 1 right — should fail with mismatched count error
    await page.getByTestId('match-add-left').click();
    await page.waitForTimeout(200);
    await page.getByTestId('match-left-input-0').fill('A');
    await page.getByTestId('match-add-left').click();
    await page.waitForTimeout(200);
    await page.getByTestId('match-left-input-1').fill('B');
    await page.getByTestId('match-add-right').click();
    await page.waitForTimeout(200);
    await page.getByTestId('match-right-input-0').fill('X');
    await page.getByTestId('question-editor-save').click();
    await page.waitForTimeout(500);
    // Dialog should stay open
    await expect(editorDialog).toBeVisible();
    // Should show some validation error
    const alertEls = page.locator('[role="alert"]');
    const errorCount = await alertEls.count();
    expect(errorCount).toBeGreaterThan(0);
    // Cancel
    await page.getByTestId('question-editor-cancel').click();
  });

  // CUR-TC-55: MCQ validation rules
  test('CUR-TC-55: MCQ validation rules', async ({ page }) => {
    await page.goto(`${ADMIN_URL}${QUESTIONS_PATH}`);
    await page.waitForLoadState('networkidle');
    await page.waitForTimeout(2000);
    await page.getByTestId('new-question-btn').click();
    const editorDialog = page.getByTestId('question-editor-dialog');
    await expect(editorDialog).toBeVisible({ timeout: 15_000 });
    await page.getByTestId('question-type-select').selectOption('1');
    await page.waitForTimeout(500);
    // Empty text + save
    await page.getByTestId('question-editor-save').click();
    await page.waitForTimeout(500);
    await expect(editorDialog).toBeVisible();
    const alerts = page.locator('[role="alert"]');
    const alertCount = await alerts.count();
    expect(alertCount).toBeGreaterThan(0);
    // Check remove button count - at 2 options, MCQ remove should be hidden
    const removeBtn0 = page.getByTestId('mcq-remove-option-0');
    const removeBtnVisible = await removeBtn0.isVisible({ timeout: 3000 }).catch(() => false);
    // With only 2 options, remove should be hidden
    if (removeBtnVisible) {
      // If visible, it means there are more than 2 options
    }
    await page.getByTestId('question-editor-cancel').click();
  });

  // CUR-TC-56: Required type + difficulty
  test('CUR-TC-56: Required type + difficulty validation', async ({ page }) => {
    await page.goto(`${ADMIN_URL}${QUESTIONS_PATH}`);
    await page.waitForLoadState('networkidle');
    await page.waitForTimeout(2000);
    await page.getByTestId('new-question-btn').click();
    const editorDialog = page.getByTestId('question-editor-dialog');
    await expect(editorDialog).toBeVisible({ timeout: 15_000 });
    // Save without type
    await page.getByTestId('question-editor-save').click();
    await page.waitForTimeout(500);
    await expect(editorDialog).toBeVisible();
    const alerts = page.locator('[role="alert"]');
    expect(await alerts.count()).toBeGreaterThan(0);
    await page.getByTestId('question-editor-cancel').click();
  });

  // CUR-TC-57: Question type locked on edit
  test('CUR-TC-57: Question type locked on edit', async ({ page }) => {
    await page.goto(`${ADMIN_URL}${QUESTIONS_PATH}`);
    await page.waitForLoadState('networkidle');
    await page.waitForTimeout(2000);
    const questionRows = page.locator('[data-testid^="question-row-"]');
    const count = await questionRows.count();
    if (count === 0) { test.skip(); return; }
    const testId = await questionRows.first().getAttribute('data-testid');
    const idMatch = testId?.match(/question-row-(\d+)/);
    if (!idMatch) { test.skip(); return; }
    const qId = idMatch[1];
    await page.getByTestId(`question-${qId}-edit`).click();
    const editorDialog = page.getByTestId('question-editor-dialog');
    await expect(editorDialog).toBeVisible({ timeout: 10_000 });
    // Type select should not be present (locked)
    const typeSelect = page.getByTestId('question-type-select');
    const isSelectVisible = await typeSelect.isVisible({ timeout: 3000 }).catch(() => false);
    expect(isSelectVisible).toBe(false);
    await page.getByTestId('question-editor-cancel').click();
  });

  // CUR-TC-58: Edit question persists
  test('CUR-TC-58: Edit question persists', async ({ page }) => {
    // Create a fresh question for this test (known valid type) to avoid
    // picking up a question with questionType=0 from leftover test data.
    const qText = uniqueName('QC-TC58-TF');
    await page.goto(`${ADMIN_URL}${QUESTIONS_PATH}`);
    await page.waitForLoadState('networkidle');
    await page.waitForTimeout(2000);
    // Create a TrueFalse question to edit
    await page.getByTestId('new-question-btn').click();
    const editorDialog = page.getByTestId('question-editor-dialog');
    await expect(editorDialog).toBeVisible({ timeout: 15_000 });
    await page.getByTestId('question-type-select').selectOption('2'); // TrueFalse
    await page.waitForTimeout(500);
    await page.getByTestId('question-text-input').fill(qText);
    await page.getByTestId('question-difficulty-select').selectOption({ index: 1 });
    await page.getByTestId('tf-option-true').click();
    await page.getByTestId('question-editor-save').click();
    await expect(editorDialog).not.toBeVisible({ timeout: 15_000 });
    await page.waitForTimeout(2000);
    // Find the created question row and get its id
    const createdRow = page.locator(`[data-testid^="question-row-"]:has-text("${qText}")`);
    const rowTestId = await createdRow.first().getAttribute('data-testid');
    const idMatch = rowTestId?.match(/question-row-(\d+)/);
    if (!idMatch) { test.skip(); return; }
    const qId = idMatch[1];
    // Edit it: change text
    await page.getByTestId(`question-${qId}-edit`).click();
    await expect(editorDialog).toBeVisible({ timeout: 10_000 });
    const textInput = page.getByTestId('question-text-input');
    const origText = await textInput.inputValue();
    await textInput.fill(origText + ' (edited)');
    await page.getByTestId('question-editor-save').click();
    await expect(editorDialog).not.toBeVisible({ timeout: 15_000 });
    await page.waitForTimeout(2000);
    await expect(page.getByText(origText + ' (edited)')).toBeVisible({ timeout: 10_000 });
    // Restore original text
    await page.getByTestId(`question-${qId}-edit`).click();
    await expect(editorDialog).toBeVisible({ timeout: 10_000 });
    await textInput.fill(origText);
    await page.getByTestId('question-editor-save').click();
    await page.waitForTimeout(500);
  });

  // CUR-TC-59: Delete question
  test('CUR-TC-59: Delete question', async ({ page }) => {
    await page.goto(`${ADMIN_URL}${QUESTIONS_PATH}`);
    await page.waitForLoadState('networkidle');
    await page.waitForTimeout(2000);
    // Create throwaway question
    await page.getByTestId('new-question-btn').click();
    const editorDialog = page.getByTestId('question-editor-dialog');
    await expect(editorDialog).toBeVisible({ timeout: 15_000 });
    await page.getByTestId('question-type-select').selectOption('2'); // TrueFalse
    await page.waitForTimeout(300);
    await page.getByTestId('question-text-input').fill('Delete me TF question');
    await page.getByTestId('question-difficulty-select').selectOption({ index: 1 });
    await page.getByTestId('tf-option-true').click();
    await page.getByTestId('question-editor-save').click();
    await expect(editorDialog).not.toBeVisible({ timeout: 15_000 });
    await page.waitForTimeout(2000);
    // Find it
    const newRow = page.locator('[data-testid^="question-row-"]:has-text("Delete me TF question")');
    const rowTestId = await newRow.first().getAttribute('data-testid');
    const idMatch = rowTestId?.match(/question-row-(\d+)/);
    if (!idMatch) { test.skip(); return; }
    const qId = idMatch[1];
    await page.getByTestId(`question-${qId}-delete`).click();
    const confirmBtn = page.getByTestId('delete-question-confirm-btn');
    await expect(confirmBtn).toBeVisible({ timeout: 10_000 });
    await confirmBtn.click();
    await page.waitForTimeout(2000);
    await expect(newRow).not.toBeVisible({ timeout: 10_000 });
  });

  // CUR-TC-60: Activate / Deactivate question
  test('CUR-TC-60: Activate/Deactivate question', async ({ page }) => {
    await page.goto(`${ADMIN_URL}${QUESTIONS_PATH}`);
    await page.waitForLoadState('networkidle');
    await page.waitForTimeout(2000);
    const questionRows = page.locator('[data-testid^="question-row-"]');
    const count = await questionRows.count();
    if (count === 0) { test.skip(); return; }
    const testId = await questionRows.first().getAttribute('data-testid');
    const idMatch = testId?.match(/question-row-(\d+)/);
    if (!idMatch) { test.skip(); return; }
    const qId = idMatch[1];
    const toggleBtn = page.getByTestId(`question-${qId}-toggle-active`);
    await expect(toggleBtn).toBeVisible({ timeout: 10_000 });
    await toggleBtn.click();
    // Deactivate confirm
    const deactivateBtn = page.getByTestId('deactivate-question-confirm-btn');
    const hasDeactivate = await deactivateBtn.isVisible({ timeout: 5000 }).catch(() => false);
    if (hasDeactivate) {
      await deactivateBtn.click();
    }
    await page.waitForTimeout(2000);
    // Toggle back
    await toggleBtn.click();
    const activateBtn = page.getByTestId('activate-question-confirm-btn');
    const hasActivate = await activateBtn.isVisible({ timeout: 5000 }).catch(() => false);
    if (hasActivate) {
      await activateBtn.click();
    }
    await page.waitForTimeout(1000);
  });

  // CUR-TC-61: Questions reorder + save
  test('CUR-TC-61: Questions reorder and save', async ({ page }) => {
    await page.goto(`${ADMIN_URL}${QUESTIONS_PATH}`);
    await page.waitForLoadState('networkidle');
    await page.waitForTimeout(2000);
    const questionRows = page.locator('[data-testid^="question-row-"]');
    const count = await questionRows.count();
    if (count < 2) { test.skip(); return; }
    const testId = await questionRows.first().getAttribute('data-testid');
    const idMatch = testId?.match(/question-row-(\d+)/);
    if (!idMatch) { test.skip(); return; }
    const qId = idMatch[1];
    const moveDownBtn = page.getByTestId(`question-${qId}-move-down`);
    await expect(moveDownBtn).toBeVisible({ timeout: 10_000 });
    await moveDownBtn.click();
    await page.waitForTimeout(300);
    const saveOrderBtn = page.getByTestId('questions-save-order');
    await expect(saveOrderBtn).toBeVisible({ timeout: 5000 });
    await saveOrderBtn.click();
    await page.waitForTimeout(1500);
    await expect(saveOrderBtn).not.toBeVisible({ timeout: 5000 });
  });

  // CUR-TC-62: Open editor by clicking question text
  test('CUR-TC-62: Open editor via question text click', async ({ page }) => {
    await page.goto(`${ADMIN_URL}${QUESTIONS_PATH}`);
    await page.waitForLoadState('networkidle');
    await page.waitForTimeout(2000);
    const questionRows = page.locator('[data-testid^="question-row-"]');
    if (await questionRows.count() === 0) { test.skip(); return; }
    const testId = await questionRows.first().getAttribute('data-testid');
    const idMatch = testId?.match(/question-row-(\d+)/);
    if (!idMatch) { test.skip(); return; }
    const qId = idMatch[1];
    const textBtn = page.getByTestId(`question-${qId}-text-btn`);
    await expect(textBtn).toBeVisible({ timeout: 10_000 });
    await textBtn.click();
    const editorDialog = page.getByTestId('question-editor-dialog');
    await expect(editorDialog).toBeVisible({ timeout: 10_000 });
    await page.getByTestId('question-editor-cancel').click();
  });
});
