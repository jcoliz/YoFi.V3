# Content Security Policy (CSP) Implementation Guide

## Overview

Content Security Policy (CSP) is the **primary defense against XSS attacks**. It tells the browser what resources are allowed to load and execute, preventing malicious scripts even if an XSS vulnerability exists.

---

## Implementation Locations

### 1. Frontend: Nuxt.js (Primary Implementation)

**File:** [`src/FrontEnd.Nuxt/nuxt.config.ts`](../../src/FrontEnd.Nuxt/nuxt.config.ts)

Add CSP headers in the `nitro.routeRules` section:

```typescript
export default defineNuxtConfig({
  // ... existing config ...

  nitro: {
    prerender: {
      routes: ['/health'],
    },
    routeRules: {
      '/**': {
        headers: {
          // Content Security Policy - Primary XSS defense
          'Content-Security-Policy': [
            "default-src 'self'",
            "script-src 'self' 'unsafe-inline' 'unsafe-eval'", // Nuxt requires these
            "style-src 'self' 'unsafe-inline'",
            "img-src 'self' data: https:",
            "font-src 'self'",
            "connect-src 'self' " + process.env.NUXT_PUBLIC_API_BASE_URL,
            "frame-ancestors 'none'",
          ].join('; '),

          // Additional security headers
          'X-Content-Type-Options': 'nosniff',
          'X-Frame-Options': 'DENY',
          'X-XSS-Protection': '1; mode=block',
          'Referrer-Policy': 'strict-origin-when-cross-origin',
        },
      },
    },
  },
})
```

**Important Configuration Notes:**

1. **`'unsafe-inline'` and `'unsafe-eval'`**
   - Required for Nuxt's reactive system and HMR (Hot Module Replacement)
   - For stricter production CSP, consider using nonces (see Advanced section)

2. **`connect-src`**
   - Must include your API base URL
   - Environment variable is replaced during build:
     - Development: `http://localhost:5070`
     - Production: Your deployed API URL
   - Format: `"connect-src 'self' " + process.env.NUXT_PUBLIC_API_BASE_URL`

3. **`img-src`**
   - Includes `data:` for inline images
   - Includes `https:` for external images (e.g., CDNs, avatars)
   - Adjust if you need specific image sources

### 2. Backend: ASP.NET Core (API Endpoints)

**File:** [`src/BackEnd/Program.cs`](../../src/BackEnd/Program.cs)

Add security headers middleware after `app.UseExceptionHandler()`:

```csharp
// Add security headers middleware
app.Use(async (context, next) =>
{
    // Content Security Policy for API responses
    // More restrictive since APIs don't need to load resources
    context.Response.Headers.Append("Content-Security-Policy",
        "default-src 'none'; frame-ancestors 'none'");

    // Prevent MIME type sniffing
    context.Response.Headers.Append("X-Content-Type-Options", "nosniff");

    // Prevent clickjacking
    context.Response.Headers.Append("X-Frame-Options", "DENY");

    // Control referrer information
    context.Response.Headers.Append("Referrer-Policy", "strict-origin-when-cross-origin");

    await next();
});

app.UseStatusCodePages();
```

**Placement in Program.cs:**
```csharp
// Exception handler must come BEFORE middleware that might throw exceptions
app.UseExceptionHandler();

// Add security headers HERE (before status code pages)
app.Use(async (context, next) => { /* security headers */ });

// Status code pages middleware
app.UseStatusCodePages();

// Authentication and Authorization
app.UseAuthentication();
app.UseAuthorization();
```

---

## Why Both Frontend and Backend?

| Location | Purpose | Priority | CSP Policy |
|----------|---------|----------|------------|
| **Frontend (Nuxt)** | Protects the SPA from XSS attacks | **Critical** | Permissive (allows scripts, styles) |
| **Backend (API)** | Protects API responses | Good practice | Restrictive (blocks everything) |

The **frontend implementation is most critical** since that's where XSS attacks would execute malicious JavaScript.

---

## Testing Your CSP Implementation

### Step 1: Implement the Headers

Add the CSP configuration to both `nuxt.config.ts` and `Program.cs` as shown above.

### Step 2: Start Development Servers

```bash
# Terminal 1: Start backend
cd src/BackEnd
dotnet run

# Terminal 2: Start frontend
cd src/FrontEnd.Nuxt
npm run dev
```

### Step 3: Test in Browser

1. Open your application in Chrome/Edge
2. Open DevTools (F12)
3. Click on the **Console** tab
4. Look for CSP violations (if any)

**Expected behavior:** No CSP violation errors (or only expected ones during development)

**Example CSP violation:**
```
Refused to load the script 'https://malicious.com/script.js' because it violates
the following Content Security Policy directive: "script-src 'self'"
```

### Step 4: Verify Headers are Set

In DevTools **Network** tab:
1. Reload the page
2. Click on the first request (usually the HTML document)
3. Go to **Headers** tab
4. Look for `Content-Security-Policy` in Response Headers

Should see something like:
```
Content-Security-Policy: default-src 'self'; script-src 'self' 'unsafe-inline' 'unsafe-eval'; ...
```

---

## Verification Checklist

After implementing CSP, verify:

- [ ] No CSP violations in browser console (except expected development ones)
- [ ] Application loads without errors
- [ ] All pages render correctly
- [ ] Images display properly
- [ ] Styles are applied
- [ ] API calls work (check `connect-src` includes API URL)
- [ ] Navigation works
- [ ] Forms submit successfully
- [ ] Authentication flow works

---

## Troubleshooting Common Issues

### Issue: API Calls Blocked

**Error in console:**
```
Refused to connect to 'https://api.example.com/api/auth/login'
because it violates the following Content Security Policy directive: "connect-src 'self'"
```

**Fix:** Ensure API URL is in `connect-src`:
```typescript
"connect-src 'self' " + process.env.NUXT_PUBLIC_API_BASE_URL
```

**Verify environment variable:**
```bash
# Check .env or environment
echo $NUXT_PUBLIC_API_BASE_URL
```

### Issue: Images Not Loading

**Error in console:**
```
Refused to load the image 'https://cdn.example.com/logo.png'
because it violates the following Content Security Policy directive: "img-src 'self' data:"
```

**Fix:** Add the image source to `img-src`:
```typescript
"img-src 'self' data: https: https://cdn.example.com"
```

### Issue: Inline Styles Blocked

**Error in console:**
```
Refused to apply inline style because it violates CSP directive: "style-src 'self'"
```

**Fix:** CSP already includes `'unsafe-inline'` for styles. If still seeing this:
1. Check if CSP is being overridden elsewhere
2. Verify the CSP header is actually set (check Network tab)
3. Consider moving inline styles to CSS files (better practice)

### Issue: Development Tools Not Working

**Error:** Vue DevTools or other dev extensions not working

**Cause:** CSP may block browser extensions in development

**Fix:** Add `'unsafe-eval'` to `script-src` (already included in example)

---

## CSP Directives Explained

| Directive | Purpose | Your Setting |
|-----------|---------|--------------|
| `default-src` | Fallback for all resource types | `'self'` - only from same origin |
| `script-src` | Controls JavaScript sources | `'self' 'unsafe-inline' 'unsafe-eval'` |
| `style-src` | Controls CSS sources | `'self' 'unsafe-inline'` |
| `img-src` | Controls image sources | `'self' data: https:` |
| `font-src` | Controls font sources | `'self'` |
| `connect-src` | Controls fetch/XHR/WebSocket | `'self' [API_URL]` |
| `frame-ancestors` | Controls who can embed this page | `'none'` - prevent clickjacking |

### Special Keywords

- `'self'` - Same origin as the document
- `'none'` - Block everything
- `'unsafe-inline'` - Allow inline scripts/styles (less secure)
- `'unsafe-eval'` - Allow eval() and similar (less secure)
- `data:` - Allow data: URIs
- `https:` - Allow any HTTPS resource

---

## Advanced: Stricter CSP with Nonces

For production environments requiring maximum security, use CSP nonces instead of `'unsafe-inline'`:

### Option 1: Manual Nonce Implementation

```typescript
// nuxt.config.ts
export default defineNuxtConfig({
  hooks: {
    'render:response': (response, { event }) => {
      // Generate nonce for each request
      const nonce = crypto.randomBytes(16).toString('base64');

      // Add nonce to CSP header
      response.headers['Content-Security-Policy'] =
        `script-src 'self' 'nonce-${nonce}'; style-src 'self' 'nonce-${nonce}'`;

      // Make nonce available to templates
      event.context.cspNonce = nonce;
    }
  }
})
```

### Option 2: Use @nuxtjs/security Module

```bash
npm install @nuxtjs/security
```

```typescript
// nuxt.config.ts
export default defineNuxtConfig({
  modules: ['@nuxtjs/security'],

  security: {
    headers: {
      contentSecurityPolicy: {
        'script-src': ["'self'", "'nonce-{NONCE}'"],
        'style-src': ["'self'", "'nonce-{NONCE}'"],
      },
    },
  },
})
```

**Note:** Nonce implementation is more complex but provides better security. See [Nuxt Security docs](https://nuxt.com/modules/security) for details.

---

## Monitoring and Reporting

### CSP Report-Only Mode

Test CSP without breaking your site using report-only mode:

```typescript
headers: {
  // Use this instead of Content-Security-Policy during testing
  'Content-Security-Policy-Report-Only': [
    "default-src 'self'",
    // ... your CSP rules
  ].join('; ')
}
```

Violations are logged to console but not enforced.

### CSP Violation Reporting

Set up violation reporting to track CSP issues in production:

```typescript
headers: {
  'Content-Security-Policy': [
    "default-src 'self'",
    "report-uri /api/csp-violations",
    "report-to csp-endpoint",
    // ... other rules
  ].join('; ')
}
```

Then create an endpoint to collect reports (optional).

---

## Security Impact

CSP provides defense-in-depth against:

- ✅ **XSS attacks** - Prevents execution of injected scripts
- ✅ **Data exfiltration** - Blocks unauthorized network requests
- ✅ **Clickjacking** - `frame-ancestors 'none'` prevents embedding
- ✅ **Code injection** - Restricts script sources
- ✅ **Mixed content** - Can enforce HTTPS

**Important:** CSP doesn't fix XSS vulnerabilities, it **mitigates** them. Always prioritize preventing XSS through proper input validation and output escaping.

---

## References

- [MDN: Content Security Policy](https://developer.mozilla.org/en-US/docs/Web/HTTP/CSP)
- [CSP Evaluator (Google)](https://csp-evaluator.withgoogle.com/)
- [OWASP: Content Security Policy Cheat Sheet](https://cheatsheetseries.owasp.org/cheatsheets/Content_Security_Policy_Cheat_Sheet.html)
- [Nuxt Security Module](https://nuxt.com/modules/security)
- [Can I Use: CSP Browser Support](https://caniuse.com/contentsecuritypolicy)

---

## Quick Reference

### Frontend (nuxt.config.ts)
```typescript
nitro: {
  routeRules: {
    '/**': { headers: { /* CSP here */ } }
  }
}
```

### Backend (Program.cs)
```csharp
app.Use(async (context, next) => {
  context.Response.Headers.Append("Content-Security-Policy", "...");
  await next();
});
```

### Test Command
```bash
# Check CSP in browser
curl -I http://localhost:5173 | grep -i content-security
```

---

**Document Status:** Implementation Guide
**Last Updated:** December 2025
**Priority:** High - Primary XSS defense mechanism
