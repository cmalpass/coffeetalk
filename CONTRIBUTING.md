# Contributing to CoffeeTalk

Thank you for your interest in contributing to CoffeeTalk! This document provides guidelines and information for contributors.

## How to Contribute

### Reporting Issues

If you find a bug or have a suggestion:

1. Check if the issue already exists in the GitHub Issues
2. If not, create a new issue with:
   - Clear title and description
   - Steps to reproduce (for bugs)
   - Expected vs actual behavior
   - Your environment (OS, .NET version, LLM provider)

### Submitting Changes

1. **Fork the repository**
2. **Create a feature branch**
   ```bash
   git checkout -b feature/your-feature-name
   ```
3. **Make your changes**
4. **Test your changes**
   ```bash
   cd CoffeeTalk
   dotnet build
   dotnet run
   ```
5. **Commit your changes**
   ```bash
   git commit -m "Add feature: description"
   ```
6. **Push to your fork**
   ```bash
   git push origin feature/your-feature-name
   ```
7. **Create a Pull Request**

## Development Guidelines

### Code Style

- Follow standard C# conventions
- Use meaningful variable and method names
- Keep methods focused and concise
- Add comments for complex logic

### Project Structure

```
CoffeeTalk/
├── Models/          # Configuration and data models
├── Services/        # Business logic and orchestration
├── Program.cs       # Application entry point
└── appsettings.json # Configuration file
```

### Adding New Features

When adding features, consider:

1. **Minimal Changes**: Keep changes focused and minimal
2. **Configuration**: Make features configurable when possible
3. **Documentation**: Update relevant docs (README, USAGE)
4. **Examples**: Add examples if introducing new capabilities

### Testing

Currently, the project focuses on integration testing:

1. Build the project without errors
2. Run with different configurations
3. Test with both OpenAI and Ollama providers
4. Verify error handling

## Contribution Ideas

### Easy Contributions

- Fix typos in documentation
- Add new example configurations
- Improve error messages
- Add console output formatting

### Medium Contributions

- Add conversation export functionality
- Implement conversation history persistence
- Add support for additional LLM providers
- Improve conversation completion detection

### Advanced Contributions

- Add unit tests
- Implement plugin system for custom behaviors
- Add streaming response support
- Create GUI wrapper

## Example Contributions

### Adding a New Example Configuration

1. Create a new JSON file in `examples/`
2. Define personas with clear system prompts
3. Add documentation to `examples/README.md`
4. Test with various topics

Example:
```json
{
  "LlmProvider": { ... },
  "Personas": [
    {
      "Name": "Expert",
      "SystemPrompt": "Clear role definition..."
    }
  ]
}
```

### Improving Conversation Detection

The `IsConversationComplete` method in `ConversationOrchestrator.cs` can be enhanced:

```csharp
private bool IsConversationComplete(string response, int turn)
{
    // Add your improved logic here
}
```

### Adding New LLM Providers

In `Services/KernelBuilder.cs`, add a new case:

```csharp
case "newprovider":
    builder.AddOpenAIChatCompletion(
        modelId: config.ModelId,
        endpoint: new Uri(config.Endpoint),
        apiKey: config.ApiKey);
    break;
```

## Documentation

When updating documentation:

- Keep it clear and concise
- Include examples where helpful
- Update all relevant files (README, USAGE, etc.)
- Test any code examples you include

## Code of Conduct

- Be respectful and constructive
- Welcome newcomers and help them get started
- Focus on what's best for the project
- Assume good intentions

## Questions?

If you have questions about contributing:

1. Check existing documentation
2. Look at closed issues/PRs for similar questions
3. Open a new issue with the "question" label

## License

By contributing, you agree that your contributions will be licensed under the MIT License.

Thank you for contributing to CoffeeTalk! ☕
