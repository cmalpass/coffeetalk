using CoffeeTalk.Models;
using CoffeeTalk.Services;
using Microsoft.Agents.AI;

namespace CoffeeTalk.Tests;

public sealed class AgentPersonaGeneratorTests
{
    [Fact]
    public void BuildSystemPrompt_RequiresJsonAndExcludesBuiltInAgents()
    {
        var prompt = AgentPersonaGenerator.BuildSystemPrompt();

        Assert.Contains("JSON ONLY", prompt);
        Assert.Contains("DO NOT include them", prompt);
        Assert.Contains("2 to 10 personas", prompt);
    }

    [Fact]
    public async Task GenerateAsync_ClampsCountAndUsesUniqueFallbackNames()
    {
        var generator = new AgentPersonaGenerator(new TestAIAgent("not json"));

        var personas = await generator.GenerateAsync("topic", 1, new[] { "ProductLead" });

        Assert.Equal(2, personas.Count);
        Assert.Equal(personas.Count, personas.Select(persona => persona.Name).Distinct(StringComparer.OrdinalIgnoreCase).Count());
        Assert.DoesNotContain(personas, persona => persona.Name == "ProductLead");
    }

    [Fact]
    public async Task GenerateAsync_ParsesJsonAndDeduplicatesNames()
    {
        var generator = new AgentPersonaGenerator(new TestAIAgent(
            """[{"name":"Researcher!","systemPrompt":"You are Researcher, an evidence-focused analyst."},{"name":"researcher","systemPrompt":"You are Researcher, a second perspective."}]"""));

        var personas = await generator.GenerateAsync("topic", 2);

        Assert.Equal(new[] { "Researcher", "researcher2" }, personas.Select(persona => persona.Name));
    }
}
