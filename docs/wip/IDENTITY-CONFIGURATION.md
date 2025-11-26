# Identity configuration

I'm updating the underlying [NuxtIdentity](https://www.github.com/jcoliz/NuxtIdentity) library to be more secure by removing defaults for JWT configuration values. This means I will now need to ensure these values get placed correctly.

Specific values:

* Jwt:Key -- secret key used for signing JWT tokens.
* Jwt:Issuer -- token issuer
* Jwt:Audience -- token audience
* Jwt:Lifespan -- how long JWT auth tokens live before expiring

Configuration will be placed differently for each of the supported [ENVIRONMENTS](../ENVIRONMENTS.md).

## Local development

For local development, it's easiest to keep this information in `appsettings.Development.json`.

The downside of this is that this means checking a key into source control. However, this key is not used in any production settings, so the risk here should be limited.

Alternately, if I were to require settting up a key, this is a barrier to entry for new development. Perhaps I could automatically configure this in `scripts/SetupDevelopment.ps1`

## Container

In the spirit of keeping the container environment as close as possible to the production environment, I'll provide these values as runtime environment valiables.

In past projects, I have checked in a separate config file into the source tree, again containing a key. The dockerfile copies this file from its source location, into the project structure, so the configuration is built in.

## Production (initial)

For initial bring-up, I will manually set these as application configuration environment variables in the app service resource, e.g. `JWT__KEY`

## Production (long-term)

For long-term production, I will move these to Azure Key Vault.
