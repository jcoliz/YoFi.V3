using Microsoft.Playwright;

namespace YoFi.V3.Tests.Functional.Components;

/// <summary>
/// Component object model for navigation menu (nav element)
/// Provides access to navigation links and selection functionality
/// </summary>
/// <param name="page">Playwright page instance (reserved for future use)</param>
/// <param name="parent">Parent locator containing the navigation element</param>
#pragma warning disable IDE0060 // Remove unused parameter
public class Nav(IPage page, ILocator parent)
#pragma warning restore IDE0060 // Remove unused parameter
{
    /// <summary>
    /// Root navigation element (nav tag)
    /// Returns the first nav element found within the parent locator
    /// </summary>
    public ILocator Root => parent.Locator("nav").First;

    /// <summary>
    /// Indexer to access navigation links by their visible text
    /// </summary>
    /// <param name="key">The visible text of the navigation link</param>
    /// <returns>Locator for the navigation link with the specified text</returns>
    public ILocator this [string key] => Root.Locator(".nav-link").GetByText(key);

    /// <summary>
    /// Clicks a navigation link by its visible text and initiates navigation
    /// </summary>
    /// <param name="option">The visible text of the navigation link to click</param>
    /// <remarks>
    /// Navigation happens asynchronously - caller should wait for destination page to be ready
    /// </remarks>
    public async Task SelectOptionAsync(string option)
    {
        await this[option].ClickAsync();
        // Navigation happens - caller should wait for destination page to be ready
    }
}
