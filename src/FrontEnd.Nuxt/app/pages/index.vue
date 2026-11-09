<script setup lang="ts">
definePageMeta({
  title: 'Home',
  order: 1,
  layout: 'chrome',
  auth: false,
})
</script>

<template>
  <div
    class="container-fluid"
    data-test-id="brochure-section"
  >
    <!-- Hero Section -->
    <div class="row bg-primary text-white py-5 mb-5">
      <div class="col-12 text-center">
        <div class="container">
          <h1 class="display-4 fw-bold mb-3">YoFi.V3 Reference Implementation</h1>
          <p class="lead mb-4">
            A modern .NET 10 + Nuxt 4 stack with authentication, authorization, and best practices
          </p>
          <p class="mb-4">
            This reference implementation demonstrates integrating .NET 10, Nuxt 4, authentication,
            and best practices for building reliable, scalable web applications.
          </p>
        </div>
      </div>
    </div>

    <div class="container mb-5">
      <div class="row">
        <div class="col-lg-10 col-xl-8 mx-auto">
          <h2 class="display-5 mb-4">Get Started</h2>
          <p class="lead mb-4">
            YoFi.V3 is very early in development, in an early Alpha state, with more functionality to
            follow. See the
            <a
              href="https://github.com/jcoliz/YoFi.V3/blob/main/docs/PRODUCT-ROADMAP.md"
              target="_blank"
              rel="noopener"
              >product roadmap</a
            >
            for what's available and what's planned. Take a 15-minute tour from sample transactions to
            updated financial reports, with automatic categorization along the way.
          </p>

          <div class="alert alert-warning">
            <strong>Evaluation only.</strong> Use sample data and a disposable password, not real
            financial information. Stopping the database container clears your accounts and data.
          </div>

          <ol class="ps-4">
            <li class="mb-3">
              <strong>Request an account, then sign in.</strong>&nbsp;
              <NuxtLink to="/register">Request an account</NuxtLink> with a unique username.
              Requests are automatically approved for this evaluation, even though the
              acknowledgment mentions a review. Select Continue, then
              <NuxtLink to="/login">sign in</NuxtLink> with your username and password.
            </li>
            <li class="mb-3">
              <strong>Find your workspace.</strong>
              Open <NuxtLink to="/workspaces">Workspaces</NuxtLink>: your personal workspace is
              already provisioned, and you are its Owner. Select Manage to explore its settings and
              data tools. Leave the delete actions alone.
            </li>
            <li class="mb-3">
              <strong>Load some history.</strong>
              Under Sample Data, keep Personal Finance and the default dates. Select Load Sample
              Data, confirm, and wait for the transaction and rule counts. Load it only once, and
              use this same workspace throughout the tour.
            </li>
            <li class="mb-3">
              <strong>Explore and search.</strong>
              Open <NuxtLink to="/transactions">Transactions</NuxtLink>, browse a few pages, and
              open a row for details. Try searching for a word from a payee or category; search also
              covers memos and amounts. Clear the search afterward.
            </li>
            <li class="mb-3">
              <strong>See the starting picture.</strong>
              Visit <NuxtLink to="/reports">Reports</NuxtLink> and try Expenses, Income, and
              Expenses Detail. Note a category total and the monthly amounts in Expenses Detail so
              you can compare after importing. Reports show the current year.
            </li>
            <li class="mb-3">
              <strong>Get a sample bank download.</strong>
              Return to <NuxtLink to="/workspaces">Workspaces</NuxtLink> and Manage your workspace.
              Under Download sample bank data, keep the default dates and select Download Sample
              OFX. This simulates a bank download with more recent activity; it does not change your
              ledger.
            </li>
            <li class="mb-3">
              <strong>Upload and review, but don't accept yet.</strong>
              Open <NuxtLink to="/import">Import</NuxtLink>, choose the downloaded OFX file, and
              select Upload. New transactions are selected by default; duplicates are deselected.
              Notice the categories are already filled in automatically.
            </li>
            <li class="mb-3">
              <strong>Discover the automation.</strong>
              Visit <NuxtLink to="/payee-rules">Rules</NuxtLink> and find a payee from the review.
              The sample rules match payee text to categories, saving you manual work. Your pending
              import stays available while you look around.
            </li>
            <li class="mb-3">
              <strong>Accept the import.</strong>
              Return to <NuxtLink to="/import">Import</NuxtLink>, select Import, check the workspace
              and counts, then confirm. Selected rows enter your ledger; unselected rows are
              discarded and the review queue is cleared. Find the new activity in
              <NuxtLink to="/transactions">Transactions</NuxtLink>, with its categories intact.
            </li>
            <li class="mb-3">
              <strong>See what changed.</strong>
              Return to <NuxtLink to="/reports">Reports</NuxtLink> and choose Expenses Detail again.
              Compare the category totals and recent months, not the number of rows. Your newly
              accepted activity now contributes to the report. Try All Detail for the wider picture.
            </li>
          </ol>

          <div class="mt-4">
            <p class="text-muted">
              Have feedback or questions?
              <a
                href="https://github.com/jcoliz/YoFi.V3/issues"
                target="_blank"
                rel="noopener"
              >
                Open an issue
              </a>
              or start a
              <a
                href="https://github.com/jcoliz/YoFi.V3/discussions"
                target="_blank"
                rel="noopener"
              >
                discussion on GitHub </a
              >.
            </p>
          </div>
        </div>
      </div>
    </div>
  </div>
</template>
