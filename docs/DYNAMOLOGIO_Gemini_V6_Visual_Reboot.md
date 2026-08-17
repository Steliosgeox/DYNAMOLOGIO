# DYNAMOLOGIO V6 — VISUAL REBOOT / NO-SLOP DESIGN GATE

Repository: `Steliosgeox/DYNAMOLOGIO`
Source branch: `gemini/v5-truth-gate`
Reviewed head: `41d6d92ce6db4dab7c58fcd7856280f91021e1e4`

Create:
`gemini/v6-visual-reboot`

DO NOT MERGE TO MAIN.
DO NOT TOUCH SECURITY / DATABASE / REPORTING / IMPORT BUSINESS LOGIC unless a UI binding absolutely requires it.
THIS PASS IS ABOUT PRODUCT DESIGN, VISUAL SYSTEM, INFORMATION ARCHITECTURE, AND REAL HUMAN USABILITY.

The V5 application is still visually rejected.

The problem is NOT a DataGrid header color.
The problem is NOT the lack of another border.
The problem is NOT that one empty state was missing.

The problem is that the product still looks like:
"generic WPF admin app + navy sidebar + white cards + blue buttons."

That is not the target.

## ABSOLUTE RULE 0 — YOU MUST ACTUALLY LOOK AT THE SCREENSHOTS

Before touching XAML:

Open these PNG files AS IMAGES using your environment's actual image viewer:
- `docs/screenshots/v5/MainWindow_Dashboard_1366x768.png`
- `docs/screenshots/v5/MainWindow_Dynamologio_1366x768.png`
- `docs/screenshots/v5/MainWindow_Personnel_1366x768.png`
- `docs/screenshots/v5/MainWindow_Absences_1366x768.png`
- `docs/screenshots/v5/MainWindow_Services_1366x768.png`
- `docs/screenshots/v5/MainWindow_Reports_1366x768.png`
- and the corresponding 1024x768 images.

Create:
`docs/V6-VISUAL-FORENSICS.md`

For EACH screen write at least 8 visible observations that can only come from looking at the rendered image:
- exact regions that are too empty
- components that look visually heavy
- typography hierarchy failures
- alignment problems
- cramped areas
- excessive borders
- poor density
- weak call-to-action hierarchy
- awkward table proportions
- color balance
- sidebar proportion
- header proportion
- visual rhythm

DO NOT derive this document merely from XAML.

If you cannot actually view the PNG files visually:
STOP.
State:
`BLOCKED: I cannot visually inspect the screenshots in this environment.`
Do not claim a visual redesign is verified.

## ABSOLUTE RULE 1 — SCREENSHOT GENERATION IS NOT VISUAL VERIFICATION

A test that writes a PNG proves only that WPF rendered a bitmap.

It does NOT prove:
- good design
- correct hierarchy
- no clipping
- good spacing
- professional appearance
- readable density
- strong aesthetics.

Never again use:
`VERIFIED BY TEST`
for subjective visual quality.

Allowed statuses:
- RENDERED
- MANUALLY VISUALLY REVIEWED
- USER APPROVED
- NOT REVIEWED

## ABSOLUTE RULE 2 — DO NOT "POLISH" THE CURRENT LAYOUT

Do not keep the current page architecture and merely:
- recolor it
- add shadows
- add more cards
- change corner radii
- make headers darker
- add badges
- add icons.

The current visual architecture itself must be challenged.

---

# 1. CURRENT VISUAL FAILURES TO FIX

## UI-001 — THE SHELL LOOKS LIKE A 2016 ADMIN TEMPLATE

Current traits:
- fixed 220px dark sidebar
- 52px dark top bar
- 28px status bar
- dark navy + bright cobalt
- permanent "Air-Gapped" badge
- large amount of chrome before useful content
- every page starts with another 16px heading and another white card.

Redesign the shell.

Target:
- Sidebar: approximately 176–188px expanded.
- Optional compact/collapsed mode: 56px icon rail.
- Top header: 44–48px maximum.
- Remove the large Air-Gapped badge from prime visual real estate.
- Put environment/security state in a subtle status area.
- Use page-level context in the content area, not in giant permanent chrome.
- At 1024px the sidebar should collapse automatically or switch compact mode.

No custom title-bar gimmicks unless proven stable on Windows 7.

## UI-002 — TYPOGRAPHY IS TOO SMALL AND FLAT

Current typical hierarchy:
- page title 16px
- section title 14px
- body 13px
- caption 11px.

The result has almost no visual hierarchy.

Create explicit typography tokens:

- `Type.PageTitle` = 21–22px Semibold
- `Type.PageSubtitle` = 12–13px Regular
- `Type.SectionTitle` = 14–15px Semibold
- `Type.Body` = 13px
- `Type.Table` = 12.5–13px
- `Type.Caption` = 11px
- `Type.KpiValue` = 26–30px
- `Type.KpiLabel` = 10–11px Medium/Semibold

Do NOT uppercase every label.
Use uppercase only for tiny metadata/status where it helps.

## UI-003 — THE COLOR SYSTEM IS TOO "AI ADMIN DASHBOARD"

Current navy:
`#0F172A / #1E293B`
Current primary:
`#2563EB`

Those are generic Tailwind/admin-dashboard defaults.

You may keep similar functional colors, but create a more deliberate institutional palette.

Suggested direction:
- Shell ink: `#111827` or a softened graphite/navy
- Canvas: `#F5F6F8`
- Main surface: `#FFFFFF`
- Raised surface: `#FAFBFC`
- Border: `#E5E7EB`
- Primary text: `#171A21`
- Secondary: `#667085`
- Accent: controlled Hellenic blue around `#2463A9` / `#2566B1`
- Present: muted emerald
- Warning: muted amber
- Danger: controlled red

Avoid saturated blue everywhere.

Use color intentionally:
- primary CTA
- selected navigation
- active status
- focused input

Not as decoration on every KPI.

## UI-004 — TOO MANY BOXES

The product still uses:
`Border -> CardContainer -> StackPanel -> another Border -> another TabControl`

Stop boxing every concept.

Use hierarchy from:
- whitespace
- type
- separators
- alignment
- background zones.

Card count should go DOWN, not up.

## UI-005 — DATA TABLES LOOK OLD

Current dark navy column headers create a dated enterprise-grid appearance.

Redesign grids:
- light/neutral header background
- dark header text
- 36–38px header
- 36–40px rows depending density
- no heavy zebra striping by default
- subtle 1px row separators
- hover state
- selected state
- right-aligned numeric cells
- consistent date formatting
- status shown as compact semantic chips
- row actions as quiet icon actions
- column alignment rules
- ellipsis + tooltip for overflow
- no full-width screaming dark header unless a specific table needs it.

Dark headers are NOT automatically "high contrast = premium."

## UI-006 — SIDEBAR LABELS ARE TOO VERBOSE

Current examples:
- `Απουσίες & Άδειες`
- `Αναφορές & Εξαγωγές`
- `Ιστορικό (Audit)`
- `Ρυθμίσεις & Backup`

Prefer:
- Αρχική
- Δυναμολόγιο
- Προσωπικό
- Απουσίες
- Υπηρεσίες
- Αναφορές
- Εισαγωγή
- Έλεγχος
- Ιστορικό
- Ρυθμίσεις

Keep tooltips/subtitles for explanation.

## UI-007 — NO REAL PAGE HEADER COMPONENT

Create one reusable page header pattern:
- 21–22px title
- concise subtitle
- contextual actions aligned right
- optional breadcrumb/context
- no repeated ad-hoc StackPanels.

## UI-008 — NO TRUE COMMAND BAR

Create a compact command bar pattern for:
- search
- filters
- date
- scope
- primary action
- export/print.

Do not scatter unrelated buttons across cards.

---

# 2. HERO SCREEN: DYNAMOLOGIO MUST BE COMPLETELY RECOMPOSED

The current screen is still:
- title
- command card
- 340px summary card
- 4 mini KPI cards
- category mini-table
- large tabbed card.

That is rejected.

## New layout

### Top
PageHeader:
`Δυναμολόγιο`

Subtitle:
`Κατάσταση δύναμης για 17/08/2026`

Right actions:
- `Excel`
- `Εκτύπωση`
- overflow menu if needed

### Context command strip
One horizontal strip:
- Date
- Today quick action
- Scope / Unit selector
- template status

No big surrounding "card".

### Strength summary
ONE integrated summary surface, not four baby cards.

Example:

```
ΔΥΝΑΜΗ            ΠΑΡΟΝΤΕΣ       ΑΠΟΝΤΕΣ       ΕΠΙΣΤΡΟΦΕΣ
156               132             24            6 σήμερα · 3 αύριο
```

Below it, a compact matrix:

```
                    Δύναμη   Παρόντες   Απόντες
Στελέχη                36        31         5
Οπλίτες                120       101        19
```

No colored backgrounds behind every number.
Use subtle semantic color only for present/absent values.

### Main data region
Full-width usable roster.

Use a modern segmented control:
- Απόντες
- Παρόντες
- Μεταβολές

Do NOT use old browser-like tab shapes.

Table should get most of the page.

When empty:
simple quiet empty-state inside table surface, not a floating card on top of a card.

### Attention strip
If important:
`6 επιστρέφουν σήμερα`
`2 ασυμφωνίες`
`Πρότυπο αναφοράς μη επαληθευμένο`

Make this concise and operational.

---

# 3. ABSENCES — REMOVE THE PERMANENT LEFT FORM

The current permanent 320px editor makes the page look like a CRUD form and steals table width.

REJECT IT.

New model:

## Default page
- PageHeader
- summary counts
- full-width tab/list
- command bar
- primary button: `Νέα απουσία`

## New absence interaction
When `Νέα απουσία` is clicked:
- open a right-side panel ~360–400px on 1366+
OR
- a well-designed modal dialog on smaller screens.

The list should remain the primary canvas.

At 1024x768:
- use modal
- never squeeze form + 8-column table side by side.

Panel fields:
- searchable person picker
- absence type
- from
- return
- calculated duration
- reference
- notes
- conflict/validation
- Save / Cancel

This single change should make Absences dramatically cleaner.

---

# 4. DASHBOARD — MAKE IT OPERATIONAL, NOT KPI DECORATION

Current dashboard has a five-value strip plus a table plus quick-action card.

Redesign:

### Primary question
"What requires attention right now?"

### Proposed structure
Top:
- Current strength summary in a single line/surface.

Main left ~65%:
`Σήμερα`
- active absences
- returns today
- service conflicts
- unresolved issues

Main right ~35%:
`Επόμενες Ενέργειες`
- 6 επιστροφές
- 2 αλλαγές αύριο
- template warning
- backup health warning

Bottom:
Today's services / recent changes.

Remove redundant giant quick-action buttons.
Navigation already exists.

---

# 5. PERSONNEL

Target:
- full-width dense roster
- strong SearchBox
- filters in command bar
- Add person primary action
- selected row opens right detail panel
- no permanently visible oversized secondary cards

Columns:
- Rank
- Full name
- ASM
- Unit
- Specialty
- Effective status

Detail panel:
- identity
- status
- recent absences
- services
- history
- edit/archive actions

---

# 6. SERVICES

Target:
- date navigator integrated into command bar
- primary `Νέα υπηρεσία`
- full-width daily roster
- conflict badges
- empty state
- detail/edit side panel
- no permanent create form beside table on 1024.

---

# 7. REPORTS

Do NOT use generic cards for every report.

Use:
- left report selector/list
- right preview/configuration surface
OR
- clean table/list of reports + action panel.

Each report:
- name
- purpose
- template status
- last generated
- preview
- export.

Make template state visually trustworthy, not decorative.

---

# 8. IMPORT

The import flow should visually look like a workflow, not a single admin form.

Use a horizontal stepper:
1. Αρχείο
2. Στήλες
3. Αντιστοίχιση
4. Έλεγχος
5. Προεπισκόπηση
6. Εισαγωγή

Current stage gets strongest hierarchy.

Use a large central workspace, not tiny cards everywhere.

---

# 9. SETTINGS

Use category navigation inside Settings:
- Μονάδα
- Κατάλογοι
- Πρότυπα
- Backup
- Ασφάλεια
- Εφαρμογή

Do not show every setting in one vertical box stack.

---

# 10. DESIGN SYSTEM REWRITE

The existing 26KB monolithic `DesignSystem.xaml` has become a dumping ground.

Split into:
- `Styles/Colors.xaml`
- `Styles/Typography.xaml`
- `Styles/Buttons.xaml`
- `Styles/Inputs.xaml`
- `Styles/Navigation.xaml`
- `Styles/Tables.xaml`
- `Styles/Tabs.xaml`
- `Styles/Dialogs.xaml`
- `Styles/Layout.xaml`
- `Styles/States.xaml`

Keep `Icons.xaml`.

Only if all dictionaries genuinely contain reusable styles.

## Required components
- AppShell
- PageHeader
- CommandBar
- SearchBox
- StatusChip
- MetricStrip
- DataGrid
- EmptyState
- SidePanel
- Dialog
- FilterChip
- SegmentedControl
- IconButton
- SplitButton if useful
- NotificationBanner
- Toast

No third-party runtime dependency unless absolutely necessary.

---

# 11. WINDOWS 7 VISUAL CONSTRAINTS

Still support Windows 7 SP1 / .NET Framework 4.7.2.

Allowed:
- standard WPF
- vector PathGeometry
- gradients only if extremely subtle
- DropShadowEffect sparingly
- custom ControlTemplates
- system Segoe UI

Forbidden:
- Mica
- Acrylic
- WebView2
- WinUI
- variable fonts
- online fonts
- GPU-heavy effects
- huge blur effects
- animation-heavy interface.

A beautiful WPF UI does not require modern OS APIs.

---

# 12. SPACING SYSTEM

Use one spacing scale:
4 / 8 / 12 / 16 / 20 / 24 / 32.

No random:
13px
17px
19px
etc. margins unless optically justified.

Page gutter:
- 24px at >=1280 content width
- 16px at smaller content width.

Section gap:
- 20–24px.

Control gap:
- 8px.

---

# 13. CONTROL DIMENSIONS

Buttons:
- normal 34–36px
- compact 30–32px

Inputs:
- 34–36px

Table:
- header 36px
- normal row 38px
- compact row 34px

Navigation item:
- 38–40px

Do not arbitrarily mix heights.

---

# 14. VISUAL DETAILS THAT MATTER

- Icons: consistent 16px / 18px optical size.
- Do not mix 12, 14, 20, 28px icons randomly.
- Align icons and text baselines.
- Make focus rings visible but elegant.
- Use 4–6px radius, not excessive pills.
- Use shadows only on floating overlays/dialogs, not every card.
- Use borders sparingly.
- Prefer surface contrast over shadows.
- Numeric values should use tabular-looking alignment.
- Put units/secondary metadata in muted text.
- Make danger actions visually quiet until confirmation.

---

# 15. REMOVE FAKE OR OVERLONG COPY

Examples to remove:
`Επιχειρησιακή Επισκόπηση (Αρχική)`
-> `Αρχική`

`Ημερήσιο Δυναμολόγιο & Κατάσταση Δύναμης`
-> `Δυναμολόγιο`

`Συγκεντρωτική εικόνα πραγματικής δύναμης μονάδας και ενεργών μεταβολών`
-> concise subtitle or remove.

Desktop operators do not need paragraphs explaining each screen.

---

# 16. THE FINAL COMMIT YOU JUST MADE WAS NOT A REDESIGN

Do not repeat this pattern:
- fix header foreground
- add an empty-state Border
- regenerate screenshot
- declare visual problem solved.

A visual reboot must materially change:
- composition
- hierarchy
- density
- use of chrome
- navigation proportion
- table treatment
- interaction model.

Use Git diff to prove meaningful structural changes.

---

# 17. VISUAL REVIEW GATE

After implementing ONLY these first three screens:
1. Shell/MainWindow
2. Dashboard
3. Dynamologio
4. Absences

STOP.

Generate real full-window screenshots:
- 1366x768
- 1024x768

Use realistic sanitized demo data.

Then OPEN THE GENERATED PNGs AS IMAGES.

Create:
`docs/V6-VISUAL-REVIEW-ROUND1.md`

For each image document:
- 5 strongest aspects
- 5 visible weaknesses
- clipping/overflow
- hierarchy
- density
- visual balance
- whether any area still resembles generic WPF/admin template.

If any screen still looks generic:
ITERATE AGAIN BEFORE MOVING TO OTHER screens.

Do not touch Personnel/Services/Reports/etc until Round 1 is visually accepted.

---

# 18. HUMAN APPROVAL GATE

At the end of Round 1, do NOT continue automatically.

Return screenshot paths and say:
`ROUND 1 READY FOR HUMAN VISUAL APPROVAL.`

No "complete".
No "verified".
No automatic implementation of the remaining screens.

The user must approve the visual direction first.

After approval, propagate the system to:
- Personnel
- Services
- Reports
- Import
- Validation
- History
- Settings.

---

# 19. TEST HONESTY

Remove/rename misleading tests.

Current test:
`AT_UI_001_SearchablePersonPicker_SearchesAllFiveDimensions`
must actually enter/search each dimension and assert filtered results.

Simply asserting ItemsSource has 2 items is not a search test.

Current screenshot tests may be called:
`ScreenshotCapture_*`

They may not be used as proof of aesthetic quality.

Visual quality requires human/image inspection.

---

# 20. ACCEPTANCE CRITERIA FOR ROUND 1

Shell:
- no 220px mandatory sidebar at 1024
- no oversized Air-Gapped badge
- active nav clear
- modern compact visual rhythm

Dashboard:
- no generic five-number dashboard strip
- useful operational attention
- less box soup
- page feels intentional

Dynamologio:
- no four colored mini KPI cards
- strength summary integrated
- table dominates useful space
- modern light table header
- template status truthful but visually subtle

Absences:
- no permanent left CRUD form
- full-width list by default
- New Absence opens side panel/modal
- 1024 is not cramped

Global:
- no dark-heavy DataGrid headers everywhere
- clear typography hierarchy
- consistent 4/8 spacing
- fewer borders/cards
- no emoji
- no default WPF feel
- no fake modern web-dashboard cosplay
- no excessive gradients/shadows
- screenshots actually visually inspected

---

# FINAL RESPONSE FOR ROUND 1

Return ONLY:

V6 VISUAL ROUND 1

Branch:
Commits:

Visual inspection capability:
Pre-redesign screenshot observations document:

Shell:
Dashboard:
Dynamologio:
Absences:

Full-window screenshots:
- 1366:
- 1024:

Visible remaining issues:
1.
2.
3.

Status:
ROUND 1 READY FOR HUMAN VISUAL APPROVAL

Do not say production-ready.
Do not say verified.
Do not write GOAL_COMPLETE.
Do not continue to remaining screens without approval.
