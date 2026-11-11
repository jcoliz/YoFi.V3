<!--
  Default Layout

  Standard page layout with site header, container, and route-based page header.
  Used for most authenticated pages in the application.
-->
<template>
  <div data-test-id="default-layout">
    <SiteHeader />
    <main class="container">
      <PageBreadcrumb />
      <RoutePageHeader />
      <slot />
    </main>
  </div>
</template>
