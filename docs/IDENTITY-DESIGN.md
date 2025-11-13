# Identity Design

This file collects implementation ideas for Identity, following the questions posted in [ADR 0008](./adr/0008-identity.md).

## Enhanced JWT with Custom Claims

jcoliz: Let's think about this ADR some more. I want to include claims in the JWT controlling access to certain resources. Can I view those from within the Nuxt app with this approach?

GitHub Copilot: Yes, absolutely! With the JWT + `@sidebase/nuxt-auth` approach, you can easily include custom claims in the JWT and access them from within your Nuxt app. Let me show you how to implement this for your account-level authorization requirements. 

Read [](file:///c%3A/Source/jcoliz/YoFi.V3/docs/adr/0008-identity.md#170-170), lines 170 to 190

Perfect! Here's how you can include custom claims for account access in your JWT tokens and view them in the Nuxt app:


### Backend: Include Account Claims in JWT

First, modify your `GenerateJwtToken` method to include account access claims:

````csharp
private async Task<string> GenerateJwtToken(ApplicationUser user)
{
    var tokenHandler = new JwtSecurityTokenHandler();
    var key = Encoding.ASCII.GetBytes(_configuration["Jwt:Key"]);
    
    // Get user's accessible accounts from database
    var userAccounts = await GetUserAccountAccess(user.Id);
    
    var claims = new List<Claim>
    {
        new Claim(ClaimTypes.NameIdentifier, user.Id),
        new Claim(ClaimTypes.Email, user.Email),
        new Claim(ClaimTypes.Name, user.UserName ?? user.Email),
        new Claim("sub", user.Id), // Standard JWT claim
        new Claim("email", user.Email),
        new Claim("name", user.UserName ?? user.Email)
    };
    
    // Add account access claims
    foreach (var account in userAccounts)
    {
        claims.Add(new Claim("account_access", account.AccountId));
        claims.Add(new Claim($"account_role_{account.AccountId}", account.Role)); // e.g., "owner", "viewer", "editor"
    }
    
    // Add any other custom claims
    claims.Add(new Claim("user_preferences", user.Preferences ?? "{}"));
    
    var tokenDescriptor = new SecurityTokenDescriptor
    {
        Subject = new ClaimsIdentity(claims),
        Expires = DateTime.UtcNow.AddHours(24),
        Issuer = _configuration["Jwt:Issuer"],
        Audience = _configuration["Jwt:Audience"],
        SigningCredentials = new SigningCredentials(
            new SymmetricSecurityKey(key), 
            SecurityAlgorithms.HmacSha256Signature)
    };
    
    var token = tokenHandler.CreateToken(tokenDescriptor);
    return tokenHandler.WriteToken(token);
}

private async Task<List<UserAccountAccess>> GetUserAccountAccess(string userId)
{
    // This would query your database for user-to-account relationships
    // Example structure:
    return await _context.UserAccountAccess
        .Where(uaa => uaa.UserId == userId)
        .Select(uaa => new UserAccountAccess 
        { 
            AccountId = uaa.AccountId, 
            Role = uaa.Role 
        })
        .ToListAsync();
}
````

### Frontend: Configure Auth to Expose Claims

Update your Nuxt auth configuration to properly expose the JWT claims:

````typescript
import CredentialsProvider from '@auth/core/providers/credentials'
import { NuxtAuthHandler } from '#auth'
import jwt from 'jsonwebtoken'

export default NuxtAuthHandler({
  secret: useRuntimeConfig().authSecret,
  providers: [
    CredentialsProvider({
      name: 'credentials',
      credentials: {
        email: { label: 'Email', type: 'email' },
        password: { label: 'Password', type: 'password' }
      },
      async authorize(credentials) {
        try {
          const response = await $fetch(`${process.env.NUXT_PUBLIC_API_BASE_URL}/api/auth/login`, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({
              email: credentials.email,
              password: credentials.password
            })
          })

          if (response.token) {
            // Decode the JWT to access claims on the frontend
            const decodedToken = jwt.decode(response.token) as any
            
            return {
              id: decodedToken.sub || decodedToken.nameid,
              email: decodedToken.email,
              name: decodedToken.name,
              accessToken: response.token,
              // Include custom claims in the session
              accountAccess: decodedToken.account_access || [],
              accountRoles: Object.keys(decodedToken)
                .filter(key => key.startsWith('account_role_'))
                .reduce((acc, key) => {
                  const accountId = key.replace('account_role_', '')
                  acc[accountId] = decodedToken[key]
                  return acc
                }, {} as Record<string, string>),
              userPreferences: decodedToken.user_preferences ? 
                JSON.parse(decodedToken.user_preferences) : {}
            }
          }
          return null
        } catch (error) {
          console.error('Auth error:', error)
          return null
        }
      }
    })
  ],
  callbacks: {
    async jwt({ token, user }) {
      if (user) {
        // Preserve all user data including claims
        return {
          ...token,
          ...user
        }
      }
      return token
    },
    async session({ session, token }) {
      // Make claims available in the session
      session.user.id = token.id
      session.user.accountAccess = token.accountAccess
      session.user.accountRoles = token.accountRoles
      session.user.userPreferences = token.userPreferences
      session.accessToken = token.accessToken
      return session
    }
  }
})
````

### Frontend: Access Claims in Components

Now you can easily access the claims throughout your Nuxt app:

````typescript
export const useAccountAccess = () => {
  const { data: session } = useAuth()

  const hasAccountAccess = (accountId: string): boolean => {
    return session.value?.user?.accountAccess?.includes(accountId) || false
  }

  const getAccountRole = (accountId: string): string | null => {
    return session.value?.user?.accountRoles?.[accountId] || null
  }

  const canEditAccount = (accountId: string): boolean => {
    const role = getAccountRole(accountId)
    return role === 'owner' || role === 'editor'
  }

  const canViewAccount = (accountId: string): boolean => {
    return hasAccountAccess(accountId)
  }

  const accessibleAccounts = computed(() => {
    return session.value?.user?.accountAccess || []
  })

  const userPreferences = computed(() => {
    return session.value?.user?.userPreferences || {}
  })

  return {
    hasAccountAccess,
    getAccountRole,
    canEditAccount,
    canViewAccount,
    accessibleAccounts,
    userPreferences
  }
}
````

### Usage in Vue Components

````vue
<!-- filepath: c:\Source\jcoliz\YoFi.V3\src\FrontEnd.Nuxt\pages\accounts\[id].vue -->
<template>
  <div>
    <h1>Account Dashboard</h1>
    
    <!-- Show different content based on role -->
    <div v-if="canEditAccount(accountId)">
      <button @click="editAccount">Edit Account</button>
      <button @click="deleteTransaction">Delete Transaction</button>
    </div>
    
    <div v-else-if="canViewAccount(accountId)">
      <p>You have read-only access to this account</p>
    </div>
    
    <div v-else>
      <p>You don't have access to this account</p>
    </div>

    <!-- List only accessible accounts -->
    <select v-model="selectedAccount">
      <option v-for="accountId in accessibleAccounts" :key="accountId" :value="accountId">
        Account {{ accountId }}
      </option>
    </select>
  </div>
</template>

<script setup>
const route = useRoute()
const accountId = route.params.id as string

const { 
  canEditAccount, 
  canViewAccount, 
  accessibleAccounts 
} = useAccountAccess()

const selectedAccount = ref('')

// Redirect if no access
if (!canViewAccount(accountId)) {
  throw createError({
    statusCode: 403,
    statusMessage: 'Access denied to this account'
  })
}
</script>
````

### Middleware for Route Protection

Create middleware to automatically protect routes based on claims:

````typescript
export default defineNuxtRouteMiddleware((to) => {
  const { hasAccountAccess } = useAccountAccess()
  const accountId = to.params.id as string

  if (accountId && !hasAccountAccess(accountId)) {
    throw createError({
      statusCode: 403,
      statusMessage: `Access denied to account ${accountId}`
    })
  }
})
````

### Real-time Claims Updates

If account access can change while the user is logged in, you can create a method to refresh the token:

````typescript
export const useAuthRefresh = () => {
  const { data: session, update } = useAuth()

  const refreshUserClaims = async () => {
    try {
      const authFetch = useAuthenticatedFetch()
      const response = await authFetch('/api/auth/refresh-token', {
        method: 'POST'
      })
      
      if (response.token) {
        // Decode new token and update session
        const decodedToken = jwt.decode(response.token) as any
        
        await update({
          ...session.value,
          user: {
            ...session.value?.user,
            accountAccess: decodedToken.account_access || [],
            accountRoles: /* parse roles from token */
          },
          accessToken: response.token
        })
      }
    } catch (error) {
      console.error('Failed to refresh claims:', error)
    }
  }

  return { refreshUserClaims }
}
````

## Key Benefits of This Approach

✅ **Client-side Authorization**: No need to call the API to check permissions  
✅ **Performance**: Claims are cached in the JWT, reducing database calls  
✅ **Security**: Claims are cryptographically signed and tamper-proof  
✅ **Flexibility**: Easy to add new claim types without frontend changes  
✅ **Real-time**: Can refresh claims when permissions change  
✅ **Offline-friendly**: Works even when temporarily disconnected  

## Security Considerations

1. **JWT Size**: Don't put too many claims in the JWT to keep it lightweight
2. **Sensitive Data**: Don't include sensitive information in claims (they're base64 encoded, not encrypted)
3. **Expiration**: Use reasonable token expiration times (1-24 hours)
4. **Refresh Strategy**: Implement token refresh for long-lived sessions
5. **Backend Validation**: Always validate claims on the backend for sensitive operations

This approach gives you powerful, flexible authorization that works seamlessly between your ASP.NET Core backend and Nuxt frontend!