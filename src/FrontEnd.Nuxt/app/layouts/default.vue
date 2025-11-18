<template>
    <div data-test-id="default-layout">
        <SiteHeader/>
        <main class="container">
            <RoutePageHeader/>            
            <slot/>
        </main>
    </div>
</template>