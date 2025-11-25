<!--
  Blank Layout

  Minimal centered layout without header or navigation.
  Used for login, registration, and other standalone pages.
-->
<template>
  <main
    data-test-id="blank-layout"
    class="min-vh-100 d-flex align-items-center justify-content-center"
  >
    <div class="container-fluid">
      <slot />
    </div>
  </main>
</template>
