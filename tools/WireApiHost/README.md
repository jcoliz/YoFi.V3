# WireAPI Host

This project is a minimal host project containing just enough code to generate 
TypeScript APIs for the Front End to call the Back End.

Note that while you can run this project and explore the API in the Swagger UI,
you cannot effective call it, as no data source is provided.

## Generating new apiclient.ts

To generate a new client, just build this project. It will create a new [apiclient.ts](../FrontEnd.Nuxt/app/utils/apiclient.ts)
file as part of the build output.

For validation error response metadata, use `Microsoft.AspNetCore.Http.HttpValidationProblemDetails`
in `[ProducesResponseType]`. MVC's `ValidationProblemDetails` derives from that type
and hides its `Errors` property. NSwag generates duplicate `errors` declarations for
that hierarchy, which fail TypeScript typechecking. The HTTP base type describes
the same JSON response and is also used by the exception handlers. The model-state
response factory retains MVC's type for its `ModelStateDictionary` constructor.

The TypeScript generator uses the customized [Client.RequestUrl.liquid](Templates/Client.RequestUrl.liquid)
template to serialize date-only query parameters as calendar dates. It is based on
NSwag 14.7.1's template; review it when upgrading NSwag. The template uses the
same local-calendar-date convention as the generated DTO serializer, so callers
should construct dates from date inputs using `fromDateInputValue`.

For more information, see the [NSwag template override documentation](https://github.com/RicoSuter/NSwag/wiki/Templates),
the [NJsonSchema template documentation](https://github.com/RicoSuter/NJsonSchema/wiki/Templates)
for generator-specific tags and conventions, and the [Liquid language reference](https://shopify.github.io/liquid/)
for template syntax.
