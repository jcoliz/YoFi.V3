using Microsoft.Playwright;

namespace YoFi.V3.Tests.Functional.Pages;

public class WeatherPage(IPage? _page): BasePage(_page)
{
    public ILocator ForecastTableBody => Page!.GetByTestId("forecast-table-body");
    public ILocator ForecastRows => ForecastTableBody.Locator("tr");
}
