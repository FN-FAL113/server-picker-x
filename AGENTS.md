# AGENTS.md

## App Overview
The app is a Windows/Linux desktop application designed to manage access to global CS2 and Deadlock servers by blocking
or unblocking specific servers based on their geographic location. The primary function of this tool is server filtering for distributed gaming networks.

## Repository Overview
This repository contains a **AvaloniaUI** desktop application named *ServerPickerX*.
The project is a .NET 10.0 that bundles all resources into
a single file `<PublishSingleFile>true</PublishSingleFile>` and it can run independently without requiring .NET SDK `<SelfContained>true</SelfContained>`. 
The code follows the MVVM pattern with ViewModels exposed through `Microsoft.Extensions.DependencyInjection` specifically a 
static singleton IoC service container in `App.axaml.cs`.

## Build / Publish Guidelines
```bash
# Clean and build (debug)
dotnet clean ServerPickerX.slnx

# Development build for current platform
dotnet build ServerPickerX.slnx -c Debug

# Release build for windows platform
dotnet publish ServerPickerX.slnx -c Release -r win-x64

# Release build for linux platform
dotnet publish ServerPickerX.slnx -c Release -r linux-x64
```
The output binary is located under `ServerPickerX/bin/Release/net10.0/<win-x64|linux-x64>/publish`.

## Linting & Formatting Guidelines
```bash
# Check formatting without making changes
# Requires dotnet-format to be installed (dotnet tool install -g dotnet-format)
dotnet format ServerPickerX.slnx --verify-no-changes

# Apply formatting automatically, ask for permission first before executing this command
# WARNING: This rewrites files. Use only in CI or when you intend to change code.
dotnet format ServerPickerX.slnx
```

## Testing Guidelines
> **Note**: The repository currently contains no automated tests. When adding
> tests, follow these guidelines:
>
> * Place test projects in a sibling folder named `Tests`.
> * Target the same framework (`net10.0`).
> * Use xUnit as the testing framework
>
```bash
# Create a new test project

dotnet new xunit -n ServerPickerX.Tests --framework net10.0

# Add reference to the main project

dotnet add ServerPickerX.Tests/ServerPickerX.Tests.csproj reference ../ServerPickerX/ServerPickerX.csproj
```
* Run all tests:
```bash

dotnet test ServerPickerX.Tests.slnx
```
* Run a single test (fully‑qualified name):
```bash

dotnet test --filter "FullyQualifiedName=ServerPickerX.Models.ServerModelTests.ParseTest"
```

## Code Style Guidelines
| Area | Guideline |
|------|-----------|
| **Imports** | System namespaces first, then project namespaces. Keep `using` statements sorted alphabetically and grouped by scope. Remove unused usings with the built‑in IDE refactor.
| **Formatting** | 4 spaces per indentation level; no tabs. End each file with a single newline. Do not leave trailing whitespace on any line.
| **Naming** |
| &nbsp;&nbsp;*Public members (classes, methods, properties)* | PascalCase (e.g., `LoadServers`, `ClusterUnclusterServers`).
| &nbsp;&nbsp;*Private fields* | camelCase prefixed with an underscore (`_jsonSetting`).
| &nbsp;&nbsp;*Local variables* | camelCase without underscore.
| **Async** | All asynchronous methods return `Task` or `Task<T>`. Use the `async` keyword only when you await inside.
| **Exception Handling** | 
• Catch only specific exceptions you can handle.
• Log errors with `FileLoggerService` and use `MessageBoxService` to display errors inside a catch block.
| **MVVM Conventions** |
| &nbsp;&nbsp;*ViewModels* | Inherit from `ObservableObject` (CommunityToolkit.Mvvm). Use `[ObservableProperty]` for properties that should notify UI changes. Keep commands as `ICommand` or `RelayCommand`.
| &nbsp;&nbsp;*Views* | Prefer code‑behind only for view logic that cannot be expressed in XAML, such as dynamic tooltips. Keep view models free of UI references.
| **Resources** |
| &nbsp;&nbsp;*Images* | Store in `Assets/` and reference via pack URIs (`/Assets/...`).
| &nbsp;&nbsp;*Styles* | Define reusable styles in `Styles/*.axaml`. Register new style files by appending inside App.axaml inside `<Application.Styles></Application.Styles>`.

## Build & CI Checklist
- [ ] All tests pass (`dotnet test ServerPickerX.Tests.slnx`).
- [ ] Code passes linting (`dotnet format ServerPickerX.slnx --verify-no-changes`).
- [ ] Publish output contains a single executable without unnecessary dependencies except for files `libHarfBuzzSharp.so` and `libSkiaSharp.so`.

## Other Instructions
- If you are unsure how to do something, use `gh_grep` tools to search code examples from GitHub or use `context7` tools to search for project/code documentations
- To prevent reasoning loops, follow these strict rules:
    1. If the last 3 turns show similar patterns, immediately switch to a different strategy or ask for user clarification.
