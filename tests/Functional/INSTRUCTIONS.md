# Instructions to convert feature files to C# test files

This file provides detailed instructions to generate C# files out of Gherkin feature files.
They are intended for GitHub Copilot, but you can use them manually if you like!

## Steps

1. For each Feature file (ending in `*.feature`) in the [Features](./Features/) folder, we will create one Test file written in C# into the [Tests](./Tests/) folder.
2. The name of the Test file is the name of the Feature file, with the extension `.cs` appended.
3. To generate the C# Test file, follow the template as indicated by the `@template` tag. This file is a mustache file located in the [Features](./Features/) folder.
4. For each step in the Feature file, you can find the corresponding method to call from the `@baseclass` located in the [Steps](./Steps/) folder.
5. If you see a `@hook:before-first-then` notation on a feature, or individual scenario, this describes a special step to call before the first `Then` step in the resulting Test. Treat this as a special `Step`, with these properties: `{ "Keyword": "Hook", "Text": "Before first Then Step", "Args": null }`, and `Method` as provided in the remainder of the tag.
