using Microsoft.Playwright;

namespace YoFi.V3.Tests.Functional.Components;

/// <summary>
/// Component object model for the site header
/// Contains navigation and login state components
/// </summary>
/// <param name="page">Playwright page instance</param>
/// <param name="parent">Parent locator containing the site header</param>
public class SiteHeader(IPage page, ILocator parent)
{
    #region Component Elements

    /// <summary>
    /// Root site header element
    /// </summary>
    public ILocator Root => parent.GetByTestId("site-header");

    #endregion

    #region Sub-Components

    /// <summary>
    /// Navigation menu component
    /// </summary>
    public Nav Nav => new Nav(page, Root);

    /// <summary>
    /// Login state component (user dropdown)
    /// </summary>
    public LoginState LoginState => new LoginState(page, Root);

    #endregion
}
