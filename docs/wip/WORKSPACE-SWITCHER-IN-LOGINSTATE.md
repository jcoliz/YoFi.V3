---
status: Draft (For future consideration)
ado: "User Story 1988: [User Can] Switch workspaces from LoginState menu"
---

# Design Proposal: Workspace Switcher in LoginState Menu

## Overview

Proposal to consolidate workspace switching functionality into the [`LoginState.vue`](../../src/FrontEnd.Nuxt/app/components/LoginState.vue) user menu dropdown, providing quick access to workspace switching alongside user profile actions.

## Current State

Currently, workspace switching is handled by:
- **[`WorkspaceSelector.vue`](../../src/FrontEnd.Nuxt/app/components/WorkspaceSelector.vue)** - Separate component in header with dropdown menu showing current workspace details and select dropdown for switching
- **[`workspaces.vue`](../../src/FrontEnd.Nuxt/app/pages/workspaces.vue)** - Full page for workspace management (CRUD operations)
- **[`LoginState.vue`](../../src/FrontEnd.Nuxt/app/components/LoginState.vue)** - User menu with Profile and Sign Out only

**Current Layout:**
```
Header:
├─ [Logo] [Nav Links] ... [Workspace: Personal ⋮] [User Icon: John ⋮]
```

## Problem Statement

The current implementation separates workspace switching from user account actions, resulting in:
1. **Separate dropdowns** - User must remember two different menu locations
2. **Header clutter** - Two dropdown triggers in header take up space
3. **Inconsistent patterns** - Most SaaS apps consolidate these in one menu
4. **Mobile challenges** - Multiple dropdowns harder to manage on small screens

## Proposed Solution: Hybrid Approach

**Pattern:** Show current workspace + quick switcher in user menu, with link to full page

```
[User Icon] John ⋮
├─ WORKSPACE
│  ├─ Personal (Current ✓)
│  │  Owner
│  ├─────────
│  ├─ Company A        ← Quick switch (3-5 max)
│  ├─ Family Budget
│  ├─────────
│  └─ All Workspaces → [Link to /workspaces]
├─────────────
├─ Profile
├─ Settings
├─────────────
└─ Sign Out
```

### Why This Solution is Best for YoFi.V3

1. **Consolidation** - Single dropdown for all user/workspace actions
2. **Quick switching** - Most users have 2-5 workspaces (Personal, Family, Company)
3. **Scalability** - Links to existing `/workspaces` page for power users
4. **Financial app pattern** - Matches Mint, QuickBooks, Personal Capital patterns
5. **Mobile-friendly** - No nested flyouts, works well on small screens
6. **Header cleanup** - Removes separate WorkspaceSelector dropdown
7. **Leverages existing code** - Uses your already-built `/workspaces` page

### Proposed Implementation

#### Visual Design

```
┌─────────────────────────────┐
│ [User Icon] John Doe      ↓ │
├─────────────────────────────┤
│ WORKSPACE                   │
│ ┌─────────────────────────┐ │
│ │ Personal (Current)      │ │ ← Highlighted with checkmark
│ │ Owner                   │ │
│ └─────────────────────────┘ │
│ • Company Finances          │ ← Clickable to switch
│ • Family Budget             │
│ → All Workspaces            │ ← Links to /workspaces
├─────────────────────────────┤
│ Profile                     │
│ Settings (future)           │
├─────────────────────────────┤
│ Sign Out                    │
└─────────────────────────────┘
```

#### Component Changes

**[`LoginState.vue`](../../src/FrontEnd.Nuxt/app/components/LoginState.vue)** modifications:

```vue
<script setup lang="ts">
import { useUserPreferencesStore } from '~/stores/userPreferences'
import { TenantClient, type TenantRoleResultDto } from '~/utils/apiclient'

const { data, status, signOut } = useAuth()
const userPreferencesStore = useUserPreferencesStore()

// Workspace state
const workspaces = ref<TenantRoleResultDto[]>([])
const loading = ref(false)

// Load workspaces on mount
onMounted(async () => {
  if (status.value === 'authenticated') {
    await loadWorkspaces()
  }
})

// Watch auth status to load workspaces when user logs in
watch(status, async (newStatus) => {
  if (newStatus === 'authenticated') {
    await loadWorkspaces()
  }
})

async function loadWorkspaces() {
  loading.value = true
  try {
    const { baseUrl } = useApiBaseUrl()
    const authFetch = useAuthFetch()
    const client = new TenantClient(baseUrl, authFetch)
    workspaces.value = await client.getTenants()
  } catch (error) {
    console.error('Failed to load workspaces:', error)
  } finally {
    loading.value = false
  }
}

const currentWorkspace = computed(() => {
  return userPreferencesStore.currentTenant
})

const otherWorkspaces = computed(() => {
  const currentKey = currentWorkspace.value?.key
  return workspaces.value.filter(w => w.key !== currentKey)
})

async function switchWorkspace(workspace: TenantRoleResultDto) {
  userPreferencesStore.setCurrentTenant(workspace)
  // Reload current page to refresh data with new workspace context
  await navigateTo(window.location.pathname)
}

function systemLogin() {
  navigateTo('/login')
}

const systemLogout = async () => {
  try {
    await signOut({ redirect: true, callbackUrl: '/' })
  } catch (error) {
    console.error('Logout error:', error)
  }
}
</script>

<template>
  <DropDownPortable
    class="ms-2 my-1 d-flex align-items-center"
    data-test-id="login-state"
  >
    <template #trigger>
      <a
        class="d-flex align-items-center link-body-emphasis text-decoration-none p-0 dropdown-toggle"
        data-bs-toggle="dropdown"
        aria-expanded="false"
      >
        <template v-if="data">
          <strong class="me-2" data-test-id="username">{{ data.name }}</strong>
        </template>
        <FeatherIcon
          icon="user"
          size="24"
          class="rounded-circle me-2"
          :class="ready ? 'text-body' : 'text-primary'"
        />
      </a>
    </template>
    <template #default>
      <ul class="dropdown-menu dropdown-menu-end text-small shadow">
        <template v-if="status === 'authenticated'">
          <!-- Workspace Section -->
          <li class="px-3 py-2">
            <div class="mb-2">
              <small class="text-muted text-uppercase fw-bold">Workspace</small>
            </div>

            <!-- Current Workspace -->
            <div v-if="currentWorkspace" class="workspace-current mb-2 p-2 bg-light rounded">
              <div class="d-flex align-items-center justify-content-between">
                <div>
                  <div class="fw-semibold">{{ currentWorkspace.name }}</div>
                  <small class="text-muted">{{ getRoleName(currentWorkspace.role) }}</small>
                </div>
                <FeatherIcon icon="check" size="16" class="text-success" />
              </div>
            </div>

            <!-- Quick Switch List (max 3-5) -->
            <div v-if="otherWorkspaces.length > 0" class="workspace-quick-list mb-2">
              <a
                v-for="workspace in otherWorkspaces.slice(0, 4)"
                :key="workspace.key"
                class="dropdown-item dropdown-item-sm py-1"
                @click="switchWorkspace(workspace)"
              >
                {{ workspace.name }}
              </a>
            </div>

            <!-- Loading State -->
            <div v-if="loading" class="text-center py-2">
              <BaseSpinner size="sm" />
            </div>

            <!-- Link to Full Page -->
            <NuxtLink
              to="/workspaces"
              class="dropdown-item dropdown-item-sm text-primary"
              data-test-id="all-workspaces-link"
            >
              <FeatherIcon icon="arrow-right" size="14" class="me-1" />
              All Workspaces
            </NuxtLink>
          </li>

          <li><hr class="dropdown-divider" /></li>

          <!-- User Actions -->
          <li>
            <NuxtLink
              class="dropdown-item"
              to="/profile"
              data-test-id="Profile"
            >
              Profile
            </NuxtLink>
          </li>
          <li><hr class="dropdown-divider" /></li>
          <li>
            <a
              class="dropdown-item"
              data-test-id="SignOut"
              @click="systemLogout"
            >
              Sign Out
            </a>
          </li>
        </template>
        <template v-else>
          <li>
            <a
              class="dropdown-item"
              data-test-id="SignIn"
              @click="systemLogin"
            >
              Sign In
            </a>
          </li>
          <li><hr class="dropdown-divider" /></li>
          <li>
            <NuxtLink
              class="dropdown-item"
              to="/register"
              data-test-id="CreateAccount"
            >
              Request Account
            </NuxtLink>
          </li>
        </template>
      </ul>
    </template>
  </DropDownPortable>
</template>

<style scoped>
.workspace-current {
  border-left: 3px solid var(--bs-success);
}

.dropdown-item-sm {
  font-size: 0.875rem;
  padding: 0.25rem 0.5rem;
}

.workspace-quick-list .dropdown-item {
  cursor: pointer;
}
</style>
```

**[`WorkspaceSelector.vue`](../../src/FrontEnd.Nuxt/app/components/WorkspaceSelector.vue):**
- **Option A:** Remove entirely (workspace switching only in LoginState)
- **Option B:** Keep as simpler "current workspace display only" (no switching functionality)
- **Option C:** Keep as-is for pages that need prominent workspace context (like transactions list)

**Recommendation:** Option C - Keep WorkspaceSelector for specific pages where workspace context is primary (transactions, budgets), remove from global header.

## Implementation Steps

1. **Phase 1: Add workspace section to LoginState.vue**
   - Add workspace loading logic
   - Add workspace list display
   - Add quick-switch functionality
   - Add "All Workspaces" link

2. **Phase 2: Update header layout**
   - Remove WorkspaceSelector from global header (if present)
   - Test consolidated LoginState dropdown
   - Verify mobile responsiveness

3. **Phase 3: Conditionally show WorkspaceSelector**
   - Only show on pages where workspace is primary context
   - Position inline with page content (not in global header)

4. **Phase 4: Testing**
   - Test workspace switching from LoginState
   - Test with many workspaces (10+)
   - Test mobile experience
   - Update functional tests

## Future Enhancements

Once hybrid approach is stable, consider:

1. **Favorites/Recent** - Show most recently used workspaces first
2. **Search** - Add search in "All Workspaces" page (already exists)
3. **Keyboard shortcuts** - Cmd/Ctrl + K for quick workspace switcher
4. **Workspace icons** - Add icons/avatars for visual distinction
5. **Notifications badge** - Show unread count per workspace

## Alternatives Considered

### Alternative 1: Inline Dropdown with Live Switch (Current Implementation)

**Current [`WorkspaceSelector.vue`](../../src/FrontEnd.Nuxt/app/components/WorkspaceSelector.vue) pattern:**

```
Workspace: Personal [⋮]
├─ [Current Workspace Details]
│  Name: Personal
│  Role: Owner
│  Created: Jan 1, 2024
├─────────────
├─ Change Workspace [Select Dropdown]
│  • Personal (selected)
│  • Company A
│  • Family Budget
├─────────────
└─ Manage Workspaces → [/workspaces page]
```

**✅ Pros:**
- Quick switching without navigation
- Shows current workspace details
- Minimal clicks (1 to open, 1 to switch)
- Context remains on current page

**❌ Cons:**
- Takes up header real estate
- Separate from user menu (inconsistent with SaaS patterns)
- Limited space for many workspaces
- Harder to show rich workspace information
- Two dropdowns in header (WorkspaceSelector + LoginState)

**Best for:** 2-10 workspaces, when workspace is primary context

**Examples:** Your current implementation

---

### Alternative 2: Secondary Dropdown/Flyout Menu

**Pattern:** User menu item triggers a secondary side panel/flyout

```
[User Icon] John ⋮
├─ Profile
├─ Settings
├─────────────
├─ Switch Workspace  →  [Secondary Flyout Opens]
│                        ├─ Personal (Current ✓)
│                        ├─ Company A
│                        ├─ Company B
│                        ├─────────────
│                        └─ + Create Workspace
├─────────────
└─ Sign Out
```

**✅ Pros:**
- Keeps user menu clean and simple
- Can show many workspaces with scroll
- Visual hierarchy (current workspace clearly highlighted)
- Industry standard (GitHub, Linear, Notion)
- Consolidates workspace + user actions

**❌ Cons:**
- Requires hover or click to open secondary menu
- More complex implementation (nested popovers)
- Mobile interaction can be tricky (nested dropdowns)
- 2-3 clicks to switch workspaces
- Requires careful UX to avoid accidental closes

**Best for:** Many workspaces (10+), desktop-focused apps

**Examples:** GitHub organizations, Linear workspaces, Notion workspaces

---

### Alternative 3: Navigate to Switcher Page

**Pattern:** User menu has link that navigates to dedicated workspace management page

```
[User Icon] John ⋮
├─ Profile
├─ Settings
├─────────────
├─ Switch Workspace  → [Navigates to /workspaces]
├─────────────
└─ Sign Out
```

On `/workspaces` page: Full workspace cards with rich information, search, filters

**✅ Pros:**
- Unlimited space for workspace cards/list
- Can show rich information (members, activity, description)
- Easy to add search/filter for many workspaces
- Simpler implementation (no nested menus)
- Better mobile experience (full page)
- You already have this page built!

**❌ Cons:**
- Loses context (navigates away from current page)
- Feels slower for quick switches
- More clicks (at least 2: menu → page → select)
- Page reload/navigation overhead

**Best for:** Many workspaces with complex management needs

**Examples:** Slack workspace switcher, Discord server list, Microsoft Teams

---

## Comparison Matrix

| Feature | Alt 1: Current | Alt 2: Flyout | Alt 3: Page Only | Hybrid (Proposed) ⭐ |
|---------|---------------|---------------|------------------|---------------------|
| Header Clutter | ❌ Two dropdowns | ✅ One dropdown | ✅ One dropdown | ✅ One dropdown |
| Quick Switching | ✅ 2 clicks | ⚠️ 3 clicks | ❌ 3+ clicks | ✅ 2 clicks |
| Mobile-Friendly | ⚠️ Separate menus | ❌ Nested menus | ✅ Full page | ✅ Simple menu |
| Scalability | ⚠️ Limited space | ✅ Scrollable | ✅ Full page | ✅ Hybrid |
| Implementation | ✅ Already built | ❌ Complex | ✅ Page exists | ⚠️ Moderate |
| Industry Pattern | ⚠️ Uncommon | ✅ Common | ⚠️ Less common | ✅ Very common |

## Industry Examples

**Hybrid Approach (Proposed):**
- Figma - User menu shows current team + quick switcher + "See all teams"
- Vercel - User menu shows current project + recent projects + "All projects"
- GitLab - User menu shows current group + recent groups + "View all groups"
- Stripe Dashboard - User menu shows account switcher inline

**Flyout Approach (Alternative 2):**
- GitHub - Organizations in nested flyout
- Linear - Workspaces in nested flyout
- Notion - Workspaces in side panel

**Page-Only Approach (Alternative 3):**
- Slack - Workspace switcher is separate page
- Discord - Server list is always visible sidebar
- Microsoft Teams - Team switcher is dedicated UI

## Decision

**Status:** Draft - Pending review

**Recommendation:** Implement Alternative 4 (Hybrid Approach)

**Next Steps:**
1. Review with team
2. Create mockups/prototypes if needed
3. Plan implementation phases
4. Update functional tests
5. Document final design in architecture decisions

## Related Documents

- [`LoginState.vue`](../../src/FrontEnd.Nuxt/app/components/LoginState.vue) - Current implementation
- [`WorkspaceSelector.vue`](../../src/FrontEnd.Nuxt/app/components/WorkspaceSelector.vue) - Current workspace selector
- [`workspaces.vue`](../../src/FrontEnd.Nuxt/app/pages/workspaces.vue) - Workspace management page
- [`docs/TENANCY.md`](../TENANCY.md) - Tenancy architecture
