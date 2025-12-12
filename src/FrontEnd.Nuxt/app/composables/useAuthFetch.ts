/**
 * Composable to create an auth-aware fetch wrapper for NSwag-generated API clients
 *
 * This wraps Nuxt's $fetch to work with the generated API clients, ensuring:
 * - Authentication tokens are automatically included
 * - Tokens are automatically refreshed when expired
 * - Network errors are properly handled
 */
export function useAuthFetch() {
  const { token } = useAuth()

  // Create a wrapper that matches the interface expected by NSwag clients
  const authFetch = {
    fetch: async (url: string | Request, init?: globalThis.RequestInit): Promise<Response> => {
      // Convert Request object to string URL if needed
      const urlString = typeof url === 'string' ? url : url.url

      // Use Nuxt's $fetch which handles auth automatically
      try {
        const response = await $fetch.raw(urlString, {
          method: (init?.method || 'GET') as any,
          body: init?.body as any,
          headers: {
            ...init?.headers,
            // @sidebase/nuxt-auth automatically adds Authorization header
            // but we can ensure it's included explicitly if needed
            ...(token.value ? { Authorization: token.value } : {}),
          },
        })

        // Convert Nuxt's response to standard Response object
        return new Response(JSON.stringify(response._data), {
          status: response.status,
          statusText: response.statusText,
          headers: response.headers,
        })
      } catch (error: any) {
        // Handle fetch errors and convert to Response for NSwag client
        const status = error.response?.status || 500
        const statusText = error.response?.statusText || 'Internal Server Error'

        // If there's a response body, use it; otherwise create RFC 7807 Problem Details
        let body = error.response?._data || error.data
        if (!body) {
          // Network error or no response - create proper Problem Details
          body = {
            type: 'about:blank',
            title: 'Network Error',
            status,
            detail: error.message || 'Failed to connect to the server',
            instance: urlString,
          }
        }

        return new Response(JSON.stringify(body), {
          status,
          statusText,
          headers: error.response?.headers || {},
        })
      }
    },
  }

  return authFetch
}
