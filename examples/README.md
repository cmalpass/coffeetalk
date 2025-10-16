# CoffeeTalk Example Configurations

This directory contains example configuration files for different use cases.

## Available Examples

### 1. Product Team (`product-team.json`)
Simulates a product development team with complementary roles:
- **ProductManager**: Focuses on user value and business outcomes
- **Engineer**: Considers technical implementation and feasibility
- **Designer**: Emphasizes user experience and interface design

**Good topics:**
- Feature prioritization discussions
- User onboarding design
- Technical architecture decisions
- Product roadmap planning

### 2. Philosophy (`philosophy.json`)
Creates a philosophical dialogue between different thinking styles:
- **Socrates**: Uses questioning to explore ideas
- **Aristotle**: Provides logical and systematic analysis
- **Moderator**: Synthesizes viewpoints toward conclusions

**Good topics:**
- Ethical dilemmas
- Conceptual questions (nature of knowledge, justice, etc.)
- Critical thinking exercises
- Thought experiments

### 3. Creative Writing (`creative-writing.json`)
Establishes a creative writing workshop environment:
- **Storyteller**: Generates narrative ideas and plot elements
- **Editor**: Provides constructive feedback on craft
- **Reader**: Shares audience perspective and reactions

**Good topics:**
- Story development
- Character creation
- Plot brainstorming
- Narrative problem-solving

### 4. Brainstorm (`brainstorm.json`)
Sets up an ideation and problem-solving team:
- **Ideator**: Generates diverse ideas without judgment
- **Analyst**: Evaluates feasibility and identifies risks
- **Synthesizer**: Combines elements into actionable solutions

**Good topics:**
- Innovation challenges
- Problem-solving sessions
- Strategic planning
- Creative exploration

## How to Use

1. **Copy to appsettings.json:**
   ```bash
   cp examples/product-team.json CoffeeTalk/appsettings.json
   ```

2. **Add your API key:**
   Edit the copied file and replace `"your-api-key-here"` with your actual API key.

3. **Run the application:**
   ```bash
   cd CoffeeTalk
   dotnet run
   ```

## Customization Tips

### Adjusting Conversation Length
Modify `MaxConversationTurns` based on your needs:
- Quick discussions: 5-8 turns
- Standard: 10-15 turns
- Deep explorations: 20+ turns

### Creating Your Own Personas
When designing personas, consider:

1. **Role Definition**: What perspective does this persona represent?
2. **Focus Areas**: What aspects do they care most about?
3. **Communication Style**: How do they express ideas?
4. **Response Length**: Keep prompts requesting concise responses

**Template:**
```json
{
  "Name": "PersonaName",
  "SystemPrompt": "You are a [role] who focuses on [focus]. You consider [considerations]. [Behavioral guidance]. Keep your responses [style]."
}
```

### Mixing Personas
Effective persona combinations often include:
- **Divergent thinker**: Generates possibilities
- **Convergent thinker**: Evaluates and refines
- **Integrator**: Synthesizes and concludes

## Using with Ollama

To use these examples with Ollama (local LLM):

1. **Modify the LlmProvider section:**
   ```json
   {
     "LlmProvider": {
       "Type": "ollama",
       "Endpoint": "http://localhost:11434/v1",
       "ModelId": "llama2"
     }
   }
   ```

2. **Ensure Ollama is running:**
   ```bash
   ollama serve
   ```

3. **Pull a model if needed:**
   ```bash
   ollama pull llama2
   ```

## Contributing Examples

Have a great persona configuration? Consider contributing it:
1. Create a descriptive JSON file
2. Test it with various topics
3. Submit a pull request with your example

## Further Reading

- [Main README](../README.md) - Project overview and setup
- [Usage Guide](../USAGE.md) - Detailed usage instructions and tips
