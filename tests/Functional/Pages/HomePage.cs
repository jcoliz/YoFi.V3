using Microsoft.Playwright;
using System.Text.RegularExpressions;

namespace YoFi.V3.Tests.Functional.Pages;

public class HomePage(IPage? _page): BasePage(_page)
{
    public ILocator BrochureSection => Page!.GetByTestId("brochure-section");
}
