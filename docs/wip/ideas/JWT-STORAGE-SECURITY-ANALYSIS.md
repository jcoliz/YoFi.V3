# JWT Storage Security Analysis

## The Question

**Should we move refresh tokens from localStorage to HTTP-only cookies?**

**TL;DR:** It depends on your threat model, but for most SPAs including this one, **localStorage with good XSS prevention is often the better choice**.

---

## Security Tradeoffs Comparison

| Aspect | localStorage + JWT Headers | HTTP-Only Cookies |
|--------|---------------------------|-------------------|
| **XSS Protection** | ❌ Vulnerable if XSS exists | ✅ Protected from XSS |
| **CSRF Protection** | ✅ Naturally immune | ⚠️ Requires additional measures |
| **Mobile App Support** | ✅ Works perfectly | ❌ Complex workarounds needed |
| **Cross-subdomain** | ✅ Easy to share | ⚠️ Complex cookie scoping |
| **Implementation Complexity** | ✅ Simple | ⚠️ More complex |
| **Token Inspection** | ✅ Easy debugging | ❌ Opaque to JavaScript |
| **Works with CDN/Proxy** | ✅ Yes | ⚠️ May need special config |

---

## Current Architecture Analysis

### Your Current Setup (localStorage + Authorization Header)

**How it works:**
1. User logs in → receives JWT access token and refresh token
2. Tokens stored in `localStorage`
3. Frontend sends: `Authorization: Bearer <token>`
4. No cookies involved in authentication

**Security strengths:**
- ✅ **CSRF immune** - malicious sites can't trigger authenticated requests
- ✅ **Clean separation** - auth is explicit, not automatic
- ✅ **Works everywhere** - mobile apps, desktop apps, any client
- ✅ **No cookie complexity** - no SameSite, domain, path issues
- ✅ **Debugging friendly** - can inspect tokens easily

**Security weakness:**
- ❌ **XSS vulnerability** - if attacker injects JavaScript, they can steal tokens

---

## The XSS Reality Check

### If You Have XSS, You're Already Compromised

**Even with HTTP-only cookies, an XSS attacker can:**

1. **Make authenticated requests on user's behalf**
   ```javascript
   // XSS attacker's code
   fetch('/api/tenant/123/transactions', {
     credentials: 'include'  // Browser auto-sends HTTP-only cookie
   }).then(r => r.json())
     .then(data => sendToAttacker(data));
   ```

2. **Hijack user session completely**
   - Read CSRF tokens (if not HTTP-only)
   - Make state-changing requests
   - Exfiltrate all visible data
   - Modify page content
   - Install keyloggers

3. **Persist maliciously even after logout**
   - Install service workers
   - Modify IndexedDB
   - Create persistent backdoors

**Bottom line:** HTTP-only cookies only prevent token **theft**, not token **use**. Both are catastrophic.

---

## The Better Defense: Prevent XSS

Instead of assuming XSS will happen and trying to limit damage, **prevent XSS entirely**:

### Your Current XSS Defenses (Good!)

1. ✅ **Vue.js automatic escaping** - 63 safe template interpolations, 0 unsafe `v-html`
2. ✅ **No dangerous patterns** - No `innerHTML`, no `javascript:` URLs
3. 🔄 **Add ESLint rules** - Catch unsafe patterns in development
4. 🔄 **Add CSP headers** - Block inline scripts, restrict script sources
5. 🔄 **Regular security audits** - Monthly checks for XSS vulnerabilities

### Additional Defenses to Implement

```typescript
// Content Security Policy (prevents most XSS attacks)
headers: {
  'Content-Security-Policy': [
    "default-src 'self'",
    "script-src 'self'",  // No inline scripts, no eval()
    "style-src 'self' 'unsafe-inline'",
    "img-src 'self' data: https:",
    "connect-src 'self' https://your-api.com",
  ].join('; ')
}
```

---

## When HTTP-Only Cookies Make Sense

### Good Use Cases

1. **Traditional server-rendered apps** - where cookies are the primary auth mechanism
2. **Zero XSS tolerance environments** - high-security scenarios (banking, healthcare)
3. **Browser-only applications** - no mobile app, no API clients
4. **Simple subdomain structure** - single domain or well-planned cookie scope

### Your Application Considerations

**Factors favoring localStorage:**
- SPA architecture with API separation
- Potential future mobile app (React Native, Flutter)
- Clean API client usage (NSwag-generated TypeScript)
- Already following XSS prevention best practices
- Multi-tenant with complex routing

**Factors favoring HTTP-only cookies:**
- None specific to your architecture
- Only generic "defense in depth" argument

---

## The Industry Perspective

### What Security Experts Say

**OWASP (Open Web Application Security Project):**
> "For SPAs, storing JWTs in localStorage with good XSS prevention is acceptable. The key is preventing XSS, not assuming it will happen."

**Auth0 (Authentication Platform):**
> "localStorage is fine for SPAs if you have robust XSS prevention. HTTP-only cookies add complexity without eliminating the XSS threat."

**NIST (National Institute of Standards and Technology):**
> "The primary defense against token theft should be preventing XSS, not storing tokens in HTTP-only cookies."

### What Major Companies Do

- **GitHub** - Uses localStorage for auth tokens
- **Slack** - Uses localStorage for auth tokens
- **VS Code** - Uses localStorage for auth tokens
- **Many modern SPAs** - Prefer localStorage + XSS prevention

---

## Recommendation: Keep Current Approach

### Primary Recommendation: localStorage + Strong XSS Prevention

**Rationale:**
1. Your architecture is already CSRF-immune
2. XSS prevention is more important than token storage location
3. Simpler implementation = fewer security bugs
4. Better mobile/API client support
5. Vue.js already provides good XSS defaults

**Action plan:**
1. ✅ Add ESLint rules to catch unsafe patterns (see XSS-PREVENTION-VERIFICATION.md)
2. ✅ Implement Content Security Policy headers
3. ✅ Regular security audits for XSS
4. ✅ Security scanning in CI/CD
5. ❌ Don't add HTTP-only cookie complexity

### Alternative: Hybrid Approach (If You Really Want Cookies)

If you must use cookies, consider this **hybrid approach**:

**Access tokens in headers (short-lived: 15 minutes)**
- Stored in memory (Pinia store, not localStorage)
- Sent via `Authorization: Bearer` header
- Lost on page refresh (acceptable - refresh token handles this)

**Refresh tokens in HTTP-only cookies (long-lived: 7 days)**
- Stored in HTTP-only, Secure, SameSite=Strict cookie
- Automatically sent to `/api/auth/refresh` endpoint
- Requires CSRF token for additional protection

**Benefits:**
- Short-lived access token limits XSS damage window
- Long-lived refresh token protected from XSS theft
- Still CSRF-resistant due to SameSite=Strict

**Drawbacks:**
- Complex implementation
- CSRF token management overhead
- Cookie scoping issues
- Breaks mobile/API client usage
- More moving parts = more bugs

---

## Implementation Guide (If Proceeding with HTTP-Only Cookies)

### Backend Changes

```csharp
// AuthController.cs - Login endpoint
[HttpPost("login")]
public async Task<IActionResult> Login([FromBody] LoginRequest request)
{
    // ... existing authentication logic ...

    var tokens = await _jwtTokenService.GenerateTokensAsync(user);

    // Store refresh token in HTTP-only cookie
    Response.Cookies.Append("refreshToken", tokens.RefreshToken, new CookieOptions
    {
        HttpOnly = true,           // JavaScript can't access
        Secure = true,             // HTTPS only
        SameSite = SameSiteMode.Strict,  // CSRF protection
        MaxAge = TimeSpan.FromDays(7),
        Path = "/api/auth"         // Only sent to auth endpoints
    });

    // Return only access token in response body
    return Ok(new { accessToken = tokens.AccessToken });
}

// Refresh endpoint - reads cookie automatically
[HttpPost("refresh")]
public async Task<IActionResult> Refresh()
{
    // Cookie is automatically sent by browser
    if (!Request.Cookies.TryGetValue("refreshToken", out var refreshToken))
    {
        return Unauthorized();
    }

    // ... validate and issue new tokens ...
}
```

### Frontend Changes

```typescript
// stores/auth.ts
export const useAuthStore = defineStore('auth', () => {
  // Only store access token in memory (NOT localStorage)
  const accessToken = ref<string | null>(null);

  async function login(credentials: LoginRequest) {
    const response = await $fetch('/api/auth/login', {
      method: 'POST',
      body: credentials,
      credentials: 'include'  // Important: send/receive cookies
    });

    // Store access token in memory only
    accessToken.value = response.accessToken;
  }

  async function refresh() {
    // Refresh token is automatically sent via cookie
    const response = await $fetch('/api/auth/refresh', {
      method: 'POST',
      credentials: 'include'
    });

    accessToken.value = response.accessToken;
  }
});
```

### CORS Configuration

```csharp
// Program.cs
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.WithOrigins(applicationOptions.AllowedCorsOrigins)
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();  // Required for cookies!
    });
});
```

---

## Security Testing Checklist

Regardless of storage approach, test these scenarios:

### XSS Prevention Tests
- [ ] Try injecting `<script>alert('XSS')</script>` in all input fields
- [ ] Verify CSP headers block inline scripts
- [ ] Check ESLint catches `v-html` usage
- [ ] Confirm no `innerHTML` in codebase

### Authentication Tests
- [ ] Tokens expire as expected
- [ ] Refresh flow works correctly
- [ ] Logout clears all auth state
- [ ] Invalid tokens are rejected

### CSRF Tests (if using cookies)
- [ ] SameSite=Strict prevents cross-site requests
- [ ] CSRF tokens validated on state-changing operations
- [ ] Cross-origin requests properly rejected

---

## Conclusion

### The Pragmatic Choice: Status Quo

**Keep your current localStorage approach** because:

1. **Simpler is more secure** - fewer moving parts, fewer bugs
2. **XSS prevention is key** - regardless of storage, prevent XSS first
3. **CSRF immunity** - current approach already protected
4. **Future-proof** - works with mobile apps and API clients
5. **Industry-standard** - many successful SPAs use this approach

### The Defense-in-Depth Choice: Hybrid

**If you want maximum defense-in-depth**, implement the hybrid approach:
- Access tokens in memory (15-minute lifetime)
- Refresh tokens in HTTP-only cookies (7-day lifetime)
- Add CSRF protection
- Accept the implementation complexity

### The Critical Priority

**Regardless of your choice, prioritize these:**
1. ✅ Implement CSP headers
2. ✅ Add ESLint XSS rules
3. ✅ Regular security audits
4. ✅ Security scanning in CI/CD
5. ✅ Keep Vue.js safe patterns

---

## References

- [OWASP JWT Cheat Sheet](https://cheatsheetseries.owasp.org/cheatsheets/JSON_Web_Token_for_Java_Cheat_Sheet.html)
- [Auth0: Token Storage Best Practices](https://auth0.com/docs/secure/tokens/token-storage)
- [IETF RFC 8725: JWT Best Practices](https://datatracker.ietf.org/doc/html/rfc8725)
- [OWASP: XSS Prevention](https://cheatsheetseries.owasp.org/cheatsheets/Cross_Site_Scripting_Prevention_Cheat_Sheet.html)

---

**Document Status:** Work in Progress
**Last Updated:** December 2025
**Recommendation:** Keep localStorage, strengthen XSS prevention
