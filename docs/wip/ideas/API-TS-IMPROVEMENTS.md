## Prompt

> I would like some advice on my generated TS client for backend API. Using NSwag, I generate TS clients to match my backend API. This works great to keep everything in sync.
>
> My question is what performance penalty am I paying in production  for having all the clients in one almost 10,000 line file? Would it be far better to divide each client into its own file?
>
> Typically a .vue file only loads one client, and a very small number of them load two, but no more than that.
>
> I don't understand TS runtime in production well enough to understand the trade-offs.

## Short answer

There is no "TS runtime" in production. TypeScript is erased at build time; what ships is JavaScript produced by Vite/Rollup during `npm run generate`. The question is therefore purely about **bundle size and chunking**, and the honest answer is: splitting [`apiclient.ts`](src/FrontEnd.Nuxt/app/utils/apiclient.ts) into one file per client would gain you **very little**, because the bundler already does most of that work for you.

## What actually happens to that 9,455-line file

1. **Tree-shaking works at the export level, not the file level.** Rollup sees `import { TransactionsClient, type IProblemDetails } from '~/utils/apiclient'` and only keeps `TransactionsClient` plus whatever it transitively references (`ApiException`, `throwException`, the DTO classes it calls `.fromJS()` on). `ImportClient`, `ReportsClient`, etc. are dropped from that page's chunk if nothing in the build imports them. ES module `export class` declarations are statically analyzable and side-effect free, so this works well for NSwag output.

2. **`import type` costs zero bytes.** Roughly a third of your imports (`IProblemDetails`, `IPaginationMetadata`, `ReportResultDto` in `reports/Table.vue`, etc.) are type-only and vanish entirely.

3. **Shared code gets hoisted to a common chunk.** Because many pages import from the same module, Rollup will typically place the surviving pieces of `apiclient.ts` into one shared chunk that's loaded once and cached, rather than duplicating `ApiException`/`throwException` into every page chunk. Splitting into per-client files would change this only marginally.

4. **Nuxt pages are already lazy-loaded.** Each page in `app/pages/` is a separate async chunk. So the transactions page's chunk carries `TransactionsClient`; the reports page's chunk carries `ReportsClient`. The user downloads them on navigation, not up front.

So the "10,000 lines" number is misleading. The real question is "how many of those lines survive after shaking and how many are in the entry chunk?" — and that's measurable, not something to guess at.

## Where the real size cost is (and it's not the file layout)

Looking at [`nswag.json`](tools/WireApiHost/nswag.json:57), the lever that matters is `"typeStyle": "Class"`. Every DTO is emitted as a class with `constructor`, `init`, `fromJS`, and `toJSON` (see [`TenantRoleResultDto`](src/FrontEnd.Nuxt/app/utils/apiclient.ts:8649) — ~50 lines of runtime code for 5 properties). Those classes **are not shakeable** when a client's `process*` method calls `Foo.fromJS(...)`, so every DTO reachable from a used client ships. That's likely 60–70% of the surviving bytes. Setting `typeStyle` to `Interface` would eliminate nearly all of it, but it's a code change (you'd lose `new TransactionEditDto({...})` and `.toJSON()` on the frontend, and `Date` fields would arrive as strings), so it's a trade-off, not a free win.

The second cheap win: [`errordisplay-test.vue`](src/FrontEnd.Nuxt/app/pages/errordisplay-test.vue:9) and [`weather.vue`](src/FrontEnd.Nuxt/app/pages/weather.vue:11) pull `TestControlClient` (~1,350 lines, your largest client) and `WeatherClient` into the production build. They're lazy page chunks so they don't hurt first paint, but they're dead weight in the deployed artifact.

## Trade-offs if you did split the file

| | Single file (current) | One file per client |
|---|---|---|
| Bytes shipped | Same after tree-shaking | Same after tree-shaking |
| Build correctness | Guaranteed consistent | Shared DTOs/`ApiException` must live in a common file; NSwag's `MultipleClientsFromOperationId` mode doesn't emit multiple files natively — you'd run NSwag once per controller with `includedOperationIds` filters, or post-process the output |
| Dev experience | One import path, IDE handles 10k lines fine | Need to know which file holds which DTO |
| Generation script | One step | N steps + a shared-types step, and type duplication risk across files |

Net: more moving parts in [`Generate-ApiClient.ps1`](scripts/Generate-ApiClient.ps1) and the WireApiHost MSBuild target for essentially zero production benefit.

## Recommendation

Do not split the file. Instead, in order of value:

1. Run `npx nuxi analyze` in `src/FrontEnd.Nuxt` once to see the real chunk sizes. If `apiclient` contributes under ~30–40 KB gzipped to the shared chunk (my expectation), stop there.
2. Keep test-only pages out of production. Options: gate them in `nuxt.config.ts` via `hooks: { 'pages:extend' }` filtering by `process.env.NODE_ENV`, or move them behind a Nuxt layer / `ignore` pattern used only in dev.
3. Only if analyze shows DTO classes dominating and you care about the delta: evaluate `typeStyle: Interface`, scoping the frontend refactor it would require.

Splitting the generated client (todo item 4) should remain a last resort gated on measured evidence.
