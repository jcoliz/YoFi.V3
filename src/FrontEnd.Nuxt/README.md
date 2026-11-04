# Nuxt Front End

This project implements the application user interface (Front End).

## Card titles

Use `CardTitle` for a consistent card heading and optional muted subtitle.
The `title` prop is required; `subtitle` is optional. The component adds no
bottom spacing; callers control it with classes such as `mb-3` or `pb-3`.

```vue
<CardTitle
  title="Upload Files"
  subtitle="Select OFX or QFX files from your bank to import transactions."
  class="mb-3"
/>
```
