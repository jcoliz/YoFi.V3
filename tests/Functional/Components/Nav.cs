using Microsoft.Playwright;

namespace YoFi.V3.Tests.Functional.Components;

public class Nav(IPage page, ILocator parent)
{
    public ILocator Root => parent.Locator("nav").First;
    public ILocator this [string key] => Root.Locator(".nav-link").GetByText(key);

    public async Task SelectOptionAsync(string option)
    {
        await this[option].ClickAsync();
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
    }
}
