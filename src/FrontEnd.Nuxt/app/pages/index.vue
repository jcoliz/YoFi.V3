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
        <div class="col-lg-11 col-xl-10 mx-auto">
          <h2 class="display-5 fw-semibold mb-4">Get Started</h2>
          <div class="d-flex flex-wrap gap-2 mb-3">
            <span class="badge rounded-pill text-bg-primary">Evaluator tour</span>
            <span class="badge rounded-pill text-bg-light border">Early Alpha</span>
            <span class="badge rounded-pill text-bg-light border">About 15 minutes</span>
          </div>
          <p class="lead mb-4">
            YoFi.V3 is very early in development, in an early Alpha state, with more functionality
            to follow. See the
            <a
              href="https://github.com/jcoliz/YoFi.V3/blob/main/docs/PRODUCT-ROADMAP.md"
              target="_blank"
              rel="noopener"
              >product roadmap</a
            >
            for what's available and what's planned. To see what's available today, take our
            15-minute tour from sample transactions to updated financial reports, with automatic
            categorization along the way.
          </p>

          <div class="alert alert-warning evaluation-notice mb-4">
            <strong>Evaluation only.</strong> Use sample data and a disposable password, not real
            financial information. Stopping the database container clears your accounts and data.
          </div>

          <ol class="evaluation-journey">
            <li>
              <strong>Request an account, then sign in.</strong>
              <NuxtLink to="/register">Request an account</NuxtLink> with a unique username.
              Requests are automatically approved for this evaluation, even though the
              acknowledgment mentions a review. Select Continue, then
              <NuxtLink to="/login">sign in</NuxtLink> with your username and password.
            </li>
            <li>
              <strong>Find your workspace.</strong>
              Open <NuxtLink to="/workspaces">Workspaces</NuxtLink>: your personal workspace is
              already provisioned, and you are its Owner. Select Manage to explore its settings and
              data tools. Leave the delete actions alone. You could create additional workspaces
              later to separate different aspects of your financial life.
            </li>
            <li>
              <strong>Load some history.</strong>
              Under Sample Data, keep Personal Finance and the default dates. Select Load Sample
              Data, confirm, and wait for the transaction and rule counts. Load it only once, and
              use this same workspace throughout the tour.
            </li>
            <li>
              <strong>Explore and search.</strong>
              Open <NuxtLink to="/transactions">Transactions</NuxtLink>, browse a few pages, and
              open a row for details. Try searching for a word from a payee or category; search also
              covers memos and amounts. Clear the search afterward.
            </li>
            <li>
              <strong>See the starting picture.</strong>
              Visit <NuxtLink to="/reports">Reports</NuxtLink> and try Expenses, Income, and
              Expenses Detail. Note a category total and the monthly amounts in Expenses Detail so
              you can compare after importing. Reports show the current year.
            </li>
            <li>
              <strong>Get a sample bank download.</strong>
              Return to <NuxtLink to="/workspaces">Workspaces</NuxtLink> and Manage your workspace.
              Under Download sample bank data, keep the default dates and select Download Sample
              OFX. This simulates a bank download with more recent activity; it does not change your
              ledger.
            </li>
            <li>
              <strong>Upload and review, but don't accept yet.</strong>
              Open <NuxtLink to="/import">Import</NuxtLink>, choose the downloaded OFX file from
              your computer, and select Upload. New transactions are selected by default; duplicates
              are deselected. Notice the categories are already filled in automatically.
            </li>
            <li>
              <strong>Discover the automation.</strong>
              Visit <NuxtLink to="/payee-rules">Rules</NuxtLink> and find a payee from the review.
              The sample rules match payee text to categories, saving you manual work. Your pending
              import stays available while you look around.
            </li>
            <li>
              <strong>Accept the import.</strong>
              Return to <NuxtLink to="/import">Import</NuxtLink>, select Import, check the workspace
              and counts, then confirm. Selected rows enter your ledger; unselected rows are
              discarded and the review queue is cleared. Find the new activity in
              <NuxtLink to="/transactions">Transactions</NuxtLink>, with its categories intact.
            </li>
            <li>
              <strong>See what changed.</strong>
              Return to <NuxtLink to="/reports">Reports</NuxtLink> and choose Expenses Detail again.
              Compare the category totals and recent months, not the number of rows. Your newly
              accepted activity now contributes to the report. Try All Detail for the wider picture.
            </li>
          </ol>

          <div class="mt-4 pt-4 border-top">
            <p class="text-muted mb-0">
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

<style scoped>
.evaluation-notice {
  border-left: 4px solid var(--bs-warning);
}

.evaluation-journey {
  --step-size: 2.5rem;
  counter-reset: tour-step;
  display: grid;
  gap: 1rem;
  list-style: none;
  margin: 0;
  padding: 0 0 0 calc(var(--step-size) + 1rem);
  position: relative;
}

.evaluation-journey::before {
  background: var(--bs-border-color);
  bottom: 1.5rem;
  content: '';
  left: calc(var(--step-size) / 2);
  position: absolute;
  top: 1.5rem;
  width: 2px;
}

.evaluation-journey > li {
  background: var(--bs-body-bg);
  border: 1px solid var(--bs-border-color);
  border-radius: 0.75rem;
  counter-increment: tour-step;
  line-height: 1.7;
  padding: 1.25rem 1.5rem;
  position: relative;
}

.evaluation-journey > li::before {
  align-items: center;
  background: var(--bs-primary);
  border-radius: 50%;
  color: var(--bs-white);
  content: counter(tour-step, decimal-leading-zero);
  display: flex;
  font-size: 0.875rem;
  font-weight: 700;
  height: var(--step-size);
  justify-content: center;
  left: calc(-1 * (var(--step-size) + 1rem));
  position: absolute;
  top: 1.25rem;
  width: var(--step-size);
}

.evaluation-journey > li > strong {
  color: var(--bs-emphasis-color);
  display: block;
  font-size: 1.125rem;
  line-height: 1.4;
  margin-bottom: 0.5rem;
}

.evaluation-journey > li:last-child {
  background: var(--bs-primary-bg-subtle);
  border-color: var(--bs-primary-border-subtle);
}

@media (max-width: 575.98px) {
  .evaluation-journey {
    --step-size: 2rem;
    padding-left: calc(var(--step-size) + 0.75rem);
  }

  .evaluation-journey > li {
    padding: 1rem;
  }

  .evaluation-journey > li::before {
    left: calc(-1 * (var(--step-size) + 0.75rem));
    top: 1rem;
  }
}
</style>
