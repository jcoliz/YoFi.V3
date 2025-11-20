import controls from '@coliz/vue-base-controls'

export default defineNuxtPlugin((nuxtApp) => {
  console.log('[DEBUG] vue-base-controls plugin: Loading')
  console.log('[DEBUG] vue-base-controls: Controls object:', controls)
  console.log('[DEBUG] vue-base-controls: Available components:', Object.keys(controls))

  nuxtApp.vueApp.use(controls)

  console.log('[DEBUG] vue-base-controls: Plugin installed')
  console.log(
    '[DEBUG] vue-base-controls: Global components:',
    Object.keys(nuxtApp.vueApp._context.components || {}),
  )
})
