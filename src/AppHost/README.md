# .NET Aspire App Host

This project orchestrates the entire application using .NET Aspire. It configures
and launches all services (BackEnd, FrontEnd.Nuxt) in the development environment.

## Running the Application

Run this project to start the entire application stack with the Aspire dashboard.

```powershell
dotnet watch --project src\AppHost
```