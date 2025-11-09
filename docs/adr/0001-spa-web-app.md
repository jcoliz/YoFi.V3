# 0001. Single Page Web App

Date: 2025-11-01

## Status

Accepted

## Context

YoFi was originally written before the popularity of SPA web apps.
It's a server-rendered ASP.NET app, originally built on .NET Core 2.2. Perceived performance with this approach is quite slow.

## Decision

The primary driving force behind this project is to move from a old-time ASP.NET server-rendered web app to a modern SPA app.

## Consequences

### Performance & User Experience

1. **Faster Navigation** - After initial load, SPAs only fetch data (JSON), not full HTML pages. Navigation feels instant with no page refreshes.

2. **Reduced Server Load** - Server only handles API requests and data, not rendering HTML for every interaction. Rendering work shifts to the client.

3. **Better Perceived Performance** - Can show loading states, skeleton screens, and optimistic updates while data loads in background.

4. **Offline Capabilities** - With service workers, SPAs can work offline or with poor connectivity, caching assets and data locally.

### Development & Architecture

5. **Clear Separation of Concerns** - Clean API boundary between frontend and backend. Backend is pure data/business logic, frontend handles all presentation.

6. **Independent Deployment** - Frontend and backend can be deployed separately. Update UI without touching backend and vice versa.

7. **Technology Flexibility** - Can use modern frontend frameworks (Vue, React, Angular) with their rich ecosystems. Not locked into Razor/C# for UI.

8. **Better Tooling** - Access to modern frontend dev tools: Hot Module Replacement (HMR), component dev tools, Vite/webpack optimizations.

### Scalability & Distribution

9. **CDN Distribution** - Static frontend assets can be served from CDN globally, reducing latency and server costs.

10. **API Reusability** - Same API can serve multiple clients (web, mobile apps, third-party integrations) without code duplication.

11. **Client-Side Caching** - Rich client-side state management and caching strategies reduce redundant server requests.

### Modern Features

12. **Rich Interactivity** - Complex UI interactions (drag-drop, real-time updates, animations) are easier to implement client-side.

13. **Progressive Enhancement** - Can build Progressive Web Apps (PWAs) with native-like features: push notifications, home screen install, etc.

14. **Better Mobile Experience** - Responsive SPAs can feel more app-like on mobile devices.

### Trade-offs to Consider

SPAs aren't always superior. They have downsides:
- **SEO Complexity** - Requires SSR (Server-Side Rendering) or pre-rendering for good SEO
- **Initial Load Time** - Larger JavaScript bundle to download initially
- **JavaScript Required** - Won't work if JS is disabled
- **Complexity** - More moving parts, state management, routing on client

### Why This Makes Sense for YoFi.V3

For **YoFi.V3** (a personal finance app), an SPA makes sense because:
- It's an authenticated app (SEO less critical)
- Users benefit from fast, interactive UI
- You want modern development experience (Nuxt 4)
- You can reuse the API for potential future mobile app