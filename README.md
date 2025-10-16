# ☕ CoffeeTalk

A .NET Semantic Kernel CLI application that orchestrates multi-persona LLM conversations. Configure multiple AI personas with unique system prompts and watch them engage in dynamic conversations to explore topics and reach conclusions.

## Features

- **Multiple LLM Providers**: Support for OpenAI-compatible APIs and Ollama
- **Configurable Personas**: Define multiple personas with unique system prompts and conversation styles
- **Conversation Orchestration**: Personas engage in multi-turn conversations until a goal is reached
- **Flexible Configuration**: JSON-based configuration for easy customization
- **Built on Semantic Kernel**: Leverages Microsoft's Semantic Kernel for robust LLM integration

## Prerequisites

- .NET 8.0 SDK or later
- OpenAI API key (for OpenAI provider) or Ollama running locally

## Package Restoration

This project is configured to use only the standard public NuGet package source (nuget.org) via the included `nuget.config` file. This ensures that the project can be restored consistently across different environments without requiring access to any private NuGet feeds.

The project will automatically restore packages from:
- https://api.nuget.org/v3/index.json

No additional NuGet source configuration is required.

## Quick Start

1. **Clone the repository**:
   ```bash
   git clone https://github.com/cmalpass/coffeetalk.git
   cd coffeetalk
   ```

2. **Configure the application**:
   Edit `CoffeeTalk/appsettings.json` to set your LLM provider and personas:

   ```json
   {
     "LlmProvider": {
       "Type": "openai",
       "ApiKey": "your-api-key-here",
       "Endpoint": "https://api.openai.com/v1",
       "ModelId": "gpt-4o-mini"
     },
     "Personas": [
       {
         "Name": "Alice",
         "SystemPrompt": "You are Alice, an optimistic and creative thinker..."
       },
       {
         "Name": "Bob",
         "SystemPrompt": "You are Bob, a pragmatic and analytical person..."
       }
     ],
     "MaxConversationTurns": 10,
     "ShowThinking": true
   }
   ```

   Alternatively, set the `OPENAI_API_KEY` environment variable:
   ```bash
   export OPENAI_API_KEY="your-api-key-here"
   ```

3. **Build and run**:
   ```bash
   cd CoffeeTalk
   dotnet build
   dotnet run
   ```

4. **Enter a topic** when prompted and watch the personas discuss!

## Configuration

### LLM Provider Options

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

#### Ollama
```json
{
  "LlmProvider": {
    "Type": "ollama",
    "Endpoint": "http://localhost:11434/v1",
    "ModelId": "llama2"
  }
}
```

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

### Additional Settings

- **MaxConversationTurns**: Maximum number of conversation rounds (default: 10)
- **ShowThinking**: Display thinking indicators during responses (default: true)

## Usage Examples

### Product Brainstorming
Configure creative and analytical personas to explore product ideas:
- Creative persona suggests innovative features
- Analytical persona evaluates feasibility
- Customer-focused persona considers user needs

### Problem Solving
Set up personas with different perspectives:
- Optimist highlights opportunities
- Pessimist identifies risks
- Pragmatist proposes actionable solutions

### Learning and Exploration
Create teacher and student personas:
- Expert explains concepts
- Curious learner asks clarifying questions

## Project Structure

```
CoffeeTalk/
├── Models/
│   ├── AppSettings.cs          # Configuration models
│   ├── LlmProviderConfig.cs    # LLM provider settings
│   └── PersonaConfig.cs        # Persona definitions
├── Services/
│   ├── ConversationOrchestrator.cs  # Manages multi-turn conversations
│   └── KernelBuilder.cs        # Builds Semantic Kernel instances
├── appsettings.json            # Configuration file
└── Program.cs                  # Application entry point
```

## How It Works

1. **Initialization**: The application loads configuration from `appsettings.json`
2. **Kernel Setup**: A Semantic Kernel instance is created with the configured LLM provider
3. **Persona Creation**: Each persona is initialized with its unique system prompt
4. **Conversation Loop**: 
   - User provides a topic
   - Personas take turns responding
   - Each response builds on the conversation history
   - Conversation continues until a conclusion is reached or max turns are hit
5. **Goal Detection**: The orchestrator detects when personas have reached a conclusion

## License

MIT License - see LICENSE file for details

## Contributing

Contributions are welcome! Please feel free to submit a Pull Request.
