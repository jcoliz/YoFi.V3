# PRD Writing Guidance

## Purpose

This document provides guidance for writing Product Requirements Documents (PRDs) for the YoFi.V3 project. It clarifies what belongs in a PRD versus other document types and provides examples of appropriate scope.

---

## PRD Status Workflow

PRDs progress through four status levels:

### Draft
**Definition**: Author is actively writing and editing the PRD.

**Activities**:
- Writing user stories and acceptance criteria
- Researching technical constraints
- Resolving open questions
- Iterating on scope and goals

**Who can edit**: PRD author
**When to move to next status**: When author is ready for feedback and has populated all sections which the author has a clear view of. There may be section still yet complete where author would like reviewer input to craft those sections.

### In Review
**Definition**: Author is actively soliciting feedback from stakeholders.

**Activities**:
- Team reviews user stories for completeness
- Stakeholders validate business requirements
- Technical leads assess feasibility
- Open questions are discussed and resolved
- Handoff checklist items are validated

**Who can edit**: PRD author (incorporating feedback)
**When to move to next status**: When handoff checklist is reviewed and all items are green-checked (approved)

### Approved
**Definition**: PRD has passed handoff checklist review and is ready for implementation planning.

**Activities**:
- Detailed design or implementation planning can begin
- Design documents can be created (if needed)
- No further changes to requirements without re-review

**Who can edit**: Requires stakeholder approval for changes
**When to move to next status**: When feature is fully implemented and working in code

### Implemented
**Definition**: Feature is working in the code and PRD is now historical reference.

**Activities**:
- PRD becomes documentation of what was built
- Used for onboarding and maintenance
- Changes require new PRD or feature modification request

**Who can edit**: Read-only (historical record)

---

## PRD Scope: WHAT and WHY, Not HOW

**A PRD defines WHAT to build and WHY, not HOW to build it.**

### What Belongs in a PRD

**Include these elements**:
- **Problem Statement**: 1-3 sentences describing the problem or opportunity
- **User Stories**: As a [user], I want [action], so that [goal]
- **Acceptance Criteria**: Specific, testable requirements for each story
- **Business Rules**: Validation requirements, conflict resolution logic, user-facing behavior
- **Success Metrics**: How to measure feature success
- **High-Level Entity Concepts**: E.g., "PayeeMatchingRule entity with payee pattern, category, and usage metadata"
- **Affected Layers**: Which parts of the codebase will change (Frontend, Controllers, Application, Entities, Database)
- **Code Pattern References**: Links to similar controllers/features to follow as patterns
- **Dependencies**: Related features or PRDs
- **Open Questions**: Unresolved decisions that need discussion

### What Does NOT Belong in a PRD

**These implementation details belong in a Design Document**:
- ❌ Complete entity definitions with C# code, XML comments, and validation attributes
- ❌ DTO definitions with validation attributes
- ❌ API endpoint specifications (routes, HTTP methods, request/response types)
- ❌ Query patterns with EF Core syntax
- ❌ Database indexes and constraints (specific SQL/migrations)
- ❌ Specific method signatures for Feature classes
- ❌ Frontend component names and routes

---

## When to Create a Design Document

**Create a companion Design Document** (e.g., `FEATURE-NAME-DESIGN.md`) when:
- Feature is complex with many interacting components
- Multiple developers/AI agents will implement different layers
- Detailed technical specifications would accelerate implementation
- You want to validate technical approach before writing code

**Design Documents are optional** for:
- Simple CRUD features following existing patterns
- Features where implementation can follow obvious patterns from existing code
- Prototypes or experiments where design will emerge during implementation

**Example**: The Transaction Splits PRD has a companion [`TRANSACTION-SPLIT-DESIGN.md`](transactions/TRANSACTION-SPLIT-DESIGN.md) with complete entity definitions, DTOs, API endpoints, and query patterns.

---

## PRD Template Structure

See [`PRD-TEMPLATE.md`](PRD-TEMPLATE.md) for the standard structure:

1. **Metadata**: Status, dates, owner, release target
2. **Problem Statement**: Why are we building this?
3. **Goals & Non-Goals**: What's in scope and what's explicitly out of scope
4. **User Stories**: Who needs what and why?
5. **Technical Approach**: High-level technical direction (not detailed implementation)
6. **Open Questions**: Unresolved decisions
7. **Success Metrics**: How to measure success
8. **Dependencies & Constraints**: What this depends on and what limits it
9. **Notes & Context**: Historical context, related documents
10. **Handoff Checklist**: Readiness for implementation

---

## Examples of Appropriate PRD Scope

### ✅ Good: High-Level Entity Concept

```markdown
**Entity Design (Draft)**:
```csharp
public class PayeeMatchingRule : BaseTenantModel
{
    public string PayeePattern { get; set; }
    public bool PayeeIsRegex { get; set; }
    public string Category { get; set; }
    public DateTime? LastUsedAt { get; set; }
    public int MatchCount { get; set; }
}
```
```

This shows the **concept** without implementation details. Properties are listed to convey the data model.

### ❌ Too Detailed: Full Entity with Implementation Details

```markdown
/// <summary>
/// Represents a payee matching rule for automatic transaction categorization.
/// </summary>
/// <param name="logger">Logger for diagnostic output.</param>
/// <remarks>
/// Rules are tenant-scoped and evaluated in precedence order.
/// </remarks>
[Table("YoFi.V3.PayeeMatchingRules")]
public record PayeeMatchingRule : BaseTenantModel
{
    /// <summary>
    /// Pattern to match against transaction payee.
    /// </summary>
    [Required]
    [MaxLength(200)]
    [NotWhiteSpace]
    public string PayeePattern { get; set; } = string.Empty;

    // ... etc with full XML comments and validation
}
```

This level of detail belongs in a **Design Document**, not a PRD.

### ✅ Good: Business Rule

```markdown
**Conflict Resolution Rules**:

When multiple rules match a transaction, apply precedence in this order:
1. More matching aspects wins (e.g., payee + amount beats payee only)
2. Regex pattern beats substring pattern
3. Longer pattern beats shorter pattern
4. Most recently modified rule wins
```

This is a **business rule** that affects user experience - correctly belongs in PRD.

### ❌ Too Detailed: Query Implementation

```markdown
```csharp
var matchingRules = await context.PayeeMatchingRules
    .AsNoTracking()
    .Where(r => r.TenantId == tenantId)
    .Where(r => r.PayeeIsRegex
        ? Regex.IsMatch(transaction.Payee, r.PayeePattern, RegexOptions.IgnoreCase)
        : transaction.Payee.Contains(r.PayeePattern, StringComparison.OrdinalIgnoreCase))
    .OrderByDescending(r => r.ModifiedAt)
    .ToListAsync();
```
```

This implementation detail belongs in a **Design Document** or the code itself.

### ✅ Good: Code Pattern Reference

```markdown
**Code Patterns to Follow**:
- CRUD operations: [`TransactionsController.cs`](../../src/Controllers/TransactionsController.cs:1)
- Tenant-scoped authorization: [`TenantController.cs`](../../src/Controllers/Tenancy/Api/TenantController.cs:1)
- Entity pattern: [`BaseTenantModel`](../../src/Entities/Models/BaseTenantModel.cs:1)
```

This guides implementers to existing patterns without prescribing exact implementation.

---

## Handoff Checklist

Before handing a PRD to implementation, verify:

- [ ] **All user stories have clear acceptance criteria** - Each story has specific, testable requirements
- [ ] **Open questions are resolved** - All critical decisions documented
- [ ] **Technical approach indicates affected layers** - Clear which parts of codebase will change
- [ ] **Code patterns to follow are referenced** - Links to similar features/controllers

**Note**: You do NOT need to specify DTOs, API endpoints, or query patterns in the PRD. Those are implementation details.

---

## PRD vs Other Document Types

| Document Type | Purpose | Audience | When to Create |
|---------------|---------|----------|----------------|
| **PRD** | Define WHAT to build and WHY | Product owner, stakeholders, implementers | Start of feature planning |
| **Design Document** | Define HOW to build it | Developers, architects | Complex features before implementation |
| **ADR (Architecture Decision Record)** | Document significant architectural decisions | Architects, team leads | When making decisions that affect system structure |
| **Implementation Notes** | Capture lessons learned during implementation | Future maintainers | After implementing complex features |

---

## Examples from YoFi.V3

### Well-Scoped PRDs
- [`PRD-PAYEE-RULES.md`](payee-rules/PRD-PAYEE-RULES.md) - Focused on requirements, references patterns
- [`PRD-TRANSACTION-RECORD.md`](transactions/PRD-TRANSACTION-RECORD.md) - User needs and business rules

### PRD with Companion Design Document
- [`PRD-TRANSACTION-SPLITS.md`](transactions/PRD-TRANSACTION-SPLITS.md) (requirements)
- [`TRANSACTION-SPLIT-DESIGN.md`](transactions/TRANSACTION-SPLIT-DESIGN.md) (implementation details)

### Design Documents
- [`TRANSACTION-SPLIT-DESIGN.md`](transactions/TRANSACTION-SPLIT-DESIGN.md) - Complete technical specification

---

## Questions?

If you're unsure whether something belongs in a PRD:
1. Ask: "Is this a requirement (WHAT) or an implementation detail (HOW)?"
2. Ask: "Would changing this require user approval or just developer discussion?"
3. Ask: "Could this be implemented differently and still meet the requirements?"

If the answer to #3 is "yes," it's probably an implementation detail that doesn't belong in the PRD.

When in doubt, err on the side of **less detail in PRD**, **more detail in Design Document**.

---

## Creating a New PRD

### Process
1. **Identify Strategic Theme** - Which capability area does this feature belong to?
2. **Copy Template** - Use [`PRD-TEMPLATE.md`](PRD-TEMPLATE.md)
3. **Create PRD File** - Save in feature area folder: `docs/wip/{feature-area}/PRD-{FEATURE-NAME}.md`
4. **Fill Out Sections** - Problem Statement, Goals, User Stories, Technical Approach
5. **Update Roadmap** - Add feature to appropriate release section in [`PRODUCT-ROADMAP.md`](PRODUCT-ROADMAP.md)
6. **Request Review** - Update status as PRD progresses through lifecycle

### PRD Guidelines
- **Keep PRDs concise** - Under 200 lines preferred; link to separate design docs for implementation details
- **Focus on what and why** - Save how (implementation) for technical design documents
- **Include acceptance criteria** - Make user stories testable with clear success conditions
- **Link to designs** - Reference detailed technical documents for implementation guidance
- **Reference existing patterns** - Link to similar features or architecture documents

### Handoff Checklist (for AI Implementation)
Before marking a PRD as "Design Complete" or "Ready for Implementation":
- [ ] All user stories have clear acceptance criteria
- [ ] Open questions are resolved or documented as design decisions
- [ ] Technical approach section indicates affected layers (Frontend, API, Application, Database)
- [ ] Existing code patterns and files are referenced as examples
- [ ] Companion technical design document created if needed (for complex features)
