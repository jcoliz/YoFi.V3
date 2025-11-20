export default defineEventHandler(() => {
  const config = useRuntimeConfig()
  
  return {
    status: 'healthy',
    timestamp: new Date().toISOString(),
    environment: process.env.NODE_ENV || 'development',
    version: config.public.solutionVersion,
  }
})