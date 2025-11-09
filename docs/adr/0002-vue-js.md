# 0002. Vue.JS as front-end framework

Date: 2025-11-09

## Status

Accepted

## Context

Once we've decided to have a SPA web app, we now need to choose a framework. The leading choices for SPA frameworks in 2025 are:

### The Big Three

1. **React** (Meta/Facebook)
   - Most popular, largest ecosystem
   - Component-based, JSX syntax
   - Frameworks: Next.js, Remix

2. **Vue.js** (Independent/Community)
   - Progressive framework
   - Component-based, template syntax
   - Frameworks: Nuxt, Quasar

3. **Angular** (Google)
   - Full-featured framework
   - TypeScript-first
   - Opinionated, batteries-included

### Emerging Alternatives

4. **Svelte/SvelteKit** - Compile-time framework, no virtual DOM
5. **Solid.js** - Fine-grained reactivity, React-like syntax
6. **Qwik** - Resumability over hydration, extreme performance focus

## Decision

We will use **Vue.js 3** with **Nuxt 4** as our front-end framework.

### Primary Reasons

* **Template syntax** feels familiar to HTML/Razor developers coming from ASP.NET
* **Clear, readable** single-file components (`.vue`)
* **Balanced approach** - less opinionated than Angular, more structured than React
* **Easy to build** reusable component libraries
* **Works well with Bootstrap** - our chosen CSS framework
* **Nuxt 4 ecosystem** - provides batteries-included experience with file-based routing, SSR/SSG
* **Independent governance** - not corporate-controlled

### Why Not React?

React is excellent and has the largest ecosystem, but:
- JSX syntax is less familiar coming from ASP.NET/Razor
- More fragmented ecosystem (many competing solutions for routing, state)
- Larger corporate influence from Meta

### Why Not Angular?

Angular is powerful for large enterprise applications, but:
- Steeper learning curve
- More opinionated and heavyweight
- Requires TypeScript (though we use it anyway, the ceremony is higher)

### Why Not Svelte?

Svelte is innovative with great performance, but:
- Smaller, less mature ecosystem
- Fewer third-party libraries and tools
- Less community support and examples

## Consequences

### Positive

**Gentle Learning Curve**
- Template syntax feels familiar to HTML/Razor developers coming from ASP.NET
- Progressive adoption - can start simple and add complexity as needed
- Clear, readable single-file components (`.vue`)

**Excellent Documentation**
- Comprehensive, well-organized official docs
- Strong focus on developer experience
- Great migration guides and best practices

**Balanced Architecture**
- Provides official solutions (Vue Router, Pinia) but doesn't force them
- Clear conventions without feeling restrictive
- "Sweet spot" between flexibility and structure

**Strong TypeScript Support**
- Vue 3 rewritten in TypeScript with first-class support
- Excellent type inference with Composition API
- Works seamlessly with modern tooling

**Performance**
- Smaller bundle sizes than React/Angular
- Efficient reactivity system
- Fast virtual DOM implementation
- Compile-time optimizations in Vue 3

**Composition API (Vue 3)**
- Modern, flexible way to organize component logic
- Better code reuse than mixins
- Similar to React Hooks but more intuitive

```typescript
// Example: Clean, reusable logic
import { ref, computed } from 'vue'

export function useCounter() {
  const count = ref(0)
  const doubled = computed(() => count.value * 2)
  return { count, doubled }
}
```

**Nuxt Ecosystem**
- File-based routing out of the box
- SSR/SSG capabilities for SEO when needed
- Auto-imports reduce boilerplate
- Similar to Next.js but arguably cleaner

**Component Design System Friendly**
- Single-file components encapsulate HTML/CSS/JS
- Scoped styles prevent CSS leakage
- Easy to build reusable component libraries
- Works well with Bootstrap, Tailwind, etc.

**Developer Tooling**
- Vue DevTools for debugging
- Vite for blazing-fast HMR
- Volar (VS Code extension) for excellent IDE support
- Great testing libraries (Vitest, Vue Test Utils)

**Community & Ecosystem**
- Large, active community
- Rich plugin ecosystem
- Well-maintained UI libraries (Vuetify, PrimeVue, Element Plus)
- Independent governance (not corporate-controlled)
- Compatible with existing `@coliz/vue-base-controls` library

### Negative

**Smaller Ecosystem Than React**
- Fewer third-party libraries (though ecosystem is still large)
- Less Stack Overflow content and tutorials
- Smaller job market (less relevant for personal project)

**Less Corporate Backing**
- No major tech company sponsor (though this can be positive)
- Funding relies on community and sponsors

**Framework Lock-in**
- Vue-specific knowledge less transferable than React
- Component libraries are Vue-specific

### Neutral

**TypeScript is Optional**
- We're using TypeScript anyway, but the flexibility exists
- May be confusing if mixing JS and TS patterns

## References

- [Vue.js Official Documentation](https://vuejs.org/)
- [Nuxt 4 Documentation](https://nuxt.com/)
- [Vue vs React Comparison](https://vuejs.org/guide/extras/composition-api-faq.html)
- [@coliz/vue-base-controls](https://www.npmjs.com/package/@coliz/vue-base-controls) - Our existing Vue component library
