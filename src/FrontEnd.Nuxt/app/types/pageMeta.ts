export interface BreadcrumbItem {
  title: string
  to: string
}

declare module '#app' {
  interface PageMeta {
    breadcrumbs?: BreadcrumbItem[]
  }
}

declare module 'vue-router' {
  interface RouteMeta {
    breadcrumbs?: BreadcrumbItem[]
  }
}
