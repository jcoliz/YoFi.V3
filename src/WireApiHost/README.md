# WireAPI Host

This project is a minimal host project containing just enough code to generate 
TypeScript APIs for the Front End to call the Back End.

Note that while you can run this project and explore the API in the Swagger UI,
you cannot effective call it, as no data source is provided.

## Generating new apiclient.ts

To generate a new client, just build this project. It will create a new [apiclient.ts](../FrontEnd.Nuxt/app/utils/apiclient.ts)
file as part of the build output.
