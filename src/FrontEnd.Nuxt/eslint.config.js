// @ts-check
import withNuxt from './.nuxt/eslint.config.mjs'
import * as parserVue from 'vue-eslint-parser'
import * as parserTypeScript from '@typescript-eslint/parser'

export default withNuxt(
  // Override parser configuration for Vue files with TypeScript
  {
    files: ['**/*.vue', '**/*.ts'],
    languageOptions: {
      parser: parserVue,
      parserOptions: {
        parser: parserTypeScript,
        ecmaVersion: 'latest',
        sourceType: 'module',
      },
    },
    rules: {
      // Relax some rules for better DX
      'vue/multi-word-component-names': 'off',
      // Vue 3 supports multiple root elements in templates
      'vue/no-multiple-template-root': 'off',
    },
  },
)