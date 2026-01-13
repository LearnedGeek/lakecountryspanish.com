# View Refactoring Plan - Lake Country Spanish

## Overview

This plan outlines a strategy to make views easier to manage by extracting reusable components, consolidating duplicated code, and establishing a consistent component library.

**Current State:**
- 73 views across 12 directories
- Only 3 shared partials
- 0 view components
- Significant code duplication

**Target State:**
- 10+ reusable view components
- Consolidated Create/Edit forms
- Extracted JavaScript utilities
- ~2,000+ lines reduced

---

## Phase 1: Core Components (Quick Wins)

### 1.1 Create Alert Component
**Location:** `Views/Shared/Components/Alert/Default.cshtml`

Used on nearly every page for success/error messages.

```csharp
public class AlertViewComponent : ViewComponent
{
    public IViewComponentResult Invoke(string type, string message, bool dismissible = true)
}
```

**Types:** success, error, warning, info

**Impact:** Replaces ~50 inline alert blocks

### 1.2 Create Button Component
**Location:** `Views/Shared/Components/Button/Default.cshtml`

Standardizes button styling across the application.

```csharp
public class ButtonViewComponent : ViewComponent
{
    public IViewComponentResult Invoke(
        string text,
        string type = "primary", // primary, secondary, danger, ghost
        string size = "md",      // sm, md, lg
        string icon = null,
        string href = null,
        bool submit = false)
}
```

**Impact:** Replaces 12+ files with duplicate button markup

### 1.3 Create Badge/StatusPill Component
**Location:** `Views/Shared/Components/StatusBadge/Default.cshtml`

For consistent status indicators throughout the app.

```csharp
public class StatusBadgeViewComponent : ViewComponent
{
    public IViewComponentResult Invoke(
        string text,
        string color = "gray") // gray, green, yellow, red, blue, indigo, cyan
}
```

**Impact:** Used in 19+ files for status displays

### 1.4 Create StatCard Component
**Location:** `Views/Shared/Components/StatCard/Default.cshtml`

For dashboard statistics cards.

```csharp
public class StatCardViewComponent : ViewComponent
{
    public IViewComponentResult Invoke(
        string value,
        string label,
        string color = "indigo",
        string icon = null)
}
```

**Impact:** Consolidates 16+ stat card implementations

---

## Phase 2: Page Structure Components

### 2.1 Create PageHeader Component
**Location:** `Views/Shared/Components/PageHeader/Default.cshtml`

Consolidates page title + action buttons pattern.

```csharp
public class PageHeaderViewComponent : ViewComponent
{
    public IViewComponentResult Invoke(
        string title,
        string subtitle = null,
        string backUrl = null,
        string backText = "Back")
}
```

With slot for action buttons via `@await Component.InvokeAsync("PageHeader", new { title = "...", slot = "<button>...</button>" })`

**Impact:** ~30 admin pages with similar headers

### 2.2 Create EmptyState Component
**Location:** `Views/Shared/Components/EmptyState/Default.cshtml`

For empty data states with icon, message, and CTA.

```csharp
public class EmptyStateViewComponent : ViewComponent
{
    public IViewComponentResult Invoke(
        string title,
        string message = null,
        string icon = "folder",
        string actionText = null,
        string actionUrl = null)
}
```

**Impact:** Used in Topics, Assignments, Badges, Student Dashboard

### 2.3 Create Card Component
**Location:** `Views/Shared/Components/Card/Default.cshtml`

Base card container with optional header.

```csharp
public class CardViewComponent : ViewComponent
{
    public IViewComponentResult Invoke(
        string title = null,
        bool noPadding = false)
}
```

**Impact:** 178+ occurrences of card pattern

---

## Phase 3: Form Components

### 3.1 Create FormGroup Partial
**Location:** `Views/Shared/_FormGroup.cshtml`

Standardizes label + input + validation structure.

```razor
@model FormGroupModel
<div class="mb-4">
    <label asp-for="@Model.For" class="block text-sm font-medium text-gray-700 mb-1">
        @Model.Label
    </label>
    @Model.InputHtml
    <span asp-validation-for="@Model.For" class="text-red-600 text-sm"></span>
</div>
```

### 3.2 Consolidate Create/Edit Forms

**Badge Form:**
- Merge `CreateBadge.cshtml` + `EditBadge.cshtml` into `BadgeForm.cshtml`
- Use `isEdit` flag to toggle behavior

**Topic Form:**
- Merge `CreateTopic.cshtml` + `EditTopic.cshtml` into `TopicForm.cshtml`
- Use `isEdit` flag to toggle behavior

**Implementation:**
```razor
@model BadgeFormViewModel
@{
    var isEdit = Model.Id > 0;
    ViewData["Title"] = isEdit ? "Edit Badge" : "Create Badge";
}
```

**Impact:** Eliminates ~400 lines of duplicate code

---

## Phase 4: Interactive Components

### 4.1 Create Modal Component
**Location:** `Views/Shared/Components/Modal/Default.cshtml`

Reusable modal dialog wrapper.

```csharp
public class ModalViewComponent : ViewComponent
{
    public IViewComponentResult Invoke(
        string id,
        string title,
        string size = "md") // sm, md, lg, xl
}
```

**Extract from Student Dashboard:**
1. Feedback Modal (~110 lines)
2. Tip Modal (~60 lines)
3. Cancel Class Modal (~50 lines)

### 4.2 Create DataTable Component
**Location:** `Views/Shared/Components/DataTable/Default.cshtml`

For consistent table styling.

```csharp
public class DataTableViewComponent : ViewComponent
{
    public IViewComponentResult Invoke(bool striped = true, bool hover = true)
}
```

**Impact:** 19+ files with similar table markup

---

## Phase 5: JavaScript Refactoring

### 5.1 Create Modal Utility
**Location:** `wwwroot/js/modals.js`

```javascript
const Modal = {
    open(modalId) { },
    close(modalId) { },
    reset(modalId) { }
};
```

### 5.2 Create Form Utilities
**Location:** `wwwroot/js/forms.js`

```javascript
const Forms = {
    validate(formId) { },
    reset(formId) { },
    submitAsync(formId, url, options) { }
};
```

### 5.3 Create Rating Component
**Location:** `wwwroot/js/ratings.js`

Extract star rating functionality from Student Dashboard.

---

## Phase 6: Layout Refactoring

### 6.1 Split _Layout.cshtml

**Extract:**
- `_Navigation.cshtml` - Main navigation bar
- `_Footer.cshtml` - Footer content
- `_Messages.cshtml` - TempData alerts

### 6.2 Create Role-Based Layouts (Optional)

- `_AdminLayout.cshtml` - Admin sidebar navigation
- `_StudentLayout.cshtml` - Student navigation
- `_PublicLayout.cshtml` - Public pages

---

## Implementation Order

| Order | Component | Files Affected | LOC Reduction |
|-------|-----------|----------------|---------------|
| 1 | Alert | ~50 | ~200 |
| 2 | Button | 12+ | ~150 |
| 3 | StatusBadge | 19+ | ~100 |
| 4 | StatCard | 16+ | ~150 |
| 5 | PageHeader | 30+ | ~300 |
| 6 | EmptyState | 6+ | ~150 |
| 7 | BadgeForm consolidation | 2 | ~200 |
| 8 | TopicForm consolidation | 2 | ~200 |
| 9 | Modal | 3+ | ~250 |
| 10 | Card | 55+ | ~200 |
| 11 | JavaScript utilities | Multiple | ~200 |
| 12 | Layout split | 1 | ~100 |

**Total Estimated LOC Reduction:** ~2,000+

---

## File Structure After Refactoring

```
Views/
├── Shared/
│   ├── Components/
│   │   ├── Alert/
│   │   │   └── Default.cshtml
│   │   ├── Button/
│   │   │   └── Default.cshtml
│   │   ├── Card/
│   │   │   └── Default.cshtml
│   │   ├── DataTable/
│   │   │   └── Default.cshtml
│   │   ├── EmptyState/
│   │   │   └── Default.cshtml
│   │   ├── Modal/
│   │   │   └── Default.cshtml
│   │   ├── PageHeader/
│   │   │   └── Default.cshtml
│   │   ├── StatCard/
│   │   │   └── Default.cshtml
│   │   └── StatusBadge/
│   │       └── Default.cshtml
│   ├── _Layout.cshtml
│   ├── _Navigation.cshtml
│   ├── _Footer.cshtml
│   ├── _Messages.cshtml
│   ├── _FormGroup.cshtml
│   └── _ValidationScriptsPartial.cshtml
├── Admin/
│   ├── BadgeForm.cshtml      (merged Create/Edit)
│   ├── TopicForm.cshtml      (merged Create/Edit)
│   └── ... (other views)
└── ...

wwwroot/
├── js/
│   ├── modals.js
│   ├── forms.js
│   └── ratings.js
└── ...

ViewComponents/
├── AlertViewComponent.cs
├── ButtonViewComponent.cs
├── CardViewComponent.cs
├── DataTableViewComponent.cs
├── EmptyStateViewComponent.cs
├── ModalViewComponent.cs
├── PageHeaderViewComponent.cs
├── StatCardViewComponent.cs
└── StatusBadgeViewComponent.cs
```

---

## Usage Examples

### Alert Component
```razor
@* Before *@
<div class="bg-green-100 border border-green-200 text-green-700 px-4 py-3 rounded-lg">
    @TempData["Success"]
</div>

@* After *@
@await Component.InvokeAsync("Alert", new { type = "success", message = TempData["Success"] })
```

### Button Component
```razor
@* Before *@
<a href="/admin/badges" class="inline-flex items-center px-4 py-2 border border-gray-300 rounded-md shadow-sm text-sm font-medium text-gray-700 bg-white hover:bg-gray-50">
    <svg class="w-4 h-4 mr-2">...</svg>
    Back to Badges
</a>

@* After *@
@await Component.InvokeAsync("Button", new {
    text = "Back to Badges",
    href = "/admin/badges",
    type = "secondary",
    icon = "arrow-left"
})
```

### PageHeader Component
```razor
@* Before *@
<div class="flex justify-between items-center mb-6">
    <h1 class="text-3xl font-bold text-gray-900">Badges</h1>
    <a asp-action="CreateBadge" class="inline-flex items-center...">
        <svg>...</svg>
        Create Badge
    </a>
</div>

@* After *@
<vc:page-header title="Badges">
    <actions>
        <vc:button text="Create Badge" asp-action="CreateBadge" icon="plus" />
    </actions>
</vc:page-header>
```

---

## Benefits

1. **Consistency** - Uniform styling across all pages
2. **Maintainability** - Single source of truth for each component
3. **Speed** - Faster development of new features
4. **Accessibility** - Centralized ARIA attributes and focus management
5. **Testability** - Components can be unit tested in isolation
6. **Theme Changes** - Update once, propagate everywhere
