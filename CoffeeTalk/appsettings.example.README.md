# appsettings.example.json

This file provides a comprehensive example of all available CoffeeTalk configuration options with detailed inline comments.

## Using This File

### Quick Start

Copy the example file to create your configuration:

```bash
cp appsettings.example.json appsettings.json
```

Then edit `appsettings.json` to:
1. Add your API key
2. Choose your LLM provider
3. Customize personas and settings as needed

### About JSON Comments

The `appsettings.example.json` file includes `//` comments for documentation purposes. While these comments are not valid in strict JSON, they **are supported** by .NET's configuration system, which uses `System.Text.Json` with comment support enabled.

If you need strict JSON (no comments):
- Simply remove all lines starting with `//`
- Or copy specific sections without the comment lines

## Configuration Sections

### LlmProvider
Configure which AI service to use:
- **OpenAI**: Use OpenAI's API (gpt-4o, gpt-4o-mini, etc.)
- **Azure OpenAI**: Use Azure-hosted OpenAI models
- **Ollama**: Use local models (free, private, offline)

### Personas
Define the AI personalities that will participate in conversations. Each persona should have:
- Unique role and perspective
- Clear system prompt defining behavior
- Concise communication style

### DynamicPersonas
Automatically generate topic-specific personas:
- **Enabled**: Turn on/off dynamic generation
- **Count**: How many personas to generate (2-10)
- **Mode**: "augment" (add to configured) or "replace" (ignore configured)

### Orchestrator
Enable AI-directed conversation flow:
- **Enabled**: Use orchestrated mode vs round-robin
- **SystemPrompt**: Optional custom orchestrator instructions

### Editor
Automatic document quality maintenance:
- **Enabled**: Turn on/off editor agent
- **InterventionFrequency**: How often to review (every N turns)
- **SystemPrompt**: Optional custom editor instructions

### RateLimit
Control API usage and costs:
- **RequestsPerMinute**: Throttle request frequency
- **TokensPerMinute**: Throttle token usage
- **MaxRequestsPerConversation**: Total request cap
- **MaxTokensPerConversation**: Total token cap

### Retry
Handle rate limit errors automatically:
- **InitialDelaySeconds**: First retry delay
- **MaxRetries**: Maximum retry attempts
- **BackoffMultiplier**: Exponential backoff factor

### Tools
Control function calling behavior:
- **EnableFallbackJsonTools**: Use JSON tools if native calls fail
- **RequireToolsVerification**: Exit if tools don't work

### General Settings
- **MaxConversationTurns**: Conversation length limit
- **ShowThinking**: Display thinking indicators

## Environment Variables

Instead of hardcoding API keys, you can use environment variables:

**OpenAI:**
```bash
export OPENAI_API_KEY="sk-your-key-here"
```

**Azure OpenAI:**
```bash
export AZURE_OPENAI_API_KEY="your-key"
export AZURE_OPENAI_ENDPOINT="https://your-resource.openai.azure.com/"
export AZURE_OPENAI_DEPLOYMENT_NAME="your-deployment"
```

## Example Configurations

See the `examples/` directory for pre-configured setups:
- `product-team.json` - Product development team
- `philosophy.json` - Philosophical discussions
- `creative-writing.json` - Creative writing workshop
- `brainstorm.json` - Brainstorming sessions

## Minimal Configuration

The smallest valid configuration:

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
      "Name": "Alice",
      "SystemPrompt": "You are Alice, a helpful assistant."
    },
    {
      "Name": "Bob",
      "SystemPrompt": "You are Bob, a thoughtful analyst."
    }
  ]
}
```

All other settings will use sensible defaults.

## Further Documentation

- [Main README](../README.md) - Full project documentation
- [Quick Start Guide](../QUICKSTART.md) - Get started in 5 minutes
- [Usage Guide](../USAGE.md) - Detailed usage examples
- [Orchestrator Guide](../ORCHESTRATOR.md) - Orchestrated mode details
