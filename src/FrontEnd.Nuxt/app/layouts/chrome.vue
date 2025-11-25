<!--
  Chrome Layout

  Layout with site header and container, but without the route page header.
  Used for pages that want full control over their heading display.
-->
<template>
  <div data-test-id="default-layout">
    <SiteHeader />
    <main class="container">
      <slot />
    </main>
  </div>
</template>
