# LOKA MASTER CORRECTION: ENGLISH-ONLY UI, CLEAN CHECKLISTS, FRAMEWORK INSTALLATION FIXES, AND FULL VERIFICATION

## Main Objective

Audit and improve the existing Loka desktop application without breaking existing functionality.

The priorities are:

1. Translate all existing user-facing application text into natural, consistent English.
2. Make every existing checklist visually clean, consistent, responsive, and fully functional.
3. Continue fixing Laravel and CodeIgniter project creation and installation.
4. Remove only SEO functionality that was newly introduced because of the previous misunderstanding.
5. Build, test, and verify the actual implementation before reporting completion.

**Important:** Inspect the existing source code, project structure, Git diff, and current implementation before making changes. Modify the actual source files rather than merely providing recommendations or pseudocode.

---

## 1. Translate the Entire Existing Loka UI into English

Audit all existing user-facing text throughout the application and translate Indonesian text into natural, professional English.

Include, but do not limit the audit to:

- Dashboard, sidebar, navigation, and existing menus.
- Project management and project creation wizard.
- Framework selection, project installation, and configuration.
- PHP, Apache, Nginx, MySQL, MariaDB, and PostgreSQL management.
- Current server selection, service status, and server controls.
- Buttons, labels, placeholders, tooltips, dialogs, and confirmation prompts.
- Success messages, warnings, notifications, validation errors, and exceptions displayed to users.
- Activity logs, troubleshooting instructions, and diagnostic screens.
- Settings, preferences, and installer interfaces.
- Dynamically generated messages and status descriptions.
- All checklist labels, section titles, descriptions, and related instructions.

Inspect XAML files, resource dictionaries, view models, code-behind files, localization resources, hardcoded strings, and other sources of visible text.

Do not translate only the main dashboard while leaving secondary screens, dialogs, installation flows, or error-handling paths in Indonesian.

### Translation rules

- Use consistent technical terminology throughout the application.
- Keep technical identifiers, configuration keys, framework APIs, database fields, and user-generated content unchanged unless a specific technical reason requires otherwise.
- Do not rename internal classes, methods, variables, database columns, configuration properties, or APIs merely to translate them.
- Do not change application behavior solely to translate a label.
- Preserve all existing functionality and user data.

**Expected result:** All existing Loka interface text is in English, including text displayed during failures and troubleshooting.

---

## 2. Do Not Introduce SEO Functionality

The previous SEO requirement was a misunderstanding. The actual requirement is English-only UI text.

Do not create:

- An SEO menu or navigation item.
- An SEO page or dashboard.
- An SEO management module.
- New SEO settings or services.
- SEO-specific project management features.
- Any other new SEO-related functionality.

Before removing anything, inspect the Git diff and identify which changes were introduced specifically because of the mistaken SEO interpretation.

If a new SEO menu or feature was introduced during that implementation, remove only that newly introduced functionality and its associated navigation entries.

Preserve unrelated existing functionality, configuration, project data, and framework installation fixes. Do not remove pre-existing project configuration merely because it contains the term SEO.

Verify that no new SEO menu or feature remains as a result of this work.

---

## 3. Standardize and Improve All Existing Checklists

Audit every existing checklist, checkbox group, selectable option list, and related control throughout Loka.

This includes checklists found in:

- Project creation and framework installation wizards.
- PHP and web-server configuration.
- MySQL, MariaDB, and PostgreSQL settings.
- Environment and service configuration.
- Application settings and preferences.
- Installer interfaces.
- Confirmation dialogs and other existing screens that contain checkbox controls.

Do not create unnecessary checklists or add unrelated options. Improve the existing controls and their layout.

### 3.1 Consistent visual design

Create a consistent visual standard for existing checklist components.

- Align checkboxes and their labels vertically.
- Use consistent checkbox dimensions, row heights, margins, padding, and spacing.
- Keep labels immediately beside their checkboxes.
- Make the complete label area clickable when appropriate.
- Group related options into clearly named sections.
- Use consistent spacing between items and sections.
- Avoid crowded lists, excessive whitespace, uneven alignment, and unnecessary decorative elements.
- Ensure long labels wrap naturally without overlapping checkboxes or adjacent controls.
- Align secondary descriptions beneath the main label, indented to the beginning of the text rather than the checkbox.
- Use consistent typography and readable text sizes.

### 3.2 Match the existing Loka design

Preserve Loka's minimalist, professional, light-theme design.

- Use a white or light background and the application's existing color palette.
- Use subtle borders only where they improve clarity.
- Avoid excessive rounded corners, heavy shadows, unnecessary gradients, and excessive icons.
- Do not introduce a dark theme.
- Do not redesign the entire application unnecessarily.
- Make checklist controls visually consistent with existing buttons, inputs, forms, and panels.

The result should feel like a polished part of Loka's existing interface, not a separate design system.

### 3.3 Checkbox interaction states

Ensure each existing checkbox has a clear visual state for:

- Checked.
- Unchecked.
- Disabled.
- Hover.
- Keyboard focus.

Maintain sufficient contrast and a visible keyboard-focus indicator.

The checked and unchecked states must be immediately distinguishable. Disabled controls must appear disabled without making their labels unreadable.

### 3.4 Responsive layout and accessibility

- Prevent text clipping, overlap, and horizontal layout overflow.
- Ensure long descriptions wrap correctly.
- Support different window sizes and DPI scaling.
- Maintain appropriate spacing when the window is resized.
- Support keyboard navigation and activation.
- Associate labels with their corresponding checkbox controls.
- Preserve accessibility and readable contrast.

### 3.5 Shared components and maintainability

Inspect the existing UI architecture before deciding how to standardize checklist components.

If appropriate, create or refactor a reusable checklist component or shared style/resource dictionary.

- Follow the existing framework and architecture.
- Avoid duplicating identical XAML styles across multiple screens.
- Do not create unnecessary abstraction for a small number of controls.
- Keep existing bindings, commands, events, validation, and data models intact.
- Reuse the same checklist styling consistently throughout the application.

### 3.6 Preserve checklist functionality

Visual improvements must not break behavior.

For every existing checklist, verify that:

- Clicking the checkbox updates the correct bound value.
- Clicking the label toggles the checkbox when appropriate.
- Default selections remain unchanged.
- Changes are saved and restored correctly wherever persistence already exists.
- Existing validation rules continue to work.
- Disabled options remain non-interactive where required.
- Dependent options and conditional sections continue to behave correctly.
- Checkboxes connected to installer options, service configuration, or other system operations still trigger their intended behavior.
- No control is merely decorative or disconnected from its actual functionality.

Do not change defaults, business rules, configuration values, or system operations just to make the interface look better.

---

## 4. Preserve and Fix Laravel and CodeIgniter Project Installation

Continue addressing the existing framework creation and installation problems independently of UI translation and checklist styling.

### 4.1 Laravel projects

Ensure Laravel project creation generates a real, functional Laravel application with the appropriate structure, including:

- `artisan`
- `public/index.php`
- `bootstrap/app.php`
- `routes/web.php`
- `composer.json`
- Composer dependencies and `vendor/autoload.php`
- `.env` configuration and a valid application key when required.

Use the project's configured PHP and Composer executables, correct working directory, and appropriate installation procedure.

Do not replace Laravel with a static HTML page or generic Loka welcome page.

### 4.2 CodeIgniter 4 projects

Ensure CodeIgniter 4 project creation generates a real application with the appropriate structure, including:

- `public/index.php`
- `spark`
- `app/Config/App.php`
- `app/Config/Routes.php`
- `composer.json`
- Composer dependencies and `vendor/autoload.php`.

Preserve the framework's expected bootstrap and routing behavior.

### 4.3 Installation process reliability

For both frameworks:

- Validate project names, destination paths, and domain configuration.
- Use the configured PHP executable and Composer installation.
- Run Composer in the correct project directory.
- Capture standard output, standard error, process exit codes, and relevant failure details.
- Handle timeouts and process failures without falsely reporting success.
- Do not mark a project as ready when dependency installation fails.
- Do not display a successful installation status when required files are missing.
- Show accurate, actionable English error messages.
- Avoid duplicate installation processes and unsafe retries.
- Do not overwrite or delete unrelated projects, files, databases, or user data.
- Preserve existing project settings and unrelated features.

### 4.4 Correct virtual hosts and document roots

Verify that each project's domain points to its own correct directory.

For Laravel and CodeIgniter 4, configure the web server's document root to the relevant project's `public` directory.

Confirm that:

- Each domain maps to the intended project.
- Projects do not accidentally share document roots.
- Existing Apache or Nginx configurations remain compatible with the application's current architecture.
- Project-specific environment and database configurations remain isolated.
- A project's domain does not redirect to phpMyAdmin or another project's dashboard unexpectedly.

Do not rewrite unrelated server-management functionality unless the investigation identifies a concrete defect.

### 4.5 Do not roll back legitimate fixes

Framework installation repairs are separate from translation, checklist styling, and SEO cleanup.

Do not revert valid Laravel or CodeIgniter improvements merely because they were introduced in the same changeset as UI or SEO-related changes.

Inspect the Git diff carefully and retain unrelated, legitimate fixes.

---

## 5. Implementation Workflow

Follow this workflow rather than making broad, unverified changes.

### Step 1: Inspect

- Inspect the solution structure, source files, existing UI components, resource dictionaries, view models, and configuration.
- Review the current Git diff and working-tree changes.
- Identify all Indonesian user-facing strings.
- Locate every existing checklist and inspect its visual and functional implementation.
- Identify any newly introduced SEO-related changes.
- Review the Laravel and CodeIgniter installation workflows.

Do not discard existing uncommitted changes or overwrite unrelated work.

### Step 2: Establish the current baseline

Before modifying code:

- Determine the existing build status.
- Identify available tests and relevant test projects.
- Record existing failures so they are not incorrectly attributed to the new changes.
- Identify the actual target framework, build configuration, and required dependencies.

### Step 3: Implement targeted changes

Perform the following as separate, carefully scoped changes:

1. Translate existing user-facing text into English.
2. Remove only mistakenly introduced SEO functionality, if found.
3. Standardize existing checklist layout and interaction.
4. Fix the Laravel and CodeIgniter creation and installation defects.
5. Preserve existing server management, project management, and unrelated functionality.

Avoid unrelated refactoring and unnecessary architectural changes.

### Step 4: Verify source and UI

Search the source for remaining Indonesian user-facing text, including:

- XAML and resource dictionaries.
- Code-behind and view models.
- Dialogs, validation, notifications, and exception messages.
- Activity log templates and dynamically generated text.
- Installer and troubleshooting interfaces.

Distinguish actual UI text from technical identifiers, comments, test fixtures, user-generated content, and unrelated documentation.

Review all checklist locations for alignment, spacing, responsiveness, and functioning bindings.

Search the navigation and UI structure to verify that no new SEO menu or feature was introduced.

### Step 5: Build and test

Use the actual solution and configuration found during inspection.

- Build the application in the relevant configuration.
- Run the available automated tests.
- Fix errors caused by the changes.
- Run targeted tests for UI bindings, checklist state handling, project creation, Composer execution, and virtual-host configuration where the existing test infrastructure supports them.
- Verify generated Laravel and CodeIgniter project structures.
- Verify installation success and failure reporting.
- Check the installer or release build if these changes affect the packaging workflow.

Do not fabricate test results. If a test cannot run, state why.

### Step 6: Final review

Review the final Git diff to confirm:

- No unrelated functionality was removed.
- No user data or project configuration was deleted.
- No new SEO functionality was introduced.
- Existing framework fixes remain in place.
- Checklist behavior and defaults are preserved.
- All actual build and test results are reported accurately.

---

## 6. Final Report

Provide a concise but evidence-based report containing:

1. **English UI:** Areas audited, categories of Indonesian text translated, and any remaining exceptions.
2. **Files changed:** Actual file paths and a short description of each significant change.
3. **SEO cleanup:** Whether any newly introduced SEO functionality was found and removed.
4. **SEO verification:** Confirmation that no new SEO menu, page, dashboard, or feature was introduced.
5. **Checklist improvements:** Locations updated, styling changes, responsive behavior, and functional checks performed.
6. **Framework installation:** Laravel and CodeIgniter defects fixed, project structures verified, and installation failure handling tested.
7. **Build results:** Actual build command, exit code, and outcome.
8. **Test results:** Tests executed, passed, failed, and skipped, with reasons where applicable.
9. **Remaining issues:** Any unresolved defects or verification steps that could not be completed.

Do not claim completion based only on code edits or a successful build. Clearly distinguish source inspection, automated tests, and manual runtime verification.

## Final Acceptance Criteria

The task is complete only when the implementation demonstrates that:

- Existing Loka user-facing text is consistently English.
- No new SEO menu or SEO feature has been added.
- All existing checklists have consistent, clean, responsive layouts.
- Existing checklist values, bindings, persistence, and behavior remain correct.
- Laravel and CodeIgniter project creation and installation are handled correctly.
- Project domains and document roots are isolated and configured correctly.
- The application builds successfully, or any baseline or unresolved build failure is clearly documented.
- Test results and remaining issues are reported honestly.

**Priority:** Functional correctness first, security and data integrity second, consistent UI and maintainable architecture third.

**Do not merely explain what should be changed. Inspect the actual implementation, make targeted source-code changes, and verify the results.**