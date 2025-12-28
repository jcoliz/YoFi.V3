# Workflow Templates

This directory contains detailed template documents that support the [IMPLEMENTATION-WORKFLOW.md](../IMPLEMENTATION-WORKFLOW.md).

## Purpose

The main workflow document provides high-level orchestration instructions, while these templates provide detailed step-by-step guidance for complex tasks. This separation helps Orchestrator mode delegate effectively while ensuring Code/Architect modes have complete instructions.

## Templates

### [FUNCTIONAL-TEST-PLAN-TEMPLATE.md](FUNCTIONAL-TEST-PLAN-TEMPLATE.md)

**Used in:** Step 10.5 of Implementation Workflow

**Mode:** Architect

**Purpose:** Guide creation of functional test plans that identify critical UI-dependent workflows worthy of functional testing.

**Key Topics:**
- Identifying scenarios (10-15% target)
- Writing justifications (risk, coverage gaps)
- Determining Gherkin language tiers (Tier 1 vs Tier 2)
- Single-responsibility principle
- Example test plans

### [FUNCTIONAL-TEST-IMPLEMENTATION-PLAN-TEMPLATE.md](FUNCTIONAL-TEST-IMPLEMENTATION-PLAN-TEMPLATE.md)

**Used in:** Step 10.6 of Implementation Workflow

**Mode:** Architect

**Purpose:** Guide creation of detailed implementation plans that bridge from Gherkin scenarios to C# test code.

**Key Topics:**
- Analyzing Page Object Models (POMs)
- Identifying step definitions
- Planning test control endpoints
- Determining implementation order
- Risk assessment
- Complete example plans

## Usage Pattern

1. **Orchestrator** reads main workflow, identifies step requiring detailed guidance
2. **Orchestrator** delegates to Architect/Code mode with reference to appropriate template
3. **Architect/Code** reads template directly for complete instructions
4. **Architect/Code** follows template structure to create deliverable
5. **Orchestrator** reviews deliverable and proceeds to next step

## Benefits

**For Orchestrator:**
- Concise workflow steps (easy to parse and convey)
- Clear delegation boundaries
- Reduced token usage in delegation messages

**For Architect/Code Modes:**
- Complete, detailed instructions in one place
- No ambiguity about requirements
- Examples and anti-patterns included
- Checklists for self-verification

**For Users:**
- Consistent deliverable quality
- Predictable structure across features
- Easy to audit and improve templates independently

## Adding New Templates

When workflow steps become too complex (>20 lines, >3 levels of nesting):

1. Create template in this directory: `{STEP-NAME}-TEMPLATE.md`
2. Include: Purpose, Instructions, Examples, Checklists, Common Mistakes
3. Update main workflow to reference template
4. Test with Orchestrator to verify effective delegation
5. Update this README with new template entry

## Template Structure Guidelines

**All templates should include:**

1. **Purpose statement** - When and why to use this template
2. **Prerequisites** - What must be complete before starting
3. **Step-by-step instructions** - Numbered, actionable steps
4. **Examples** - Real-world examples from project
5. **Anti-patterns** - Common mistakes to avoid
6. **Checklists** - Self-verification before completion
7. **Success criteria** - How to know the work is complete

**Keep instructions:**
- Specific (not vague)
- Actionable (clear next steps)
- Complete (no "figure it out" gaps)
- Structured (headings, lists, code blocks)

## Related Documentation

- [`docs/wip/IMPLEMENTATION-WORKFLOW.md`](../IMPLEMENTATION-WORKFLOW.md) - Main orchestration workflow
- [`docs/TESTING-STRATEGY.md`](../../TESTING-STRATEGY.md) - Test layer selection guidance
- [`tests/Functional/INSTRUCTIONS.md`](../../../tests/Functional/INSTRUCTIONS.md) - Functional test mechanics
- [`.roorules`](../../../.roorules) - Project-wide patterns and conventions
