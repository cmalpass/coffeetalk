# CoffeeTalk Quick Start Guide

Get CoffeeTalk up and running in 5 minutes!

## Step 1: Prerequisites

Ensure you have:
- ✅ .NET 8.0 SDK or later ([Download](https://dotnet.microsoft.com/download))
- ✅ OpenAI API key OR Ollama running locally

Check your .NET version:
```bash
dotnet --version
```

## Step 2: Clone and Navigate

```bash
git clone https://github.com/cmalpass/coffeetalk.git
cd coffeetalk/CoffeeTalk
```

## Step 3: Configure

### Option A: Using OpenAI

Set your API key as an environment variable:

**Linux/macOS:**
```bash
export OPENAI_API_KEY="sk-your-key-here"
```

**Windows PowerShell:**
```powershell
$env:OPENAI_API_KEY="sk-your-key-here"
```

**Or** edit `appsettings.json` and add your key:
```json
{
  "LlmProvider": {
    "ApiKey": "sk-your-key-here"
  }
}
```

### Option B: Using Ollama

1. Install Ollama from https://ollama.ai
2. Start Ollama: `ollama serve`
3. Pull a model: `ollama pull llama2`
4. Copy the Ollama config:
   ```bash
   cp appsettings.ollama.json appsettings.json
   ```

## Step 4: Run!

```bash
dotnet run
```

When prompted, enter a topic:
```
What would you like the personas to discuss?
Topic: How can we improve team collaboration?
```

## Step 5: Watch the Magic

The personas will engage in conversation:

```
☕ Welcome to CoffeeTalk!
A multi-persona LLM conversation orchestrator

Provider: openai
Model: gpt-4o-mini
Personas: Alice, Bob

🎯 Topic: How can we improve team collaboration?

Participants: Alice, Bob

Starting conversation...

================================================================================

💬 Alice:
  I think we should focus on creating more opportunities for...

💬 Bob:
  That's a good point, but we also need to consider...

[Conversation continues...]

================================================================================

✅ Conversation goal appears to be reached!
```

## Next Steps

### Try Different Personas

Copy one of the example configurations:
```bash
cp ../examples/product-team.json appsettings.json
# Edit to add your API key
```

Available examples:
- `product-team.json` - PM, Engineer, Designer
- `philosophy.json` - Socrates, Aristotle, Moderator
- `creative-writing.json` - Storyteller, Editor, Reader
- `brainstorm.json` - Ideator, Analyst, Synthesizer

### Customize Your Personas

Edit `appsettings.json` to create your own personas:

```json
{
  "Personas": [
    {
      "Name": "YourPersona",
      "SystemPrompt": "You are... [define their role and style]"
    }
  ]
}
```

### Interesting Topics to Try

**Product Development:**
- "Should we add dark mode to our app?"
- "How to prioritize features for Q1?"
- "Design a better onboarding flow"

**Creative:**
- "Write a story about AI discovering emotions"
- "Create a mystery plot with a twist ending"
- "Develop a compelling villain character"

**Problem Solving:**
- "How to reduce customer churn?"
- "Improve remote team communication"
- "Make meetings more effective"

**Philosophy:**
- "What defines personal identity?"
- "Is free will compatible with determinism?"
- "The relationship between knowledge and belief"

## Troubleshooting

### "OpenAI API key not found"
→ Set `OPENAI_API_KEY` environment variable or add to `appsettings.json`

### "Name or service not known"
→ Check internet connection or verify Ollama is running for local use

### Build errors
→ Ensure .NET 8.0 SDK is installed: `dotnet --version`

### Empty or strange responses
→ Try a different model (e.g., gpt-4 instead of gpt-3.5-turbo)

## Learn More

- 📖 [Full Documentation](README.md)
- 📚 [Detailed Usage Guide](USAGE.md)
- 💡 [Example Configurations](examples/README.md)
- 🤝 [Contributing](CONTRIBUTING.md)

## Support

Questions? Issues? 
- Check [USAGE.md](USAGE.md) for detailed help
- Open an issue on GitHub
- Read through existing issues for solutions

---

**Enjoy your coffee and conversations! ☕**
