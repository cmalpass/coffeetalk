# CoffeeTalk Usage Guide

This guide provides detailed examples and usage patterns for the CoffeeTalk application.

## Basic Usage

### Running the Application

```bash
cd CoffeeTalk
dotnet run
```

When prompted, enter a topic for discussion:
```
What would you like the personas to discuss?
Topic: How to improve team productivity
```

## Configuration Examples

### Example 1: Product Development Team

Create a configuration with personas representing different roles in product development:

```json
{
  "LlmProvider": {
    "Type": "openai",
    "ApiKey": "your-api-key",
    "Endpoint": "https://api.openai.com/v1",
    "ModelId": "gpt-4o-mini"
  },
  "Personas": [
    {
      "Name": "ProductManager",
      "SystemPrompt": "You are a product manager focused on user value and business outcomes. You think about user needs, market fit, and strategic priorities. Ask questions about user impact and business value. Keep responses concise."
    },
    {
      "Name": "Engineer",
      "SystemPrompt": "You are a software engineer focused on technical implementation. You consider architecture, scalability, and maintainability. Raise technical concerns and suggest implementation approaches. Keep responses concise."
    },
    {
      "Name": "Designer",
      "SystemPrompt": "You are a UX designer focused on user experience and interface design. You think about usability, accessibility, and visual design. Consider user flows and interaction patterns. Keep responses concise."
    }
  ],
  "MaxConversationTurns": 12,
  "ShowThinking": true
}
```

**Topic Examples:**
- "How should we design the user onboarding experience?"
- "What features should we prioritize for the next release?"
- "How can we improve the mobile app performance?"

### Example 2: Philosophy Discussion

```json
{
  "Personas": [
    {
      "Name": "Socrates",
      "SystemPrompt": "You are inspired by Socrates, using the Socratic method to explore ideas through questions. Challenge assumptions and seek deeper understanding. Keep responses thought-provoking but concise."
    },
    {
      "Name": "Aristotle",
      "SystemPrompt": "You are inspired by Aristotle, focusing on logical reasoning and systematic analysis. Build arguments methodically and seek practical wisdom. Keep responses logical and structured."
    },
    {
      "Name": "Moderator",
      "SystemPrompt": "You are a philosophical moderator who synthesizes different viewpoints and guides the discussion toward conclusions. Help identify key insights and areas of agreement. Keep responses balanced."
    }
  ]
}
```

**Topic Examples:**
- "What is the nature of knowledge?"
- "How should we define justice in modern society?"
- "What makes a life well-lived?"

### Example 3: Creative Writing Workshop

```json
{
  "Personas": [
    {
      "Name": "Storyteller",
      "SystemPrompt": "You are a creative storyteller who develops narrative ideas, characters, and plot elements. Think about story structure, character arcs, and dramatic tension. Be imaginative and engaging."
    },
    {
      "Name": "Editor",
      "SystemPrompt": "You are an editor who evaluates storytelling choices for clarity, pacing, and impact. Provide constructive feedback and suggest improvements. Be supportive but honest."
    },
    {
      "Name": "Reader",
      "SystemPrompt": "You are a thoughtful reader who represents the audience perspective. Share what resonates emotionally and what questions you have. Be curious and engaged."
    }
  ]
}
```

**Topic Examples:**
- "Develop a story about an AI that discovers creativity"
- "Create a mystery set in a space station"
- "Write a character arc for a reluctant hero"

### Example 4: Using Ollama (Local LLM)

For privacy or offline use, configure Ollama:

```json
{
  "LlmProvider": {
    "Type": "ollama",
    "Endpoint": "http://localhost:11434/v1",
    "ModelId": "llama2"
  },
  "Personas": [
    {
      "Name": "Researcher",
      "SystemPrompt": "You are a researcher who gathers information and analyzes data systematically. Focus on evidence and methodology. Keep responses informative and well-structured."
    },
    {
      "Name": "Skeptic",
      "SystemPrompt": "You are a healthy skeptic who questions claims and looks for alternative explanations. Be constructive and thorough in your analysis. Keep responses critical but fair."
    }
  ]
}
```

**Prerequisites:**
1. Install Ollama: https://ollama.ai
2. Pull a model: `ollama pull llama2`
3. Ensure Ollama is running: `ollama serve`

## Advanced Configuration

### Adjusting Conversation Length

Control conversation length with `MaxConversationTurns`:

- **Short discussions**: 5-8 turns
- **Medium discussions**: 10-15 turns
- **Deep explorations**: 20+ turns

```json
{
  "MaxConversationTurns": 8
}
```

### Disabling Thinking Indicators

For cleaner output, disable thinking indicators:

```json
{
  "ShowThinking": false
}
```

### Using Environment Variables

Set API key via environment variable instead of config file:

**Linux/macOS:**
```bash
export OPENAI_API_KEY="sk-..."
dotnet run
```

**Windows:**
```powershell
$env:OPENAI_API_KEY="sk-..."
dotnet run
```

## Tips for Effective Conversations

### 1. Clear System Prompts

Good system prompts should:
- Define the persona's role and perspective
- Specify the communication style
- Set expectations for response length
- Guide the type of contributions expected

**Example:**
```
"You are a [ROLE] who focuses on [FOCUS]. You consider [CONSIDERATIONS]. Keep responses [STYLE]."
```

### 2. Complementary Personas

Design personas with complementary strengths:
- **Divergent thinking**: One persona generates ideas
- **Convergent thinking**: Another evaluates and refines
- **Synthesis**: A third brings perspectives together

### 3. Topic Formulation

Effective topics are:
- **Specific**: "Design a user authentication flow" vs. "Talk about security"
- **Actionable**: "Plan a marketing campaign" vs. "Discuss marketing"
- **Scoped**: "Choose between options A and B" vs. "Solve all our problems"

### 4. Conversation Goals

The system detects conversation completion when personas use phrases like:
- "In conclusion..."
- "To summarize..."
- "We've reached..."
- "Final decision..."
- "Let's wrap up..."

Include these naturally when the conversation should end.

## Troubleshooting

### API Key Not Found

**Error:** "OpenAI API key not found in config or environment"

**Solution:** 
- Add API key to `appsettings.json` under `LlmProvider.ApiKey`
- Or set `OPENAI_API_KEY` environment variable

### Connection Errors

**Error:** "Name or service not known (api.openai.com:443)"

**Solution:**
- Check internet connection
- Verify API endpoint is correct
- For Ollama, ensure it's running locally

### Empty Responses

**Issue:** Personas give very short or empty responses

**Solution:**
- Review system prompts for clarity
- Try a different model (e.g., GPT-4 vs GPT-3.5)
- Ensure the topic is clear and specific
- Check if MaxConversationTurns is too low

### Rate Limiting

**Issue:** API rate limit errors

**Solution:**
- Add delays between requests (modify code)
- Use a higher-tier API plan
- Switch to Ollama for unlimited local requests

## Best Practices

1. **Start Simple**: Begin with 2-3 personas before adding more
2. **Test Prompts**: Iterate on system prompts to get desired behavior
3. **Monitor Costs**: Track API usage when using commercial providers
4. **Save Conversations**: Redirect output to file for later review:
   ```bash
   dotnet run | tee conversation.txt
   ```
5. **Version Configurations**: Keep different config files for different use cases

## Example Session

Here's what a typical session looks like:

```
☕ Welcome to CoffeeTalk!
A multi-persona LLM conversation orchestrator

Provider: openai
Model: gpt-4o-mini
Personas: Alice, Bob

What would you like the personas to discuss?
Topic: Should we build a mobile app or web app first?

🎯 Topic: Should we build a mobile app or web app first?

Participants: Alice, Bob

Starting conversation...

================================================================================

💬 Alice:
  I think we should consider mobile-first since that's where users spend most of their time...

💬 Bob:
  That's a valid point, but web apps are faster to develop and deploy. Let's think about our technical capacity...

[Conversation continues...]

================================================================================

✅ Conversation goal appears to be reached!
Total turns: 6 (across 2 personas)

Thank you for using CoffeeTalk! ☕
```

## Next Steps

- Experiment with different persona combinations
- Try various model providers and compare results
- Customize the conversation detection logic
- Integrate with your workflow or tools

For more information, see the main [README.md](README.md).
