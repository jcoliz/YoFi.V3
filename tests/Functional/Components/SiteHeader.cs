using Microsoft.Playwright;

namespace YoFi.V3.Tests.Functional.Components;
public class SiteHeader(IPage page, ILocator parent)
{
    public ILocator Root => parent.GetByTestId("site-header");
    public Nav Nav => new Nav(page, Root);
    public LoginState LoginState => new LoginState(page, Root);
}
