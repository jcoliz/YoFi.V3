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
