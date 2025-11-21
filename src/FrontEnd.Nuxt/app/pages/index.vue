<script setup lang="ts">
definePageMeta({
  title: 'Home',
  order: 1,
  layout: 'chrome',
})

// Sample data - replace with actual API calls
const quickStats = ref({
  totalBalance: 12345.67,
  monthlyIncome: 5200.00,
  monthlyExpenses: 3850.25,
  savingsRate: 26
})

const recentTransactions = ref([
  { id: 1, description: 'Grocery Store', amount: -85.42, date: '2024-01-15', category: 'Food' },
  { id: 2, description: 'Salary Deposit', amount: 2600.00, date: '2024-01-15', category: 'Income' },
  { id: 3, description: 'Electric Bill', amount: -120.00, date: '2024-01-14', category: 'Utilities' }
])

const quickActions = [
  { title: 'Add Transaction', icon: '💰', route: '/transactions/new', color: 'bg-green-500' },
  { title: 'View Budget', icon: '📊', route: '/budget', color: 'bg-blue-500' },
  { title: 'Reports', icon: '📈', route: '/reports', color: 'bg-purple-500' },
  { title: 'Categories', icon: '🏷️', route: '/categories', color: 'bg-orange-500' }
]
</script>

<template>
  <div class="min-vh-100 bg-light">
    <!-- Hero Section -->
    <div class="bg-gradient-primary text-white">
      <div class="container py-5">
        <div class="text-center">
          <h1 class="display-4 fw-bold mb-4">Welcome to YoFi! 🎉</h1>
          <p class="lead">Take control of your financial future</p>
        </div>
      </div>
    </div>

    <div class="container py-4">
      <!-- Quick Stats -->
      <div class="row g-4 mb-5">
        <div class="col-12 col-md-6 col-lg-3">
          <div class="card h-100 border-start border-success border-4">
            <div class="card-body">
              <div class="d-flex align-items-center">
                <div class="fs-1 me-3">💳</div>
                <div>
                  <p class="text-muted mb-1 small">Total Balance</p>
                  <p class="h4 fw-bold text-success mb-0">${{ quickStats.totalBalance.toLocaleString() }}</p>
                </div>
              </div>
            </div>
          </div>
        </div>

        <div class="col-12 col-md-6 col-lg-3">
          <div class="card h-100 border-start border-primary border-4">
            <div class="card-body">
              <div class="d-flex align-items-center">
                <div class="fs-1 me-3">📈</div>
                <div>
                  <p class="text-muted mb-1 small">Monthly Income</p>
                  <p class="h4 fw-bold text-primary mb-0">${{ quickStats.monthlyIncome.toLocaleString() }}</p>
                </div>
              </div>
            </div>
          </div>
        </div>

        <div class="col-12 col-md-6 col-lg-3">
          <div class="card h-100 border-start border-danger border-4">
            <div class="card-body">
              <div class="d-flex align-items-center">
                <div class="fs-1 me-3">📉</div>
                <div>
                  <p class="text-muted mb-1 small">Monthly Expenses</p>
                  <p class="h4 fw-bold text-danger mb-0">${{ quickStats.monthlyExpenses.toLocaleString() }}</p>
                </div>
              </div>
            </div>
          </div>
        </div>

        <div class="col-12 col-md-6 col-lg-3">
          <div class="card h-100 border-start border-info border-4">
            <div class="card-body">
              <div class="d-flex align-items-center">
                <div class="fs-1 me-3">🎯</div>
                <div>
                  <p class="text-muted mb-1 small">Savings Rate</p>
                  <p class="h4 fw-bold text-info mb-0">{{ quickStats.savingsRate }}%</p>
                </div>
              </div>
            </div>
          </div>
        </div>
      </div>

      <!-- Quick Actions -->
      <div class="mb-5">
        <h2 class="h3 fw-bold text-dark mb-4">Quick Actions</h2>
        <div class="row g-3">
          <div class="col-6 col-md-3" v-for="action in quickActions" :key="action.title">
            <NuxtLink
              :to="action.route"
              class="card h-100 text-decoration-none text-dark card-hover"
            >
              <div class="card-body text-center">
                <div class="fs-1 mb-3">{{ action.icon }}</div>
                <h3 class="h6 fw-semibold">{{ action.title }}</h3>
              </div>
            </NuxtLink>
          </div>
        </div>
      </div>

      <!-- Recent Activity -->
      <div class="row g-4">
        <div class="col-12 col-lg-6">
          <div class="card h-100">
            <div class="card-body">
              <h3 class="h5 fw-bold text-dark mb-4">Recent Transactions</h3>
              <div class="vstack gap-3">
                <div
                  v-for="transaction in recentTransactions"
                  :key="transaction.id"
                  class="d-flex align-items-center justify-content-between p-3 bg-light rounded"
                >
                  <div>
                    <p class="fw-medium text-dark mb-1">{{ transaction.description }}</p>
                    <p class="small text-muted mb-0">{{ transaction.category }} • {{ transaction.date }}</p>
                  </div>
                  <span
                    :class="transaction.amount > 0 ? 'text-success' : 'text-danger'"
                    class="fw-bold"
                  >
                    {{ transaction.amount > 0 ? '+' : '' }}${{ transaction.amount.toLocaleString() }}
                  </span>
                </div>
              </div>
              <div class="mt-3">
                <NuxtLink
                  to="/transactions"
                  class="text-primary text-decoration-none fw-medium"
                >
                  View all transactions →
                </NuxtLink>
              </div>
            </div>
          </div>
        </div>

        <!-- Financial Health Score -->
        <div class="col-12 col-lg-6">
          <div class="card h-100">
            <div class="card-body">
              <h3 class="h5 fw-bold text-dark mb-4">Financial Health</h3>
              <div class="text-center">
                <div class="display-1 mb-3">🏆</div>
                <div class="h3 fw-bold text-success mb-2">Excellent</div>
                <p class="text-muted mb-4">You're doing great with your finances!</p>
                <div class="progress mb-3" style="height: 12px;">
                  <div class="progress-bar bg-success" style="width: 85%"></div>
                </div>
                <p class="small text-muted">85% Financial Health Score</p>
              </div>
            </div>
          </div>
        </div>
      </div>

      <!-- Motivational Message -->
      <div class="mt-4 card bg-gradient-success text-white">
        <div class="card-body text-center">
          <h3 class="h5 fw-bold mb-2">💡 Financial Tip of the Day</h3>
          <p class="mb-0 opacity-75">Small consistent savings today lead to big financial wins tomorrow!</p>
        </div>
      </div>
    </div>
  </div>
</template>

<style scoped>
.bg-gradient-primary {
  background: linear-gradient(135deg, #0d6efd 0%, #6f42c1 100%);
}

.bg-gradient-success {
  background: linear-gradient(135deg, #198754 0%, #20c997 100%);
}

.card-hover:hover {
  box-shadow: 0 0.5rem 1rem rgba(0, 0, 0, 0.15) !important;
  transition: box-shadow 0.15s ease-in-out;
  color: #0d6efd !important;
}
</style>
