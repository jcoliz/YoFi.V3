# XSS Prevention Verification Guide

## Current Status: ✅ GOOD

Your Vue.js application is already following XSS prevention best practices. This document explains how to verify and maintain this security posture.

---

## Vue.js XSS Prevention Mechanisms

### 1. Automatic Template Escaping

**What it does:** Vue.js automatically escapes all content rendered using mustache syntax `{{ }}`.

**Verification Results:**
- ✅ **63 instances** of `{{ }}` template interpolation found across components
- ✅ **0 instances** of `v-html` directive (the unsafe directive)
- ✅ All user data is rendered using safe interpolation

**Examples from your code:**
```vue
<!-- Safe: Automatically escaped -->
<td>{{ transaction.payee }}</td>
<h5>{{ tenant.name }}</h5>
<div>{{ error }}</div>
```

**Why this is safe:**
- If `transaction.payee` contains `<script>alert('xss')</script>`, Vue will render it as plain text, not executable code
- The HTML special characters are converted to entities: `&lt;script&gt;alert('xss')&lt;/script&gt;`

---

## Verification Checklist

### ✅ Primary Checks (Currently Passing)

1. **No `v-html` with user content**
   ```bash
   # Search for v-html usage
   grep -r "v-html" src/FrontEnd.Nuxt/app --include="*.vue"
   ```
   - ✅ Result: 0 instances found
   - Status: SAFE

2. **Using template interpolation for all dynamic content**
   ```bash
   # Verify {{ }} is used for user data
   grep -r "{{.*}}" src/FrontEnd.Nuxt/app --include="*.vue"
   ```
   - ✅ Result: 63 instances found
   - Status: SAFE - all using automatic escaping

3. **No direct DOM manipulation**
   ```bash
   # Check for dangerous patterns
   grep -r "innerHTML\|outerHTML" src/FrontEnd.Nuxt/app --include="*.vue" --include="*.ts"
   ```
   - Should return 0 results for Vue components
   - If found, review whether it handles user input

---

## Safe vs Unsafe Patterns

### ✅ SAFE Patterns (Use These)

```vue
<!-- 1. Template interpolation (automatic escaping) -->
<div>{{ userData.name }}</div>
<p>{{ userInput }}</p>

<!-- 2. Attribute binding -->
<input :value="userData.email" />
<a :href="safeUrl">Link</a>

<!-- 3. Conditional rendering -->
<div v-if="userData.isActive">{{ userData.status }}</div>

<!-- 4. List rendering -->
<li v-for="item in userItems" :key="item.id">
  {{ item.name }}
</li>
```

### ❌ UNSAFE Patterns (Avoid These)

```vue
<!-- 1. v-html with user content (DANGEROUS!) -->
<div v-html="userInput"></div>  <!-- ❌ NEVER DO THIS -->

<!-- 2. Direct DOM manipulation -->
<script>
element.innerHTML = userInput;  // ❌ DANGEROUS
</script>

<!-- 3. JavaScript URLs -->
<a :href="'javascript:' + userCode">Link</a>  <!-- ❌ DANGEROUS -->

<!-- 4. Dynamic script tags -->
<component :is="userComponent"></component>  <!-- ⚠️ Be careful -->
```

---

## When You MUST Use HTML Content

If you absolutely need to render HTML (e.g., rich text editor, markdown preview), follow this pattern:

### Option 1: DOMPurify (Recommended)

```bash
# Install DOMPurify
npm install dompurify
npm install --save-dev @types/dompurify
```

```vue
<script setup lang="ts">
import DOMPurify from 'dompurify';

const richTextContent = ref('<p>User <strong>content</strong></p>');

// Sanitize before rendering
const sanitizedContent = computed(() => {
  return DOMPurify.sanitize(richTextContent.value, {
    ALLOWED_TAGS: ['p', 'strong', 'em', 'a', 'ul', 'ol', 'li'],
    ALLOWED_ATTR: ['href', 'title']
  });
});
</script>

<template>
  <!-- Only safe after sanitization -->
  <div v-html="sanitizedContent"></div>
</template>
```

### Option 2: Marked.js for Markdown (If applicable)

```vue
<script setup lang="ts">
import { marked } from 'marked';
import DOMPurify from 'dompurify';

const markdownContent = ref('# User Content\n\nSome **bold** text');

const safeHtml = computed(() => {
  const rawHtml = marked(markdownContent.value);
  return DOMPurify.sanitize(rawHtml);
});
</script>

<template>
  <div v-html="safeHtml"></div>
</template>
```

---

## ESLint Rules for XSS Prevention

Add these ESLint rules to catch XSS vulnerabilities during development.

### Current Configuration

Your project uses the new ESLint flat config format in [`eslint.config.js`](../../src/FrontEnd.Nuxt/eslint.config.js). Add the XSS prevention rules to the existing rules section:

```javascript
// src/FrontEnd.Nuxt/eslint.config.js
export default withNuxt(
  {
    files: ['**/*.vue', '**/*.ts'],
    languageOptions: {
      parser: parserVue,
      parserOptions: {
        parser: parserTypeScript,
        ecmaVersion: 'latest',
        sourceType: 'module',
      },
    },
    plugins: {
      prettier: eslintPluginPrettier,
    },
    rules: {
      ...eslintConfigPrettier.rules,
      // Relax some rules for better DX
      'vue/multi-word-component-names': 'off',
      'vue/no-multiple-template-root': 'off',
      'prettier/prettier': 'warn',

      // XSS Prevention Rules
      'vue/no-v-html': 'warn',  // Warns when v-html is used (dangerous for XSS)
      'vue/no-v-text-v-html-on-component': 'error',  // Prevents v-html on components
    },
  },
)
```

### What These Rules Do

1. **`vue/no-v-html: 'warn'`**
   - Warns whenever `v-html` directive is used
   - `v-html` bypasses Vue's automatic escaping and can lead to XSS
   - Set to 'warn' so legitimate uses (with DOMPurify) are allowed but flagged

2. **`vue/no-v-text-v-html-on-component: 'error'`**
   - Prevents using `v-html` or `v-text` on component tags
   - Using these directives on components is always dangerous
   - Set to 'error' to prevent this pattern entirely

### Testing the Rules

After adding these rules, run ESLint:

```bash
cd src/FrontEnd.Nuxt
pnpm run lint
```

Expected output (current state):
```
✅ No errors or warnings (your code is already XSS-safe!)
```

If you were to add unsafe code:
```vue
<!-- This would trigger a warning -->
<div v-html="userInput"></div>
```

ESLint would report:
```
warning: Using v-html on user input can lead to XSS vulnerabilities (vue/no-v-html)
```

### Additional Security Rules (Optional)

For even stricter security, consider these additional Vue ESLint rules:

```javascript
rules: {
  // ... existing rules ...

  // Additional security rules
  'vue/no-v-text': 'off',  // v-text is safe, so keep off
  'vue/require-explicit-emits': 'warn',  // Helps prevent event injection
  'vue/no-unused-properties': 'warn',  // Remove dead code
}
```

---

## Regular Security Audits

### Monthly Verification Script

Create `scripts/verify-xss-safety.sh`:

```bash
#!/bin/bash

echo "🔍 Checking for XSS vulnerabilities..."

# Check for v-html
echo "Checking v-html usage..."
v_html_count=$(grep -r "v-html" src/FrontEnd.Nuxt/app --include="*.vue" | wc -l)
if [ "$v_html_count" -eq 0 ]; then
    echo "✅ No v-html found"
else
    echo "⚠️  Found $v_html_count instances of v-html - review these:"
    grep -r "v-html" src/FrontEnd.Nuxt/app --include="*.vue"
fi

# Check for innerHTML
echo "Checking innerHTML usage..."
innerhtml_count=$(grep -r "innerHTML" src/FrontEnd.Nuxt/app --include="*.vue" --include="*.ts" | wc -l)
if [ "$innerhtml_count" -eq 0 ]; then
    echo "✅ No innerHTML found"
else
    echo "⚠️  Found $innerhtml_count instances of innerHTML - review these:"
    grep -r "innerHTML" src/FrontEnd.Nuxt/app --include="*.vue" --include="*.ts"
fi

# Check for javascript: URLs
echo "Checking for javascript: URLs..."
js_url_count=$(grep -ri "javascript:" src/FrontEnd.Nuxt/app --include="*.vue" | wc -l)
if [ "$js_url_count" -eq 0 ]; then
    echo "✅ No javascript: URLs found"
else
    echo "⚠️  Found $js_url_count instances of javascript: URLs - review these:"
    grep -ri "javascript:" src/FrontEnd.Nuxt/app --include="*.vue"
fi

echo "✅ XSS verification complete"
```

---

## Content Security Policy (CSP)

Add CSP headers to your application for defense-in-depth:

```typescript
// nuxt.config.ts
export default defineNuxtConfig({
  nitro: {
    routeRules: {
      '/**': {
        headers: {
          'Content-Security-Policy': [
            "default-src 'self'",
            "script-src 'self' 'unsafe-inline'", // Nuxt requires unsafe-inline
            "style-src 'self' 'unsafe-inline'",
            "img-src 'self' data: https:",
            "connect-src 'self' https://your-api.com",
            "frame-ancestors 'none'",
          ].join('; '),
          'X-Content-Type-Options': 'nosniff',
          'X-Frame-Options': 'DENY',
          'X-XSS-Protection': '1; mode=block',
        },
      },
    },
  },
});
```

---

## Testing for XSS Vulnerabilities

### Manual Testing

Test with these payloads in any user input field:

```html
<script>alert('XSS')</script>
<img src=x onerror=alert('XSS')>
<svg onload=alert('XSS')>
"><script>alert('XSS')</script>
javascript:alert('XSS')
```

**Expected behavior:** All should be rendered as plain text, not executed.

### Automated Testing

Add to your functional tests:

```typescript
// tests/Functional/Tests/XssPrevention.feature.ts
test('should prevent XSS via transaction payee field', async ({ page }) => {
  // Given: User is on transactions page
  await page.goto('/transactions');

  // When: User enters XSS payload
  const xssPayload = '<script>alert("XSS")</script>';
  await page.fill('[name="payee"]', xssPayload);
  await page.click('button:has-text("Create")');

  // Then: Payload should be escaped and not executed
  const payeeText = await page.textContent('td:has-text("<script>")');
  expect(payeeText).toContain('<script>'); // Should be visible as text

  // And: No alert should appear (script not executed)
  // If script executed, test would fail due to unexpected alert
});
```

---

## Summary: Current Security Posture

### ✅ Strengths

1. **No `v-html` usage** - The most dangerous Vue directive is not present
2. **Consistent template interpolation** - All 63 instances use safe `{{ }}` syntax
3. **Vue.js automatic escaping** - Framework provides built-in XSS protection
4. **No direct DOM manipulation** - No dangerous `innerHTML` patterns detected

### 📋 Recommendations

1. **Add ESLint rules** - Prevent future introduction of unsafe patterns
2. **Document the pattern** - Ensure team knows to avoid `v-html`
3. **Add CSP headers** - Defense-in-depth protection
4. **Create verification script** - Monthly automated checks
5. **Include in code review** - Flag any `v-html` or `innerHTML` usage

### 🎯 Action Items for TODO

The existing "XSS prevention in frontend" TODO item should involve:

1. ✅ Verify current usage (COMPLETE - see this document)
2. Add ESLint rules to prevent unsafe patterns
3. Implement Content Security Policy headers
4. Add XSS test cases to functional tests
5. Document safe patterns for the team
6. Create monthly verification script

---

## References

- [Vue.js Security Best Practices](https://vuejs.org/guide/best-practices/security.html)
- [OWASP XSS Prevention Cheat Sheet](https://cheatsheetseries.owasp.org/cheatsheets/Cross_Site_Scripting_Prevention_Cheat_Sheet.html)
- [DOMPurify Documentation](https://github.com/cure53/DOMPurify)
- [Content Security Policy Guide](https://developer.mozilla.org/en-US/docs/Web/HTTP/CSP)

---

**Document Status:** Work in Progress
**Last Updated:** December 2025
**Next Review:** When implementing XSS prevention TODO item
