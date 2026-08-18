# ☕ CoffeeTalk

CoffeeTalk is a .NET 9 application for orchestrating multi-persona LLM conversations with Microsoft's Agent Framework. It includes a Photino/Blazor desktop GUI and a Spectre.Console CLI. Configure personas with distinct system prompts, let them collaborate on a shared Markdown document, inspect live request/tool telemetry, and require persona consensus before a conversation concludes.

## Features

### Core Capabilities
- **Multiple LLM Providers**: Support for OpenAI, Azure OpenAI, and Ollama (local models)
- **Streaming Responses**: OpenAI and Azure OpenAI responses stream by default, with configurable buffered fallback
- **Configurable Personas**: Define multiple personas with unique system prompts and conversation styles
- **Dynamic Persona Generation**: Automatically generate topic-specific personas at runtime using AI
- **AI-Directed Orchestration**: Optional orchestrator agent intelligently selects which persona should speak next based on conversation needs
- **Collaborative Document Creation**: Personas work together to create a shared markdown document using tool calling
- **Editor Agent**: Automatic document refinement to maintain quality, conciseness, and professional structure
- **Rate Limiting**: Configure request and token limits to manage API usage
- **Retry Handling**: Automatic retry with exponential backoff for API rate limits (HTTP 429)
- **Flexible Conversation Modes**: Choose between orchestrated (AI-directed) or round-robin (sequential) conversation flow
- **Built on Microsoft Agent Framework**: Leverages Microsoft's Agent Framework for robust agentic AI integration
- **Stateless Agent Context**: Personas and the orchestrator receive explicitly reconstructed, bounded prompts; provider-managed thread state is not retained
- **Bounded Document Context**: Prompts include the current Markdown document and recent history within a documented 24,000-character budget, truncating oversized sections with a visible marker
- **Consensus Verification**: When the orchestrator proposes completion, every persona reviews the document and must agree
- **Request and Tool Telemetry**: View prompt/output sizes, token usage, first-output latency, duration, failures, and document tool calls
- **Mermaid Rendering**: Mermaid fenced blocks render in the GUI and exported Markdown previews

## Prerequisites

- .NET 9.0 SDK
- OpenAI API key (for OpenAI provider) or Ollama running locally

## Package Restoration

This project is configured to use only the standard public NuGet package source (nuget.org) via the included `nuget.config` file. This ensures that the project can be restored consistently across different environments without requiring access to any private NuGet feeds.

The project will automatically restore packages from:
- https://api.nuget.org/v3/index.json

No additional NuGet source configuration is required.

## Quick Start

### 1. Clone the repository

```bash
git clone https://github.com/cmalpass/coffeetalk.git
cd coffeetalk
```

### 2. Configure Your LLM Provider

Choose one of the following options:

#### Option A: OpenAI (Recommended for Getting Started)

Set your API key as an environment variable:

**Windows (PowerShell):**

```powershell
$env:OPENAI_API_KEY="sk-your-key-here"
```

**Linux/macOS:**

```bash
export OPENAI_API_KEY="sk-your-key-here"
```

Or edit `CoffeeTalk/appsettings.json` and add your key:

```json
{
  "LlmProvider": {
    "Type": "openai",
    "ApiKey": "sk-your-key-here",
    "Endpoint": "https://api.openai.com/v1",
    "ModelId": "gpt-4o-mini"
  }
}
```

#### Option B: Azure OpenAI

Edit `CoffeeTalk/appsettings.json` or use `appsettings.azureopenai.json` as a template:

```json
{
  "LlmProvider": {
    "Type": "azureopenai",
    "ApiKey": "<your-azure-api-key>",
    "Endpoint": "https://<your-resource>.openai.azure.com/",
    "DeploymentName": "<your-deployment>"
  }
}
```

#### Option C: Ollama (Local, No API Key Required)

1. Install Ollama from https://ollama.ai
2. Start Ollama: `ollama serve`
3. Pull a model: `ollama pull llama2`
4. Copy the Ollama config:

```bash
cp CoffeeTalk/appsettings.ollama.json CoffeeTalk/appsettings.json
```

### 3. Build and Run the CLI

```bash
dotnet build CoffeeTalk/CoffeeTalk.CLI.csproj
dotnet run --project CoffeeTalk/CoffeeTalk.CLI.csproj
```

To launch the desktop GUI:

```bash
dotnet run --project CoffeeTalk.Gui/CoffeeTalk.Gui.csproj
```

The GUI stores active workspace configuration and conversation data under the application data directory. The CLI loads the same workspace-oriented configuration model and also supports history, analytics, export, and memory commands.

### 4. Start a Conversation

In either interface, enter a topic when prompted. The GUI shows conversation messages, the live Markdown document, status updates, and compact telemetry rows. The CLI displays the conversation and operational events in the terminal.

```
What would you like the personas to discuss?
Topic: How can we improve team collaboration?
```

Watch the personas engage in a multi-perspective discussion and collaboratively build a markdown document!

### 5. Review the Output

The shared document is auto-saved to the active workspace. The CLI can additionally persist conversation history with `--save` and export the document with `--export-format`.

## Configuration

> **💡 Tip**: See [`CoffeeTalk/appsettings.example.json`](CoffeeTalk/appsettings.example.json) for a complete configuration example with all available options and detailed inline comments.

### Workspace memory (MVP)

CoffeeTalk's memory feature is **opt-in** and is disabled unless the workspace configuration explicitly enables it. Memories are plain-text notes stored locally inside the active workspace; they are not shared with other workspaces and are never uploaded by CoffeeTalk's memory store. Add the memory section to that workspace's `appsettings.json` to enable it:

```json
{
  "Memory": {
    "Enabled": true,
    "MaxEntries": 1000,
    "MaxEntrySizeBytes": 65536,
    "MaxTotalSizeBytes": 10485760,
    "MaxQueryLength": 512,
    "MaxResults": 20,
    "RetentionDays": 90
  }
}
```

The exact limits are deliberately conservative. Entries exceeding the configured byte, total-size, query, or workspace entry limit are rejected, and expired entries are removed by `memory purge`. Retention is not a legal or compliance guarantee: back up or delete the workspace data according to your own policy. Set `Enabled` back to `false` to stop memory recall and creation; existing entries remain available for explicit review or deletion until purged.

Manage only the active workspace's memories with:

```text
dotnet run -- memory list
dotnet run -- memory search "deployment decision"
dotnet run -- memory show <id>
dotnet run -- memory add --text "The release branch is cut on Tuesdays."
dotnet run -- memory edit <id> --text "Updated note"
dotnet run -- memory delete <id>
dotnet run -- memory purge
```

`delete` and `purge` always ask for confirmation and do nothing when declined. Memory commands do not run an LLM setup wizard. If memory is disabled, commands that would create or recall context fail with a clear opt-in message; explicit management remains available so that you can inspect and remove local data. Use `memory purge` after changing retention settings or disabling the feature.

Memory text is **untrusted reference context**, not instructions. It may contain prompt-injection text or stale claims. CoffeeTalk must never treat a memory as a system/developer instruction, and users should review entries before relying on them. The MVP intentionally does not provide embeddings, semantic/vector search, automatic global memory, automatic deletion of arbitrary conversation content, or cross-workspace recall. Memory is not a source of truth and is not a substitute for backups.

Embeddings are a future upgrade only if measured textual-search quality is insufficient for real workspaces. Any upgrade must preserve the local/opt-in boundary, define an explicit provider and data-flow review, and pass a quality/latency/storage comparison against the current textual search before it is enabled by default.

### LLM Provider Options

CoffeeTalk supports three LLM provider types, each with different configuration requirements.

### Streaming Responses

Streaming is enabled by default for OpenAI and Azure OpenAI. Set `StreamingFallback` to
`"error"` to fail instead of using the buffered `RunAsync` path when streaming is unavailable;
the default `"buffered"` value preserves compatibility. Ollama streaming is conditional on
the active model/server and is not assumed to be universally supported. To opt in after
verifying a live Ollama setup, set `StreamingSupported` to `true`; otherwise CoffeeTalk uses
the buffered path.

#### OpenAI

```json
{
  "LlmProvider": {
    "Type": "openai",
    "ApiKey": "sk-...",
    "Endpoint": "https://api.openai.com/v1",
    "ModelId": "gpt-4o-mini"
  }
}
```

**Environment Variables:**
- `OPENAI_API_KEY`: Alternative to setting `ApiKey` in configuration

**Recommended Models:**
- `gpt-4o` - Best quality for complex discussions
- `gpt-4o-mini` - Good balance of quality and cost
- `gpt-3.5-turbo` - Fast and economical

#### Azure OpenAI

```json
{
  "LlmProvider": {
    "Type": "azureopenai",
    "ApiKey": "<your-azure-openai-api-key>",
    "Endpoint": "https://<your-resource-name>.openai.azure.com/",
    "DeploymentName": "<your-chat-deployment-name>",
    "ModelId": "<your-model-id>"
  }
}
```

**Environment Variables:**
- `AZURE_OPENAI_API_KEY`: Alternative to setting `ApiKey`
- `AZURE_OPENAI_ENDPOINT`: Alternative to setting `Endpoint`
- `AZURE_OPENAI_DEPLOYMENT_NAME`: Alternative to setting `DeploymentName`
- `AZURE_OPENAI_CHAT_DEPLOYMENT_NAME`: Alternative deployment name variable

**Required Fields:**
- `ApiKey` - Your Azure OpenAI API key
- `Endpoint` - Your Azure OpenAI resource endpoint
- `DeploymentName` - Your chat completion deployment name

#### Ollama (Local Models)

```json
{
  "LlmProvider": {
    "Type": "ollama",
    "Endpoint": "http://localhost:11434",
    "ModelId": "llama2"
  }
}
```

**Prerequisites:**
1. Install Ollama from https://ollama.ai
2. Start Ollama: `ollama serve`
3. Pull a model: `ollama pull llama2` (or `gemma2:9b`, `mistral`, etc.)

**Benefits:**
- Privacy - all processing stays local
- No API costs
- Offline usage
- Unlimited requests

### Persona Configuration

Each persona requires:

- **Name**: A unique identifier for the persona
- **SystemPrompt**: Instructions that define the persona's behavior, tone, and approach

Example:

```json
{
  "Name": "Critic",
  "SystemPrompt": "You are a thoughtful critic who identifies potential issues and asks probing questions. Be constructive but thorough in your analysis."
}
```

### Dynamic Persona Generation

CoffeeTalk can automatically generate topic-specific personas using AI, eliminating the need to manually configure personas for every discussion.

```json
{
  "DynamicPersonas": {
    "Enabled": true,
    "Count": 4,
    "Mode": "augment"
  }
}
```

**Configuration Options:**

- **Enabled**: Set to `true` to enable dynamic persona generation
- **Count**: Number of personas to generate (2-10, will be clamped)
- **Mode**: 
  - `"augment"` - Adds generated personas to existing configured personas
  - `"replace"` - Ignores configured personas and uses only generated ones

**How It Works:**

1. You provide a topic when starting the conversation
2. An AI generates personas specifically suited to that topic
3. Each generated persona has a distinct role, expertise, and perspective
4. The system ensures no duplicate names and complementary viewpoints

**Example Generated Personas:**

For topic "How to improve team collaboration":
- **ProductLead** - Prioritizes user value and scope clarity
- **EngArchitect** - Evaluates technical feasibility and systems design
- **DataAnalyst** - Provides evidence-based insights and metrics
- **UXDesigner** - Focuses on usability and user experience

**Benefits:**

- No need to manually configure personas for each topic
- Personas are tailored to the specific discussion
- Diverse perspectives automatically included
- Can combine with pre-configured personas (augment mode)

**Fallback Behavior:**

If dynamic generation fails, the system falls back to using configured personas from `appsettings.json`.

### Orchestrator Configuration

Enable AI-directed conversation flow where an orchestrator agent intelligently selects which persona should speak next:

```json
{
  "Orchestrator": {
    "Enabled": true,
    "SystemPrompt": "You are a conversation orchestrator..."
  }
}
```

**Benefits of Orchestrated Mode**:
- More natural conversation flow
- Personas speak when their expertise is most relevant
- Balanced participation tracking
- Dynamic adaptation to conversation needs
- Better token efficiency (no redundant contributions)

**Mode Comparison**:
- **Round-robin** (default): Each persona speaks in sequence every turn
- **Orchestrated**: AI selects the most appropriate persona for each contribution

See `appsettings.orchestrated.json` for a complete example.

### Editor Agent

Enable automatic document editing and refinement to maintain quality and conciseness:

```json
{
  "Editor": {
    "Enabled": true,
    "InterventionFrequency": 3
  }
}
```

**How the Editor Works**:
- **Periodic Review**: After every N turns (configured by `InterventionFrequency`), the editor reviews the document
- **Ruthless Editing**: Removes verbose text, consolidates redundant sections, shortens paragraphs
- **Structure Refinement**: Merges duplicate headings, ensures logical flow
- **Quality Focus**: Converts narrative prose into clear, direct statements
- **Professional Output**: Keeps the document actionable and focused on the main goal

**Benefits**:
- Prevents the document from becoming a verbose narrative essay
- Maintains conciseness throughout the conversation
- Consolidates repetitive contributions from multiple personas
- Ensures the final output is polished and professional

The editor uses the same markdown tools as personas to restructure and refine content.

### Rate Limiting

Configure request and token limits to manage API usage:

```json
{
  "RateLimit": {
    "RequestsPerMinute": 30,
    "TokensPerMinute": 40000,
    "MaxRequestsPerConversation": 100,
    "MaxTokensPerConversation": 150000
  }
}
```

### Retry Configuration

Configure automatic retry behavior for HTTP 429 (rate limit) errors:

```json
{
  "Retry": {
    "InitialDelaySeconds": 30,
    "MaxRetries": 5,
    "BackoffMultiplier": 2.0
  }
}
```

When a rate limit (HTTP 429) is encountered:
- **InitialDelaySeconds**: First retry waits this many seconds (default: 30)
- **MaxRetries**: Maximum number of retry attempts (default: 5)
- **BackoffMultiplier**: Each retry multiplies the delay by this factor (default: 2.0)

Example retry sequence with defaults:
1. Initial failure → wait 30s
2. Retry 1 fails → wait 60s
3. Retry 2 fails → wait 120s
4. Retry 3 fails → wait 240s
5. Retry 4 fails → wait 480s
6. Retry 5 fails → throw exception

### Collaborative Document Tools

Personas can collaborate on a shared markdown document using these tools:

- `SetTitle`: Set the document title
- `AddHeading`: Add a new heading
- `AppendParagraph`: Add content to the document
- `InsertAfterHeading`: Insert content under a specific heading
- `ListHeadings`: View current document structure
- `ReplaceSection`: Replace the content under a heading
- `SaveDocument`: Persist the current document

The document is shared and locked across agents. Personas, the orchestrator, and optional editor/fact-checking agents receive the current full Markdown snapshot. The GUI refreshes the document while responses stream, and Mermaid blocks are rendered in the preview.

The document is maintained in memory during the conversation and auto-saved to the active workspace when complete.

### Additional Settings

- **MaxConversationTurns**: Maximum number of conversation rounds (default: 10). This is multiplied by the number of personas in round-robin mode, or used as a total turn limit in orchestrated mode.
- **ShowThinking**: Display thinking indicators during responses (default: true). When enabled, you see `💭 Thinking...` while the LLM processes.
- **StreamingEnabled**: Enable streaming responses when the provider supports it (default: true)
- **StreamingFallback**: Use `"buffered"` when streaming is unavailable, or `"error"` to fail instead

### Request and tool telemetry

The active event sink records each LLM and document-tool operation. Events include a request or tool ID, operation name, prompt/argument size, output/result size, estimated tokens, provider-reported usage when available, first-output latency, total duration, completion, and failure details. The GUI groups these events into compact timeline rows; the CLI writes them to the terminal. Streaming reasoning chunks are surfaced as thinking events when the provider exposes them.

Telemetry measures the application payload passed to the Agent Framework. Providers may add system instructions or other transport metadata, so provider-reported input usage can be larger than the application estimate.

### Consensus verification

Consensus verification applies to orchestrated mode. When the orchestrator returns its completion signal, CoffeeTalk asks every configured persona to review the full document and recent discussion concurrently. Each persona must return `CONSENSUS: YES` or `CONSENSUS: NO` with a short reason. The conversation ends only when all personas agree; dissenting reasons are sent back to the orchestrator as the next follow-up message. A failed or malformed consensus check is treated as a request to revise rather than as agreement.

## Usage Examples

CoffeeTalk excels at facilitating multi-perspective discussions. Here are some effective usage patterns:

### Product Brainstorming

Configure creative and analytical personas to explore product ideas:

```json
{
  "Personas": [
    {
      "Name": "ProductManager",
      "SystemPrompt": "You are a product manager focused on user value, market fit, and strategic priorities. Keep responses user-focused and concise."
    },
    {
      "Name": "Engineer",
      "SystemPrompt": "You are a software engineer focused on technical feasibility, architecture, and implementation. Keep responses technical and concise."
    },
    {
      "Name": "Designer",
      "SystemPrompt": "You are a UX designer focused on user experience, usability, and interface design. Keep responses user-centric and concise."
    }
  ]
}
```

**Effective Topics:**
- "Design a user onboarding flow for our app"
- "Should we add feature X or focus on improving feature Y?"
- "How can we reduce time-to-value for new users?"

### Problem Solving

Set up personas with different perspectives:

```json
{
  "Personas": [
    {
      "Name": "Optimist",
      "SystemPrompt": "You highlight opportunities and positive outcomes. You're enthusiastic about possibilities while staying realistic."
    },
    {
      "Name": "Realist",
      "SystemPrompt": "You identify practical challenges and constraints. You're pragmatic and focus on what's achievable."
    },
    {
      "Name": "Synthesizer",
      "SystemPrompt": "You bring different viewpoints together and propose balanced solutions. You seek common ground."
    }
  ]
}
```

**Effective Topics:**
- "How to improve team productivity?"
- "What's the best approach to reduce customer churn?"
- "How should we prioritize technical debt vs new features?"

### Learning and Exploration

Create teacher and student personas:

```json
{
  "Personas": [
    {
      "Name": "Expert",
      "SystemPrompt": "You are a knowledgeable expert who explains concepts clearly using examples and analogies. You encourage understanding."
    },
    {
      "Name": "Learner",
      "SystemPrompt": "You are a curious learner who asks clarifying questions and explores implications. You seek deeper understanding."
    }
  ]
}
```

**Effective Topics:**
- "Explain the CAP theorem and its practical implications"
- "How does machine learning differ from traditional programming?"
- "What are the key principles of good API design?"

### Creative Writing

Enable dynamic personas or configure creative roles:

```json
{
  "DynamicPersonas": {
    "Enabled": true,
    "Count": 3,
    "Mode": "replace"
  }
}
```

**Effective Topics:**
- "Write a short story about an AI discovering emotions"
- "Create a compelling villain character for a sci-fi novel"
- "Develop a mystery plot with an unexpected twist"

## Advanced Features

### Complete Configuration Example

Here's a fully configured example using all available features:

```json
{
  "LlmProvider": {
    "Type": "openai",
    "ApiKey": "sk-...",
    "Endpoint": "https://api.openai.com/v1",
    "ModelId": "gpt-4o-mini"
  },
  "Personas": [
    {
      "Name": "Strategist",
      "SystemPrompt": "You are a strategic thinker focused on high-level planning and frameworks."
    },
    {
      "Name": "Implementer",
      "SystemPrompt": "You are detail-oriented, focused on practical execution and specifics."
    }
  ],
  "DynamicPersonas": {
    "Enabled": true,
    "Count": 2,
    "Mode": "augment"
  },
  "Orchestrator": {
    "Enabled": true
  },
  "Editor": {
    "Enabled": true,
    "InterventionFrequency": 3
  },
  "RateLimit": {
    "RequestsPerMinute": 30,
    "TokensPerMinute": 40000,
    "MaxRequestsPerConversation": 100,
    "MaxTokensPerConversation": 150000
  },
  "Retry": {
    "InitialDelaySeconds": 30,
    "MaxRetries": 5,
    "BackoffMultiplier": 2.0
  },
  "Tools": {
    "EnableFallbackJsonTools": true,
    "RequireToolsVerification": true
  },
  "MaxConversationTurns": 12,
  "ShowThinking": true
}
```

This configuration:
- Uses OpenAI with GPT-4o-mini
- Starts with 2 configured personas
- Generates 2 additional topic-specific personas (total: 4)
- Enables AI-directed orchestration
- Runs editor review every 3 turns
- Sets rate limits for API usage
- Configures retry behavior for rate limit errors
- Requires tool verification before starting
- Allows up to 12 conversation turns

### Using Pre-configured Examples

The `examples/` directory contains ready-to-use configurations:

```bash
# Product team discussion
cp examples/product-team.json CoffeeTalk/appsettings.json

# Philosophy discussion
cp examples/philosophy.json CoffeeTalk/appsettings.json

# Creative writing session
cp examples/creative-writing.json CoffeeTalk/appsettings.json

# Brainstorming session
cp examples/brainstorm.json CoffeeTalk/appsettings.json

# Rude experts debate (8 confrontational but knowledgeable personas)
cp examples/rude-experts.json CoffeeTalk/appsettings.json
```

Don't forget to add your API key to the copied file!

### Tips for Effective Conversations

**1. Design Clear Persona Roles**

Each persona should have a distinct perspective. Good prompts include:
- Role definition (what they are)
- Focus areas (what they care about)
- Behavioral guidance (how they contribute)
- Style instructions (concise, analytical, creative, etc.)

**2. Use Dynamic Personas for Exploration**

When you're not sure what perspectives you need, enable dynamic personas:

```json
{
  "DynamicPersonas": {
    "Enabled": true,
    "Count": 4,
    "Mode": "replace"
  }
}
```

**3. Enable Orchestration for Natural Flow**

Orchestrated mode works best when:
- Personas have clearly differentiated roles
- Topics benefit from varied expertise at different stages
- You want to avoid redundant contributions

**4. Use the Editor for Long Conversations**

Enable the editor for conversations with many turns:

```json
{
  "Editor": {
    "Enabled": true,
    "InterventionFrequency": 3
  }
}
```

**5. Manage API Costs**

Control costs with rate limiting and turn limits:

```json
{
  "MaxConversationTurns": 8,
  "RateLimit": {
    "RequestsPerMinute": 10,
    "TokensPerMinute": 20000
  }
}
```

Or use Ollama for unlimited local usage.

## Project Structure

```
CoffeeTalk.Core/
├── Models/
│   ├── AppSettings.cs               # Main configuration model
│   ├── LlmProviderConfig.cs         # LLM provider settings
│   ├── PersonaConfig.cs             # Persona definitions
│   ├── DynamicPersonasConfig.cs     # Dynamic persona generation config
│   ├── OrchestratorConfig.cs        # Orchestrator settings
│   ├── EditorConfig.cs              # Editor agent configuration
│   ├── RateLimitConfig.cs           # Rate limiting configuration
│   ├── RetryConfig.cs               # Retry behavior settings
│   └── ToolsConfig.cs               # Tool verification settings
├── Services/
│   ├── AgentConversationOrchestrator.cs  # Manages conversation flow using Agent Framework
│   ├── AgentOrchestrator.cs         # AI-directed speaker selection agent
│   ├── AgentEditor.cs               # Document editing and refinement agent
│   ├── AgentPersona.cs              # Individual persona agent wrapper
│   ├── AgentPersonaGenerator.cs     # Dynamic persona generation agent
│   ├── AgentBuilder.cs              # Builds AIAgent instances for different providers
│   ├── CollaborativeMarkdownDocument.cs  # Shared document state
│   ├── MarkdownToolFunctions.cs     # Document editing tools as AIFunctions
│   ├── RequestTelemetry.cs           # LLM request lifecycle telemetry
│   ├── ToolTelemetry.cs              # Document tool lifecycle telemetry
│   ├── RateLimiter.cs               # Request/token throttling
│   └── RetryHandler.cs              # HTTP 429 retry logic
├── CoffeeTalk.Gui/                  # Photino/Blazor desktop application
├── CoffeeTalk/                      # Spectre.Console CLI
├── CoffeeTalk.Tests/                # Unit and component tests
└── examples/                        # Ready-to-use configurations

examples/
├── product-team.json                # Product development team personas
├── philosophy.json                  # Philosophical discussion personas
├── creative-writing.json            # Creative writing workshop personas
├── brainstorm.json                  # Brainstorming session personas
├── rude-experts.json                # Confrontational experts (8 rude personas + 1 nice one)
└── README.md                        # Examples documentation
```

## Troubleshooting

### API Key Errors

**Error:** "OpenAI API key not found in config or environment"

**Solution:**
- Set the `OPENAI_API_KEY` environment variable, OR
- Add `ApiKey` to `LlmProvider` section in `appsettings.json`

**Error:** "Azure OpenAI requires ApiKey, Endpoint, and DeploymentName"

**Solution:**
- Ensure all three required fields are set in configuration
- Or set environment variables: `AZURE_OPENAI_API_KEY`, `AZURE_OPENAI_ENDPOINT`, `AZURE_OPENAI_DEPLOYMENT_NAME`

### Connection Errors

**Error:** "Name or service not known" or network errors

**Solution:**
- Check your internet connection
- For OpenAI: Verify endpoint is `https://api.openai.com/v1`
- For Azure: Verify your endpoint URL is correct
- For Ollama: Ensure Ollama is running (`ollama serve`)

### Tool Calling Issues

**Issue:** Tools or function calling not working properly

**Solution:**
- Verify your model supports function calling
  - OpenAI: Use `gpt-4o`, `gpt-4o-mini`, `gpt-3.5-turbo`, or newer
  - Azure: Ensure deployment uses a function-calling capable model
  - Ollama: Some models may not support function calling
- Try a different model with better function calling support

### Empty or Poor Quality Responses

**Issue:** Personas give very short, repetitive, or off-topic responses

**Solution:**
- Use more capable models (`gpt-4o` instead of `gpt-3.5-turbo`)
- Review and improve system prompts for clarity
- Ensure topics are specific and well-defined
- For Ollama: Try larger models like `llama2:13b` or `mistral:7b`

### Rate Limit Errors

**Error:** HTTP 429 errors or "Rate limit exceeded"

**Solution:**
- The retry handler should automatically handle these
- Adjust retry configuration:
  ```json
  {
    "Retry": {
      "InitialDelaySeconds": 60,
      "MaxRetries": 10,
      "BackoffMultiplier": 2.0
    }
  }
  ```
- Configure rate limiting:
  ```json
  {
    "RateLimit": {
      "RequestsPerMinute": 10,
      "TokensPerMinute": 20000
    }
  }
  ```
- Upgrade your API tier
- Use Ollama for unlimited local requests

### Conversation Ends Too Early

**Issue:** Conversation completes after only a few turns

**Solution:**
- Increase `MaxConversationTurns` in configuration
- In orchestrated mode, the orchestrator may detect completion early
- Check if personas are using conclusion phrases prematurely
- Review persona prompts to encourage deeper exploration

### Dynamic Persona Generation Fails

**Error:** "Dynamic persona generation failed"

**Solution:**
- The system automatically falls back to configured personas
- Ensure your LLM provider is accessible
- Check that you have sufficient API quota
- Try with a more capable model

### Memory or Performance Issues

**Issue:** Application runs slowly or uses excessive memory

**Solution:**
- Reduce `MaxConversationTurns`
- Limit the number of personas (4-6 is optimal)
- Enable rate limiting to control request frequency
- Use a more efficient model (e.g., `gpt-4o-mini` instead of `gpt-4o`)
- For Ollama: Ensure adequate RAM for the model size

## How It Works

### Round-Robin Mode (Default)

1. **Initialization**: The application loads configuration from `appsettings.json`
2. **Agent Setup**: Microsoft Agent Framework agents are created with the configured LLM provider
3. **Persona Creation**: Each persona is initialized with its unique system prompt
4. **Tool Verification**: The system verifies that personas can use markdown collaboration tools
5. **Conversation Loop**:
   - User provides a topic
   - Personas take turns responding in sequence
   - Each response builds on bounded conversation history and document state; request telemetry reports the effective prompt size
   - Personas use tools to collaboratively edit the shared markdown document
   - Conversation continues until a conclusion is reached or max turns are hit
6. **Auto-Save**: The collaborative document is saved to the active workspace

### Orchestrated Mode

1. **Initialization**: Same as round-robin, plus orchestrator agent creation
2. **Dynamic Selection**:
   - Orchestrator analyzes conversation state, document progress, and participation balance
   - Selects the most appropriate persona for the current need
   - Provides reasoning for the selection
3. **Targeted Contributions**:
   - Selected persona responds based on their expertise
   - Document state is shared with all personas
   - Tools are used to make collaborative edits
4. **Adaptive Flow**:
   - Conversation adapts to needs (structure → content → refinement → conclusion)
   - No rigid turn order
   - Better token efficiency
5. **Completion Proposal**: Orchestrator signals when it believes the goals are achieved, then all personas verify consensus

## Further Reading

- **[QUICKSTART.md](QUICKSTART.md)** - Get up and running in 5 minutes
- **[USAGE.md](USAGE.md)** - Detailed usage guide with examples and tips
- **[ORCHESTRATOR.md](ORCHESTRATOR.md)** - In-depth guide to orchestrated mode
- **[examples/README.md](examples/README.md)** - Example configurations for various use cases
- **[CONTRIBUTING.md](CONTRIBUTING.md)** - Contribution guidelines

## Contributing

Contributions are welcome! Whether it's:

- Bug reports or feature requests (open an issue)
- Documentation improvements
- New example configurations
- Code contributions (submit a pull request)

Please see [CONTRIBUTING.md](CONTRIBUTING.md) for guidelines.

## License

MIT License - see [LICENSE](LICENSE) file for details.

## Acknowledgments

Built with:
- [Microsoft Agent Framework](https://github.com/microsoft/agent-framework) - AI agent orchestration framework
- [.NET 9](https://dotnet.microsoft.com/) - Runtime platform

## Support

- **Issues**: [GitHub Issues](https://github.com/cmalpass/coffeetalk/issues)
- **Discussions**: [GitHub Discussions](https://github.com/cmalpass/coffeetalk/discussions)
- **Documentation**: See the guides in this repository

---

**Made with ☕ and 🤖**
