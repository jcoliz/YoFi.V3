export default defineEventHandler((event) => {
  const config = useRuntimeConfig()

  // Set content type to JSON so browser displays it instead of downloading
  setResponseHeader(event, 'Content-Type', 'application/json')

  return {
    status: 'healthy',
    timestamp: new Date().toISOString(),
    environment: process.env.NODE_ENV || 'development',
    version: config.public.solutionVersion,
  }
})
