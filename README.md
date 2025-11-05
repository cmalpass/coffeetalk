# ☕ CoffeeTalk

A .NET CLI application that orchestrates multi-persona LLM conversations using Microsoft Agent Framework. Configure multiple AI personas with unique system prompts and watch them engage in dynamic conversations to explore topics and reach conclusions through collaborative document creation.

## Features

### Core Capabilities
- **Multiple LLM Providers**: Support for OpenAI, Azure OpenAI, and Ollama (local models)
- **Configurable Personas**: Define multiple personas with unique system prompts and conversation styles
- **Dynamic Persona Generation**: Automatically generate topic-specific personas at runtime using AI
- **AI-Directed Orchestration**: Optional orchestrator agent intelligently selects which persona should speak next based on conversation needs
- **Collaborative Document Creation**: Personas work together to create a shared markdown document using tool calling
- **Editor Agent**: Automatic document refinement to maintain quality, conciseness, and professional structure
- **Rate Limiting**: Configure request and token limits to manage API usage
- **Retry Handling**: Automatic retry with exponential backoff for API rate limits (HTTP 429)
- **Flexible Conversation Modes**: Choose between orchestrated (AI-directed) or round-robin (sequential) conversation flow
- **Built on Microsoft Agent Framework**: Leverages Microsoft's Agent Framework for robust agentic AI integration

## Prerequisites

- .NET 8.0 SDK or later
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

### 3. Build and Run

```bash
cd CoffeeTalk
dotnet build
dotnet run
```

### 4. Start a Conversation

When prompted, enter a topic:

```
What would you like the personas to discuss?
Topic: How can we improve team collaboration?
```

Watch the personas engage in a multi-perspective discussion and collaboratively build a markdown document!

### 5. Review the Output

The conversation is auto-saved to `conversation.md` in the CoffeeTalk directory.

## Configuration

> **💡 Tip**: See [`CoffeeTalk/appsettings.example.json`](CoffeeTalk/appsettings.example.json) for a complete configuration example with all available options and detailed inline comments.

### LLM Provider Options

CoffeeTalk supports three LLM provider types, each with different configuration requirements.

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

The document is maintained in memory during the conversation and auto-saved to `conversation.md` when complete.

### Additional Settings

- **MaxConversationTurns**: Maximum number of conversation rounds (default: 10). This is multiplied by the number of personas in round-robin mode, or used as a total turn limit in orchestrated mode.
- **ShowThinking**: Display thinking indicators during responses (default: true). When enabled, you see `💭 Thinking...` while the LLM processes.

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
CoffeeTalk/
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
│   ├── RateLimiter.cs               # Request/token throttling
│   └── RetryHandler.cs              # HTTP 429 retry logic
├── appsettings.json                 # Default configuration
├── appsettings.orchestrated.json    # Orchestrated mode example
├── appsettings.azureopenai.json     # Azure OpenAI example
├── appsettings.ollama.json          # Ollama example
├── conversation.md                  # Auto-saved conversation output
└── Program.cs                       # Application entry point

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
2. **Kernel Setup**: A Semantic Kernel instance is created with the configured LLM provider
3. **Persona Creation**: Each persona is initialized with its unique system prompt
4. **Tool Verification**: The system verifies that personas can use markdown collaboration tools
5. **Conversation Loop**:
   - User provides a topic
   - Personas take turns responding in sequence
   - Each response builds on the conversation history and document state
   - Personas use tools to collaboratively edit the shared markdown document
   - Conversation continues until a conclusion is reached or max turns are hit
6. **Auto-Save**: The collaborative document is saved to `conversation.md`

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
5. **Completion Detection**: Orchestrator recognizes when goals are achieved

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
- [.NET 8](https://dotnet.microsoft.com/) - Runtime platform

## Support

- **Issues**: [GitHub Issues](https://github.com/cmalpass/coffeetalk/issues)
- **Discussions**: [GitHub Discussions](https://github.com/cmalpass/coffeetalk/discussions)
- **Documentation**: See the guides in this repository

---

**Made with ☕ and 🤖**
