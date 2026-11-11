# WireAPI Host

This project is a minimal host project containing just enough code to generate 
TypeScript APIs for the Front End to call the Back End.

Note that while you can run this project and explore the API in the Swagger UI,
you cannot effective call it, as no data source is provided.

## Generating new apiclient.ts

To generate a new client, just build this project. It will create a new [apiclient.ts](../FrontEnd.Nuxt/app/utils/apiclient.ts)
file as part of the build output.

The TypeScript generator uses the customized [Client.RequestUrl.liquid](Templates/Client.RequestUrl.liquid)
template to serialize date-only query parameters as calendar dates. It is based on
NSwag 14.7.1's template; review it when upgrading NSwag. The template uses the
same local-calendar-date convention as the generated DTO serializer, so callers
should construct dates from date inputs using `fromDateInputValue`.

For more information, see the [NSwag template override documentation](https://github.com/RicoSuter/NSwag/wiki/Templates),
the [NJsonSchema template documentation](https://github.com/RicoSuter/NJsonSchema/wiki/Templates)
for generator-specific tags and conventions, and the [Liquid language reference](https://shopify.github.io/liquid/)
for template syntax.
