type ApiClientConstructor<T> = new (
  baseUrl?: string,
  http?: {
    fetch(url: RequestInfo, init?: RequestInit): Promise<Response>
  },
) => T

/**
 * Creates an API client with the validated base URL and auth-aware fetch.
 *
 * @param ClientClass The NSwag-generated API client class to instantiate
 * @param useTokenCheck Whether to refresh tokens and include the authorization header
 * @returns A configured instance of the supplied client class
 *
 * @example
 * const tenantClient = useApiClient(TenantClient)
 */
export function useApiClient<T>(ClientClass: ApiClientConstructor<T>, useTokenCheck = true): T {
  const { baseUrl } = useApiBaseUrl()
  const authFetch = useAuthFetch(useTokenCheck)

  return new ClientClass(baseUrl, authFetch)
}
