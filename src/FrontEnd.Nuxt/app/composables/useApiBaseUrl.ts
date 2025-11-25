/**
 * API Base URL Composable
 *
 * Provides the configured API base URL from runtime configuration.
 * Validates that the URL is properly configured and throws an error if not.
 *
 * @returns {Object} Object containing the base URL string
 * @throws {Error} If API base URL is not configured as a string
 */
export const useApiBaseUrl = () => {
  const config = useRuntimeConfig()

  let baseUrl: string
  if (typeof config.public.apiBaseUrl === 'string') {
    baseUrl = config.public.apiBaseUrl
  } else {
    console.error('API base URL is not a string')
    throw new Error('API base URL is not a string')
  }

  console.log('API Base URL:', baseUrl)

  return {
    baseUrl: baseUrl,
  }
}
