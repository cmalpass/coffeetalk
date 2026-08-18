# Contributing to CoffeeTalk

CoffeeTalk is a .NET 9 desktop and command-line application for bounded, multi-persona LLM conversations. Contributions should preserve the shared Core behavior across both user interfaces and keep agent behavior observable, testable, and safe.

## Before you start

1. Search existing issues and pull requests before opening a new issue.
2. For a bug, include reproducible steps, expected and actual behavior, OS, SDK version, and provider (if relevant).
3. For an enhancement, describe the user outcome, compatibility impact, and how it will be tested.

Never include API keys, tokens, personal workspace data, or full provider transcripts in an issue or pull request.

## Repository layout

| Project | Responsibility |
| --- | --- |
| `CoffeeTalk.Core` | Shared models, configuration, agent orchestration, document tools, memory, telemetry, persistence, and provider integration |
| `CoffeeTalk` | Spectre.Console CLI entry point and command handlers |
| `CoffeeTalk.Gui` | Photino/Blazor desktop UI and GUI event sinks |
| `CoffeeTalk.Tests` | Unit and component tests, deterministic agent doubles, and regression coverage |
| `examples` | Safe, provider-neutral example configurations |

The solution file includes all four projects. Keep feature logic in Core so the CLI and GUI use the same pipeline.

## Local setup and validation

Use the pinned SDK in `global.json` and the public source in `nuget.config`:

```bash
dotnet restore CoffeeTalk.sln --configfile nuget.config
dotnet build CoffeeTalk.sln --configuration Release --no-restore
dotnet test CoffeeTalk.sln --configuration Release --no-build
dotnet list CoffeeTalk.sln package --vulnerable --include-transitive
```

The GitHub Actions workflow runs the same restore/build/test flow, collects coverage, and fails on known vulnerable packages. Run focused tests while iterating, then run the full solution checks before opening a pull request. `dotnet format --verify-no-changes` is not currently a repository gate because the existing codebase contains unrelated formatting debt.

Test analyzer policy: the test project permits CA1707 for readable underscore-separated test names and CA1859/CA1861 for intentionally small fixtures. Correctness, xUnit, nullable, and production-project analyzers remain enforced; do not broaden these exceptions to application code.

To run locally, use an ignored `CoffeeTalk/appsettings.json` or environment variables. The CLI is started with:

```bash
dotnet run --project CoffeeTalk/CoffeeTalk.CLI.csproj
```

The desktop app is started with:

```bash
dotnet run --project CoffeeTalk.Gui/CoffeeTalk.Gui.csproj
```

## Change workflow

1. Create a focused branch (for example, `feature/<short-name>` or `codex/<short-name>`).
2. Keep the change narrow and update tests and documentation in the same pull request.
3. Prefer deterministic `TestAIAgent`-based tests for orchestration, prompt construction, streaming fallback, termination, and tool behavior; live provider calls are not required for the test suite.
4. Run the full validation commands above and inspect `git diff --check`.
5. Open a pull request with a concise summary, validation results, and the issue it closes. Do not merge directly to `main`.

## Agentic and security invariants

- `CoffeeTalk.Core` is the source of truth for behavior shared by the CLI and GUI; do not fork orchestration logic in either UI.
- Persona and orchestrator requests use explicitly reconstructed, bounded stateless context. Keep `AgentContextPolicy` limits and visible truncation markers intact unless the change includes new budget tests and documentation.
- System/developer instructions must not be generated from user messages, document text, memories, or model output. Treat all of those as untrusted reference data.
- Markdown tools may edit the shared document and save through the configured workspace/export resolver. Do not add arbitrary filesystem or network capabilities to persona tools without a separate threat-model review.
- Workspace memory is opt-in, local, size-limited, workspace-scoped, and untrusted. It must never be treated as instructions or silently shared across workspaces.
- Do not weaken generic error messages or expose provider exceptions, prompts, API keys, or raw sensitive payloads in the GUI, CLI, telemetry, tests, or documentation.
- Preserve request/tool telemetry and explicit conversation termination reasons when changing execution paths.
- Configuration files containing secrets are ignored by git. Use environment variables or the checked-in example/template files; never commit a real key.

## Documentation

Update `README.md` when commands, configuration, architecture, or user-visible behavior changes. Keep examples provider-neutral and safe to copy. Add an entry to an existing issue rather than maintaining a separate TODO list in documentation.

Thank you for contributing thoughtfully to CoffeeTalk.
