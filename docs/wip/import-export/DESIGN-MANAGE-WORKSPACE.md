---
status: In Review
target_release: Alpha 1
related_docs:
  - PRD-TENANT-DATA-ADMIN.md
  - DESIGN-SAMPLE-DATA.md
---

# Design: Manage Workspace

## Overview

Design the Owner-only workspace management page at
`/workspaces/{workspaceKey}`. The page brings workspace settings and tenant data
administration together for one workspace. Tenant data administration includes
the import, export, deletion, and sample-data capabilities defined by their
respective product and feature designs.

The page does not combine workspace deletion with tenant-data deletion.
Workspace deletion removes the workspace; tenant-data deletion removes its
tenant data while preserving the workspace and its memberships.

## Decisions

| Area | Decision |
|------|----------|
| Route | `/workspaces/{workspaceKey}`, implemented as `app/pages/workspaces/[workspaceKey].vue` |
| Access | Workspace Owners only |
| Entry | Replace workspace-card Edit and Delete actions with a Manage action |
| Workspace settings | Provide workspace editing and workspace deletion on this page |
| Tenant data | Host all tenant data administration, including sample data, on this page |
| Target identity | Route workspace key is authoritative for page data and operations |
| Workspace switching | Switching changes the route and page context together |

## Entry from the Workspace List

On [workspaces.vue](../../../src/FrontEnd.Nuxt/app/pages/workspaces.vue),
replace the workspace-card **Edit** and **Delete** actions with **Manage**.
Manage opens `/workspaces/{workspaceKey}` for that card's workspace, regardless
of which workspace is currently selected elsewhere in the application.

Only Owners see the Manage action. Keep workspace creation and other
non-management workspace-list behavior unchanged.

## Page Structure

Show **Manage Workspace** with the selected workspace's name. Organize the page
into two clearly distinct areas:

1. **Workspace settings**: edit workspace details and delete the workspace.
2. **Data administration**: the unified tenant data administration experience,
   including import, export, delete-all-data, and sample data as those
   capabilities are delivered.

The sample-data section and its detailed workflow are specified in
[DESIGN-SAMPLE-DATA.md](./DESIGN-SAMPLE-DATA.md). This page design establishes
its location and access context; it does not duplicate its generation,
preview, loading, OFX, or reset behavior.

Use distinct labels and confirmations for workspace deletion and tenant-data
deletion. Workspace deletion removes the workspace itself and is not a shortcut
for clearing its data. Tenant-data deletion follows the safeguards in the
tenant-data administration design and preserves the workspace and memberships.
Do not silently perform one operation as part of the other.

Keep the existing edit and delete behavior when relocating those actions from
the workspace cards. Do not broaden workspace deletion semantics as part of this
page move.

## Route, Workspace Context, and Authorization

The workspace key in the URL determines the workspace displayed and targeted by
every action. Resolve the route key and verify the user's membership and Owner
role before synchronizing the shared current-workspace preference or loading
workspace-specific content. Never silently substitute the current or another
workspace for an invalid, unavailable, or unauthorized route key.

Enforce Owner authorization on the page and on every server operation it
exposes. Editors, Viewers, nonmembers, and unauthenticated users must not gain
access by navigating directly to the URL or calling an endpoint. Show an
explicit access-denied or unavailable state without leaking the workspace's
details or retaining actionable content from a previously selected workspace.

If the shared
[WorkspaceSelector](../../../src/FrontEnd.Nuxt/app/components/workspace/Selector.vue)
is available on this page, selecting another workspace navigates to its
`/workspaces/{workspaceKey}` route. The route and page context must stay aligned
through refresh and browser Back/Forward navigation. If the selected workspace
is not manageable by the user, show the access-denied state rather than
continuing to display the prior workspace's settings or data.

Clear workspace-specific state, including loaded data, confirmations, results,
and pending operation targets, when the route changes. While a write or
destructive operation is pending, prevent switching or navigation from
retargeting that operation. Do not treat cancellation of a browser request as
proof that a server-side write was canceled; follow the relevant feature design
for uncertain outcomes.

## Testing and Acceptance

- Owners see Manage on workspace cards; Editors and Viewers do not.
- Manage on a card opens that card's workspace even when another workspace was
  previously current.
- Workspace-card Edit and Delete actions are replaced by Manage.
- Owners can edit and delete their workspace from its Manage Workspace page.
- Workspace deletion and tenant-data deletion are distinct actions with
  separate confirmation and outcomes.
- Tenant-data administration, including sample data, is available in the
  Manage Workspace page.
- Editors, Viewers, nonmembers, and unauthenticated users cannot access the
  page's settings or data-administration operations.
- Invalid or unauthorized route keys never fall back silently and do not leave
  prior workspace content or actions visible.
- Refresh and Back/Forward keep route, selected workspace, displayed content,
  and operation targets aligned.
- Switching workspaces clears prior workspace state; pending writes cannot be
  retargeted.
