# Solution Version Strategy

We display a version identifier to the end user. Users can use it to determine the features to expect in a given session, and gauge the relative maturity level between two separate versions. Developers can use it to localize reported bugs or issues to a specific set of built bits. Consequently, the version number needs to indicate the exact bits used to build it, AND the environment used to build.

## Version Identifiers

### Release versions: "5.3.1" or "1.2.0-alpha1"

Software released for public use, or on a pathway for ultimate release, uses the standard semantic version as its version. Alone and with no additions like a 'v' prefix.

Such versions are always built from a git tag, whose name exactly matches the semantic version. This provides precise git commit reconciliation.

Such versions are always built using the project's build service. Absolutely no code is released which was built privately. The one exception to this is during initial bring-up of the project, in which case the version must always be "0.0.x".

### Continuous Integration builds "ci-1536"

CI builds are built more to ensure that the code integrates properly, and less to widely run them. Still, they may be run from time to time. They get a version "ci-{key}" where the key identifies the build number. From the build number, we must be able to get to a specific commit and test pass results.

### Local builds "1.2.0-53-5df7ae61-jcoliz-05231326"

Builds made on an individual developer's machine must include an identifier of that developer. In addition, we like to include the `git describe` version, and a time identifier. The git version gives a sense of where in the codebase the build was created, while the  time identifier gives additional precision as a developer will typically create multiple builds between commits.

## Data Flow

### Back end: Production CD Build

Let's examine the backend build flow from building production bits on Azure Pipelines, all the way through displaying to user.

```
Developer
  ↓ git tag 1.2.3 -s -m "Added many exciting new features"
  ↓ git push --tags

Azure Pipelines
  ↓ triggers new pipeline run when detects new tag
  ↓ launches build using cd.yaml
  ↓ loads vars from vars/var-cd.yaml
  ↓ sets Solution.Version pipeline variable to $(Build.SourceBranchName)
  ↓ sets $env:SOLUTION_VERSION with this value

MSBuild (src\BackEnd\YoFi.V3.BackEnd.csproj)
  ↓ Reads $env:SOLUTION_VERSION into $(SolutionVersion) project variable
  ↓ SetSolutionVersion target skips running Get-Version.ps1 (condition is false)
  ↓ Generates AssemblyInfo with [AssemblyInformationalVersion("1.2.3")]

SetupApplicationOptions.AddApplicationOptions()
  ↓ Reads AssemblyInformationalVersionAttribute → "1.2.3"
  ↓ Sets builder.Configuration["Application:Version"] = "1.2.3"

Configure<ApplicationOptions>
  ↓ Binds Configuration["Application:Version"] to ApplicationOptions.Version
  ↓ Registers in DI container as IOptions<ApplicationOptions>

VersionController.GetVersion()
  ↓ Receives IOptions<ApplicationOptions> via DI
  ↓ Reads options.Value.Version → "1.2.3"
  ↓ Exposed at GET /api/version endpoint

about.vue (Frontend)
  ↓ Calls client.getVersion()
  ↓ Makes GET request to http://localhost:5001/api/version

HTTP Response
  ↓ Returns: "1.2.3"

about.vue
  ↓ Receives response in .then()
  ↓ Sets version.value = "1.2.3"
  ↓ Template displays: {{ version }}

User's Browser
  ✅ Displays: "1.2.3"
```

### Front end: Production CD Build

```
Azure Pipelines
(same as above)

docker-compose
  ↓ reads ${SOLUTION_VERSION} from host env
  ↓ passes as --build-arg NUXT_PUBLIC_SOLUTION_VERSION=1.2.3

Frontend Dockerfile
  ↓ receives ARG NUXT_PUBLIC_SOLUTION_VERSION

Nuxt Build Process
  ↓ reads ENV variable during: npm run generate
  ↓ Nuxt automatically reads all NUXT_PUBLIC_* environment variables

Runtime Config
  ↓ Overridden by NUXT_PUBLIC_*
   File: nuxt.config.ts
   Code: runtimeConfig: {
           public: {
             solutionVersion: '0.0',  // ← Replaced by ENV at build time
           }
         }

About.vue
  ↓ accesses version: const runtimeConfig = useRuntimeConfig()
    const frontEndVersion = runtimeConfig.public.solutionVersion
  ↓ template displays: "{{ frontEndVersion }}"

User's Browser
  ✅ Displays: "1.2.3"
```

### Back end: Container built locally

```
Build-Container.ps1
  ↓ runs Get-Version.ps1 → gets "d7fce8a-jcoliz-11181430"
  ↓ appends "-bcps" → "d7fce8a-jcoliz-11181430-bcps"
  ↓ sets $env:SOLUTION_VERSION

docker-compose
  ↓ reads ${SOLUTION_VERSION} from host env
  ↓ passes as --build-arg SOLUTION_VERSION=d7fce8a-jcoliz-11181430-bcps

Dockerfile
  ↓ ARG SOLUTION_VERSION receives the build arg
  ↓ Makes it available as env var inside container during build

MSBuild (src\BackEnd\YoFi.V3.BackEnd.csproj)
(remainder same as backend production build, instead using local version #)
```

### Front end: Container built locally

```
Build-Container.ps1
(same as local backend build)

docker-compose
(remaining steps same as frontend production build)
```

## Build tooling

### Get-Version.ps1

Script to set solution version in local builds

```powershell
$User = $env:USERNAME

$Commit = git describe --always 2>&1

if ($Stable) {
    $Time = Get-Date -UFormat "%yW%V"
    Write-Verbose "Using stable time format: $Time"
}
else {
    $Time = Get-Date -Format "MMddHHmm"
    Write-Verbose "Using default time format: $Time"
}

$Version = "$Commit-$User-$Time"
Write-Output $Version
```

### Build-Container.ps1

Local script to set solution version. Note, this uses a more stable version of the date,
so we won't trigger a new docker build every time we run this.

```powershell
$env:SOLUTION_VERSION = & ./scripts/Get-Version.ps1 -Stable
$env:SOLUTION_VERSION = "$env:SOLUTION_VERSION-bcps"
docker compose -f ./docker/docker-compose-ci.yml build
```

### src\BackEnd\YoFi.V3.BackEnd.csproj

```xml
  <!-- Generate version using Get-Version.ps1 -->
  <Target Name="SetSolutionVersion" BeforeTargets="GetAssemblyVersion;GenerateAssemblyInfo">
    <!-- Only run the script if SolutionVersion not already set -->
    <Exec Command="pwsh -Command &quot;&amp;'$(ProjectDir)\..\..\scripts\Get-Version.ps1'&quot;"
          ConsoleToMSBuild="true"
          Condition="'$(SolutionVersion)' == ''">
      <Output TaskParameter="ConsoleOutput" PropertyName="SolutionVersion" />
    </Exec>

    <!-- Fallback value -->
    <PropertyGroup>
      <SolutionVersion Condition="'$(SolutionVersion)' == ''">local-build-unknown</SolutionVersion>
      <InformationalVersion>$(SolutionVersion)</InformationalVersion>
    </PropertyGroup>

    <Message Importance="high" Text="Using SolutionVersion: $(SolutionVersion)" />
  </Target>
```
