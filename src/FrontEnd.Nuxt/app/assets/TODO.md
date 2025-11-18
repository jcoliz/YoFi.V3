# Fix me!

Using a version.txt file for injecting version number into the front end is unreliable. It requires the backend to build first. That doesn't always happen.

Better would be to inject it using normal means

1. Local development: Aspire host can get the version same way as the backend, and then leave it for us as an environment var.
1. Local container script: Build-Container.ps1 should run Get-Version, and then set SOLUTION_VERSION arg for building.
1. Container Build. Should set NUXT_PUBLIC_SOLUTION_VERSION to value of SOLUTION_FERSION
1. CD Build. Needs to set NUXT_PUBLIC_SOLUTION_VERSION in the build script env vars.

See: https://nuxt.com/docs/4.x/guide/going-further/runtime-config

```js
export default defineNuxtConfig({
  runtimeConfig: {
    public: {
        solutionVersion: '', // can be overridden by NUXT_SOLUTION_VERSION environment variable
    }
  },
})
```