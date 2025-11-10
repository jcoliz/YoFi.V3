using Microsoft.Playwright;

namespace YoFi.V3.Tests.Functional.Components;

public class NavBar(IPage parent)
{
    public ILocator Root => parent.Locator("nav").First;
    public ILocator this [string key] => Options.Locator(".nav-link").GetByText(key);

    private ILocator Options => Root.Locator(".nav");

    public async Task SelectOptionAsync(string option)
    {
        await this[option].ClickAsync();
        await parent.WaitForLoadStateAsync(LoadState.NetworkIdle);
    }
}
