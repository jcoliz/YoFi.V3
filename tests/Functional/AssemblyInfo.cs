using NUnit.Framework;

// Disable parallel execution of test fixtures to prevent Playwright disposal issues
// This is required because Playwright.NUnit.PageTest manages a shared Playwright instance
// that gets disposed when test fixtures run in parallel
[assembly: LevelOfParallelism(1)]
[assembly: NonParallelizable]
