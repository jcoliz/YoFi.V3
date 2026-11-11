# Nuxt Front End

This project implements the application user interface (Front End).

## Development server troubleshooting

If `pnpm run dev` fails with `options.hostname.toLowerCase is not a function`,
an older Nuxt dev-server cache may contain an invalid hostname. From this
directory, remove the cached hint once and restart:

```powershell
Remove-Item .\node_modules\.cache\nuxt\dev-server.json
pnpm run dev
```

The configured hostname defaults to `localhost` and can be overridden with
`NUXT_HOST`, `NITRO_HOST`, `HOST`, or the Nuxt CLI `--host` option.

## Dependency compatibility

Keep `@vitejs/plugin-vue` compatible with the Vite versions used by Nuxt,
Vue Router, and Vitest. Vue Router 5 requires Vite 7.3 or 8, so the component
tests must not use `@vitejs/plugin-vue` 5 (which only supports Vite 5 and 6).
Use `pnpm install` after dependency changes and commit the updated lockfile.
Do not bypass dependency conflicts with `--force` or `--legacy-peer-deps`.
