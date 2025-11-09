# Controllers

These are application-specific HTTP API controllers which expose application functionality
as a web API. This api is only intended to be consumed by the Front End.

They are expected to be thin. The only things they should do are:

* Deconstruct the HTTP request
* Make one call to an Application Feature
* Create log entries
* Construct the HTTP response
