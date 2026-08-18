# Agent guidance for CoffeeTalk

This file describes the repository contract for coding agents and other automated contributors. Read it together with `CONTRIBUTING.md` and the relevant issue before changing code.

## Start with repository state

- Fetch and inspect the latest `origin/main` when network access is available.
- Confirm the working tree and branch before editing; preserve unrelated user changes.
- Use a focused branch and one pull request per issue or cohesive remediation.
- Do not commit secrets, generated build output, `TestResults/`, local workspaces, or real `appsettings.json` files.

## Architecture boundaries

- `CoffeeTalk.Core` owns models, configuration, provider setup, orchestration, document tools, memory, persistence, telemetry, and shared services.
- `CoffeeTalk` is the Spectre.Console CLI. `CoffeeTalk.Gui` is the Photino/Blazor desktop UI. Both must call the shared Core pipeline.
- `CoffeeTalk.Tests` contains deterministic tests and the `TestAIAgent` double. Prefer it over live provider calls.
- The conversation pipeline is assembled by `ConversationPipelineBuilder`; inspect it before adding a new execution path.

## Required validation

```bash
dotnet restore CoffeeTalk.sln --configfile nuget.config
dotnet build CoffeeTalk.sln --configuration Release --no-restore
dotnet test CoffeeTalk.sln --configuration Release --no-build
dotnet list CoffeeTalk.sln package --vulnerable --include-transitive
git diff --check
```

The CI workflow also collects coverage and fails when the dependency vulnerability report contains an advisory. Run focused tests first, then the full solution checks before publishing a branch.

Test analyzer policy: `CoffeeTalk.Tests` intentionally allows CA1707 for descriptive underscore-separated test names and CA1859/CA1861 for small fixture allocations. Correctness-oriented analyzers, xUnit analyzers, nullable diagnostics, and all production-project analyzers remain enabled.

## Agent behavior and security invariants

1. Persona and orchestrator calls are stateless application-owned requests. `AgentContextPolicy` bounds the final prompt (24,000 characters), document (12,000), history (6,000), history entries (2,000), and current message (4,000). Changes to these limits require regression tests and documentation.
2. User topics, conversation history, Markdown documents, model responses, and memories are untrusted data. They may be quoted as context but must not become system/developer instructions.
3. Memory remains opt-in, local, workspace-scoped, size-limited, and explicitly untrusted. Never add automatic global recall or cross-workspace sharing without a data-flow and threat-model review.
4. Markdown tools are limited to the collaborative document and the configured export/workspace resolver. Do not introduce arbitrary paths, shell execution, network access, or hidden side effects.
5. Provider/API secrets come from environment variables or ignored local settings. Never log or assert real credentials. Telemetry should expose sizes and lifecycle state, not raw prompt or secret content.
6. Preserve generic user-facing failures, cancellation semantics, explicit termination reasons, retry budgets, and streaming fallback telemetry.

## Expected tests for changes

- Prompt/context changes: capture prompts with `TestAIAgent`, test multi-turn history, document truncation, and the maximum prompt bound.
- Orchestration changes: cover selection, consensus, termination, turn/failure budgets, and malformed model output.
- Tool or filesystem changes: test path containment, size limits, error handling, and telemetry.
- UI changes: add component/UI-state tests and keep the Core pipeline behavior covered independently.
- Dependency or CI changes: run restore, Release build, full tests, and the vulnerability check.

Keep pull requests small, explain the reasoning behind agentic behavior changes, and include the exact validation commands and results in the PR body.
