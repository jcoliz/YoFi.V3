using Microsoft.Playwright;

namespace YoFi.V3.Tests.Functional.Components;

/// <summary>
/// Page Object Model for the ErrorDisplay component
/// </summary>
/// <param name="parent">The parent locator containing this component</param>
/// <remarks>
/// Represents the error display component that shows error messages in a Bootstrap alert
/// with optional expandable details. Supports RFC 7807 Problem Details format.
/// </remarks>
public class ErrorDisplay(ILocator parent)
{
    #region Component Elements

    /// <summary>
    /// Root element of the ErrorDisplay component
    /// </summary>
    public ILocator Root => parent.GetByTestId("error-display");

    /// <summary>
    /// Error title text display
    /// </summary>
    public ILocator Title => Root.GetByTestId("title-display");

    /// <summary>
    /// Error detail text display
    /// </summary>
    public ILocator Detail => Root.GetByTestId("detail-display");

    /// <summary>
    /// Show/Hide details toggle button
    /// </summary>
    public ILocator MoreButton => Root.GetByTestId("more-button");

    /// <summary>
    /// Expanded details text (trace ID for server errors)
    /// </summary>
    public ILocator MoreText => Root.GetByTestId("more-text");

    /// <summary>
    /// Close button for dismissing the error alert
    /// </summary>
    public ILocator CloseButton => Root.GetByTestId("close-button");

    #endregion

    #region Actions

    /// <summary>
    /// Clicks the "Show details" button to expand additional information
    /// </summary>
    public async Task ShowMoreAsync()
    {
        await MoreButton.ClickAsync();
        await MoreText.WaitForAsync(new() { State = WaitForSelectorState.Visible });
    }

    /// <summary>
    /// Clicks the "Hide details" button to collapse additional information
    /// </summary>
    public async Task HideMoreAsync()
    {
        await MoreButton.ClickAsync();
        await MoreText.WaitForAsync(new() { State = WaitForSelectorState.Hidden });
    }

    /// <summary>
    /// Closes the error display by clicking the close button
    /// </summary>
    public async Task CloseAsync()
    {
        await CloseButton.ClickAsync();
        await Root.WaitForAsync(new() { State = WaitForSelectorState.Hidden });
    }

    #endregion

    #region Query Methods

    /// <summary>
    /// Checks if the error display is currently visible
    /// </summary>
    /// <returns>True if the error display is visible, false otherwise</returns>
    public async Task<bool> IsVisibleAsync()
    {
        return await Root.IsVisibleAsync();
    }

    /// <summary>
    /// Gets the error title text
    /// </summary>
    /// <returns>The title text content</returns>
    public async Task<string?> GetTitleAsync()
    {
        return await Title.TextContentAsync();
    }

    /// <summary>
    /// Gets the error detail text
    /// </summary>
    /// <returns>The detail text content</returns>
    public async Task<string?> GetDetailAsync()
    {
        return await Detail.TextContentAsync();
    }

    /// <summary>
    /// Checks if the "Show details" button is visible
    /// </summary>
    /// <returns>True if the more button is visible, false otherwise</returns>
    public async Task<bool> HasMoreButtonAsync()
    {
        return await MoreButton.IsVisibleAsync();
    }

    /// <summary>
    /// Gets the expanded details text (typically a trace ID)
    /// </summary>
    /// <returns>The more text content, or null if not visible</returns>
    public async Task<string?> GetMoreTextAsync()
    {
        if (await MoreText.IsVisibleAsync())
        {
            return await MoreText.TextContentAsync();
        }
        return null;
    }

    /// <summary>
    /// Checks if the expanded details are currently visible
    /// </summary>
    /// <returns>True if more text is visible, false otherwise</returns>
    public async Task<bool> IsMoreTextVisibleAsync()
    {
        return await MoreText.IsVisibleAsync();
    }

    #endregion

    #region Wait Helpers

    /// <summary>
    /// Waits for the error display to appear
    /// </summary>
    public async Task WaitForVisibleAsync()
    {
        await Root.WaitForAsync(new() { State = WaitForSelectorState.Visible });
    }

    /// <summary>
    /// Waits for the error display to disappear
    /// </summary>
    public async Task WaitForHiddenAsync()
    {
        await Root.WaitForAsync(new() { State = WaitForSelectorState.Hidden });
    }

    #endregion
}
