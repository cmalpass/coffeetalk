# Using the Orchestrator

The orchestrator feature enables AI-directed conversation flow where an intelligent agent selects which persona should speak next based on the conversation needs.

## Quick Start

### 1. Enable Orchestrator in Configuration

Add or modify the `Orchestrator` section in your `appsettings.json`:

```json
{
  "Orchestrator": {
    "Enabled": true
  }
}
```

### 2. Run Your Conversation

```bash
dotnet run
```

You'll see the mode indicator:
```
Mode: 🎭 Orchestrated (AI-directed conversation flow)
```

### 3. Observe Orchestrated Behavior

The orchestrator will:
- Analyze the conversation state and document progress
- Select the most appropriate persona for each turn
- Show reasoning: `[Orchestrator: Need initial structure and outline]`
- Balance participation across all personas
- Guide toward completion

## Configuration Options

### Basic Configuration

```json
{
  "Orchestrator": {
    "Enabled": true
  }
}
```

Uses the default base system prompt. **The persona descriptions are automatically generated from your loaded persona configurations**, so the orchestrator always has accurate, up-to-date information about available personas.

### Custom Base System Prompt

```json
{
  "Orchestrator": {
    "Enabled": true,
    "BaseSystemPrompt": "Your custom orchestrator instructions here..."
  }
}
```

The `BaseSystemPrompt` is the core instructions for the orchestrator. Persona descriptions will be automatically appended based on the loaded persona configurations.

## How It Works

### Initialization

When the orchestrator starts:
1. **Loads base system prompt** - Either default or custom from configuration
2. **Extracts persona descriptions** - Automatically parses each persona's `SystemPrompt` to identify key characteristics
3. **Builds full system prompt** - Combines base instructions with dynamically generated persona list
4. **Example output**: "Zephyr: a strategic thinker who excels at structuring ideas and creating clear frameworks"

### Selection Process

The orchestrator receives:
1. **Current topic/message** - What's being discussed
2. **Recent conversation history** - Last 5 exchanges
3. **Document state** - Current headings and structure
4. **Participation stats** - How many times each persona has spoken
5. **Available personas** - List of personas (dynamically loaded)
6. **Urgency indicators** - Turns remaining

Based on this context, it selects the most appropriate speaker.

### Example Selection Flow

**Turn 1:**
```
Orchestrator selects: Zephyr
Reason: Need initial structure and outline
```

**Turn 2:**
```
Orchestrator selects: Regulus Thorne
Reason: Technical details needed for Introduction section
```

**Turn 3:**
```
Orchestrator selects: Seraphina Bloom
Reason: Creative perspectives for alternative approaches
```

**Turn 4:**
```
Orchestrator selects: Kaito 'Kai' Ishikawa
Reason: Time to synthesize and reach conclusion
```

### Comparison: Round-Robin vs Orchestrated

**Round-Robin (Default)**
- Fixed speaking order: A → B → C → D → A → B → C → D
- Every persona speaks every round
- Predictable but potentially redundant
- 4 personas × 10 turns = 40 total contributions

**Orchestrated**
- Dynamic speaking order: A → A → C → B → D → C → A
- Personas speak when expertise needed
- Adaptive to conversation needs
- More efficient token usage
- Can end early when goals achieved

## Best Practices

### 1. Define Clear Persona Roles

Make each persona's expertise distinct in their `SystemPrompt`:

```json
{
  "Name": "Architect",
  "SystemPrompt": "You excel at high-level structure and frameworks..."
},
{
  "Name": "Engineer", 
  "SystemPrompt": "You focus on technical implementation details..."
},
{
  "Name": "Designer",
  "SystemPrompt": "You prioritize user experience and creativity..."
}
```

### 2. Adjust Max Turns

With orchestration, you may need fewer total turns:

```json
{
  "MaxConversationTurns": 12
}
```

This allows up to 12 × number_of_personas individual contributions, but the conversation may end earlier.

### 3. Monitor Token Usage

Orchestrated mode adds one LLM call per turn (for selection), but often results in:
- Fewer total turns needed
- More focused contributions
- Better overall token efficiency

### 4. Customize for Your Use Case

Modify the `SystemPrompt` to emphasize what matters for your scenario:

**For Balanced Discussion:**
```
"Ensure all personas contribute roughly equally..."
```

**For Expert-Driven:**
```
"Prioritize the most relevant expert for each topic, even if it means some personas speak more..."
```

**For Conclusion-Focused:**
```
"Aggressively move toward conclusion and avoid prolonged debates..."
```

## Troubleshooting

### Orchestrator Selects Same Persona Repeatedly

The orchestrator tracks participation and should balance speakers. If this happens:
- Check that persona roles are sufficiently distinct
- Verify the topic requires multiple perspectives
- Consider adjusting the orchestrator's system prompt

### No Persona Selected

If you see: `⚠️ Orchestrator couldn't select a speaker`

This means the orchestrator's response didn't contain a valid persona name.
- Check that persona names in configuration match exactly
- Review orchestrator system prompt for clarity
- Verify the LLM is following instructions

### Conversation Ends Too Early/Late

Adjust completion detection:
- The orchestrator's `ShouldConclude()` method looks for keywords
- Modify `IsConversationComplete()` in `ConversationOrchestrator.cs`
- Tune `MaxConversationTurns` for your use case

## Example Configurations

See the `appsettings.orchestrated.json` file for a complete working example with:
- 4 distinct personas
- Orchestrator enabled with custom prompt
- Rate limiting configured
- Tool verification enabled

## Disabling Orchestrator

To return to round-robin mode:

```json
{
  "Orchestrator": {
    "Enabled": false
  }
}
```

Or simply omit the `Orchestrator` section entirely.
