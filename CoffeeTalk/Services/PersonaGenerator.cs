using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using CoffeeTalk.Models;
using System.Text.Json;
using System.Linq;

namespace CoffeeTalk.Services;

public class PersonaGenerator
{
    private readonly Kernel _kernel;

    public PersonaGenerator(Kernel kernel)
    {
        _kernel = kernel;
    }

    public async Task<List<PersonaConfig>> GenerateAsync(string topic, int requestedCount, IEnumerable<string>? reservedNames = null)
    {
        int count = Math.Clamp(requestedCount, 2, 10);
        var chat = _kernel.GetRequiredService<IChatCompletionService>();

        var history = new ChatHistory();
        history.AddSystemMessage(@"You generate a set of distinct, complementary expert personas for a collaborative discussion.
REQUIREMENTS:
- Produce JSON ONLY. No explanations.
- Return an array of objects with fields: name (string), systemPrompt (string)
- Each persona's systemPrompt must be a focused, practical system message that starts with 'You are <Name>, ...' and clearly defines their role, strengths, and collaboration style.
- Personas must bring different perspectives, skills, and approaches (e.g., product, engineering, research, design, operations, risk, customer, data, strategy).
- Avoid overlap: ensure clear differentiation across personas.
- Keep names short and unique.
- 2 to 10 personas max.
- The orchestrator and editor already exist; DO NOT include them in the output.
- Emphasize constructive collaboration, concision, and evidence-based reasoning.
");

        history.AddUserMessage($"Topic: {topic}\nGenerate {count} personas for this conversation. JSON array only, in this shape:\n[\n  {{\"name\":\"<ShortUniqueName>\",\"systemPrompt\":\"You are <ShortUniqueName>, <1-2 sentence role and perspective>. Collaborate concisely, avoid redundancy, and use tools effectively when available.\"}},\n  ...\n]");

        var response = await RetryHandler.ExecuteWithRetryAsync(
            async () => await chat.GetChatMessageContentAsync(history),
            "Generate personas");

        var text = response.Content ?? "[]";

        // Try to parse JSON array
        List<GeneratedPersona>? generated;
        try
        {
            generated = JsonSerializer.Deserialize<List<GeneratedPersona>>(text, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
        }
        catch
        {
            // Attempt to extract JSON block if extra text leaked
            var start = text.IndexOf('[');
            var end = text.LastIndexOf(']');
            if (start >= 0 && end >= start)
            {
                var slice = text.Substring(start, end - start + 1);
                generated = JsonSerializer.Deserialize<List<GeneratedPersona>>(slice, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
            }
            else
            {
                generated = null;
            }
        }

        var results = new List<PersonaConfig>();
        var usedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (reservedNames != null)
        {
            foreach (var n in reservedNames)
                usedNames.Add(n);
        }

        if (generated != null)
        {
            foreach (var p in generated)
            {
                if (string.IsNullOrWhiteSpace(p.Name) || string.IsNullOrWhiteSpace(p.SystemPrompt))
                    continue;
                var uniqueName = EnsureUniqueName(p.Name.Trim(), usedNames);
                usedNames.Add(uniqueName);
                results.Add(new PersonaConfig
                {
                    Name = uniqueName,
                    SystemPrompt = p.SystemPrompt.Trim()
                });
            }
        }

        // Enforce count by trimming or topping up via simple templates if needed
        if (results.Count > count)
        {
            results = results.Take(count).ToList();
        }
        else if (results.Count < count)
        {
            var seeds = new (string Name, string Prompt)[]
            {
                ("ProductLead", "You are ProductLead, a pragmatic product thinker who prioritizes user value, scope clarity, and incremental delivery. Collaborate concisely and drive toward decisions."),
                ("EngArchitect", "You are EngArchitect, a systems-minded engineer who evaluates feasibility, risks, and performance. Optimize for simplicity and maintainability. Be concise."),
                ("DataAnalyst", "You are DataAnalyst, evidence-oriented, quantifying trade-offs and validating assumptions. Keep outputs short and actionable."),
                ("UXDesigner", "You are UXDesigner, focused on clarity, usability, and accessible design. Advocate for users while remaining concise."),
                ("OpsReliability", "You are OpsReliability, ensuring operability, reliability, and cost-aware scaling. Keep guidance brief and specific."),
                ("SecurityRisk", "You are SecurityRisk, identifying threats, compliance, and safe patterns. Communicate succinctly with mitigation steps."),
                ("CustomerAdvocate", "You are CustomerAdvocate, surfacing real-world needs and friction. Keep points brief and grounded in user outcomes."),
                ("Researcher", "You are Researcher, exploring alternatives and unknowns, framing crisp experiments. Avoid verbosity."),
                ("Strategist", "You are Strategist, aligning goals, scope, and success metrics. Push for clear decisions with minimal words."),
            };

            int i = 0;
            while (results.Count < count && i < seeds.Length)
            {
                var name = EnsureUniqueName(seeds[i].Name, usedNames);
                usedNames.Add(name);
                results.Add(new PersonaConfig { Name = name, SystemPrompt = seeds[i].Prompt });
                i++;
            }
        }

        // Final clamp to 2..10
        if (results.Count < 2)
        {
            // Ensure at least 2
            while (results.Count < 2)
            {
                var fillerName = EnsureUniqueName($"Contributor{results.Count+1}", usedNames);
                usedNames.Add(fillerName);
                results.Add(new PersonaConfig
                {
                    Name = fillerName,
                    SystemPrompt = $"You are {fillerName}, a concise collaborator adding a distinct viewpoint. Avoid redundancy and focus on decisions."
                });
            }
        }
        else if (results.Count > 10)
        {
            results = results.Take(10).ToList();
        }

        return results;
    }

    private static string EnsureUniqueName(string name, HashSet<string> used)
    {
        var baseName = new string(name.Where(ch => char.IsLetterOrDigit(ch)).ToArray());
        if (string.IsNullOrWhiteSpace(baseName)) baseName = "Persona";
        var candidate = baseName;
        int i = 2;
        while (used.Contains(candidate))
        {
            candidate = baseName + i.ToString();
            i++;
        }
        return candidate;
    }

    private class GeneratedPersona
    {
        public string? Name { get; set; }
        public string? SystemPrompt { get; set; }
    }
}
