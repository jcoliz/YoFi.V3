# Bugs/fixes!

## 1.0.1

* [x] Add "FeatureFile" to CRIF, and then we can use that for the symbolic name of the class
* [x] Doesn't seem to include the table in the args for datatable items
* [x] Need to output the whole CRIF for debugging
* [ ] Parsing errors need to give details: Attempting to! Check again in next release

# 1.0.3

All above confirmed fixed, EXCEPT

* [x] Parsing errors need to give details. Trying again!

```
PS C:\Source\jcoliz\YoFi.V3\tests\Functional> dotnet build
Restore complete (0.3s)
  YoFi.V3.Tests.Functional net10.0 failed with 1 error(s) (1.0s)
    CSC : error GHERKIN003: Error parsing BankImport.feature: Parser errors:

Build failed with 1 error(s) in 1.9s
```

Fixed in 1.0.4!! Perfect!!

```
  YoFi.V3.Tests.Functional net10.0 failed with 1 error(s) (0.9s)
    CSC : error GHERKIN003: Error parsing BankImport.feature: (1:1): A tag may not contain whitespace | (1:1): expected: #EOF, #Language, #TagLine, #FeatureLine, #Comment, #Empty, got '@using YoFi.V3.Tests.Functional.Helpers'
```

* [x] Unimplemented steps need to be called with args, and defined with args.

Wrong:

```c#
// And I have also some other  transactions with external IDs:
var table2 = new DataTable(
    ["ExternalId", "Date", "Payee", "Amount"],
    ["2024010701", "2024-01-07", "Gas Station", "-89.99"]
);
await this.IHaveAlsoSomeOtherTransactionsWithExternalIDs();

...

/// <summary>
/// Given I have also some other  transactions with external IDs:
/// </summary>
async Task IHaveAlsoSomeOtherTransactionsWithExternalIDs()
{
    throw new NotImplementedException();
}
```

Right:

```c#
// And I have also some other  transactions with external IDs:
var table2 = new DataTable(
    ["ExternalId", "Date", "Payee", "Amount"],
    ["2024010701", "2024-01-07", "Gas Station", "-89.99"]
);
await this.IHaveAlsoSomeOtherTransactionsWithExternalIDs(table2);

...

/// <summary>
/// Given I have also some other  transactions with external IDs:
/// </summary>
async Task IHaveAlsoSomeOtherTransactionsWithExternalIDs(DataTable table)
{
    throw new NotImplementedException();
}
```

## 1.0.4

[_] This gherkin:

```gherkin
Then I should see "Chase Visa" as the Source
```

With this step:

```c#
    [Then("I should see {expectedValue} as the {fieldName}")]
    public async Task ThenIShouldSeeValueAsField(string expectedValue, string fieldName)
```

Generates this:

```c#
        // Then I should see &quot;Chase Visa&quot; as the Source
        await TransactionDetailsSteps.ThenIShouldSeeValueAsField("Chase Visa", Source);
```

But should generate this:

```c#
        // Then I should see "Chase Visa" as the Source
        await TransactionDetailsSteps.ThenIShouldSeeValueAsField("Chase Visa", "Source");
```

[_] DataTable in Background doesn't generate correctly

This gherkin:

```gherkin
Background:
    Given the application is running
    And these users exist:
        | Username |
        | alice    |
        | bob      |
        | charlie  |
```

Generates this:

```c#
// And these users exist:
var  = new DataTable(
    ["Username"],
    ["alice"],
    ["bob"],
    ["charlie"]
);
await AuthSteps.GivenTheseUsersExist();
```

But should generate this:

```c#
// And these users exist:
var table = new DataTable(
    ["Username"],
    ["alice"],
    ["bob"],
    ["charlie"]
);
await AuthSteps.GivenTheseUsersExist(table);
```
