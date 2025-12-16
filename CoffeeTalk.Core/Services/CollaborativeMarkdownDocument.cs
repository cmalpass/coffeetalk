using System.Text;
using System.Text.RegularExpressions;

namespace CoffeeTalk.Services;

public class CollaborativeMarkdownDocument
{
    private readonly StringBuilder _content = new();
    private readonly object _lock = new();

    public CollaborativeMarkdownDocument()
    {
        // Start empty
    }

    public string GetContent()
    {
        lock (_lock)
        {
            return _content.ToString();
        }
    }

    public string Snapshot()
    {
        lock (_lock)
        {
            return _content.ToString();
        }
    }

    public void Restore(string snapshot)
    {
        lock (_lock)
        {
            _content.Clear();
            _content.Append(snapshot ?? string.Empty);
        }
    }

    public void SetTitle(string title)
    {
        lock (_lock)
        {
            var current = _content.ToString();
            if (current.StartsWith("# "))
            {
                // Replace first line
                var idx = current.IndexOf('\n');
                _content.Clear();
                _content.Append("# ").Append(title).AppendLine();
                if (idx >= 0 && idx + 1 < current.Length)
                {
                    _content.Append(current.AsSpan(idx + 1));
                }
            }
            else
            {
                _content.Insert(0, "# " + title + "\n\n");
            }
        }
    }

    public void AddHeading(string text, int level = 2)
    {
        if (level < 1) level = 1;
        if (level > 6) level = 6;
        lock (_lock)
        {
            _content.AppendLine(new string('#', level) + " " + text);
            _content.AppendLine();
        }
    }

    public void AppendParagraph(string text)
    {
        lock (_lock)
        {
            _content.AppendLine(text.Trim());
            _content.AppendLine();
        }
    }

    public void InsertAfterHeading(string headingText, string content)
    {
        lock (_lock)
        {
            var doc = _content.ToString();
            var pattern = @$"^#+\s+{Regex.Escape(headingText)}\s*$";
            var regex = new Regex(pattern, RegexOptions.Multiline | RegexOptions.CultureInvariant);
            var match = regex.Match(doc);
            if (!match.Success)
            {
                // If heading not found, append as a new section
                AddHeading(headingText, 2);
                AppendParagraph(content);
                return;
            }

            // Find insertion point: after the heading line
            var insertIndex = match.Index + match.Length;
            // Insert a blank line and the content after the heading
            var insertion = "\n" + content.Trim() + "\n\n";
            _content.Clear();
            _content.Append(doc.Substring(0, insertIndex));
            _content.Append(insertion);
            _content.Append(doc.Substring(insertIndex));
        }
    }

    // Replace the entire content under a heading until the next heading, or append if not exists
    public void ReplaceSection(string headingText, string content)
    {
        lock (_lock)
        {
            var doc = _content.ToString();
            var headingPattern = @$"^(?<hashes>#+)\s+{Regex.Escape(headingText)}\s*$";
            var headingRegex = new Regex(headingPattern, RegexOptions.Multiline | RegexOptions.CultureInvariant);
            var match = headingRegex.Match(doc);

            if (!match.Success)
            {
                // Create the section if it doesn't exist
                AddHeading(headingText, 2);
                AppendParagraph(content);
                return;
            }

            // Find the start of the section content (end of heading line)
            int sectionStart = match.Index + match.Length;

            // Find the next heading after this one to determine section end
            var nextHeadingRegex = new Regex(@"^#{1,6}\s+.+$", RegexOptions.Multiline | RegexOptions.CultureInvariant);
            var nextHeadingMatch = nextHeadingRegex.Match(doc, sectionStart);
            int sectionEnd = nextHeadingMatch.Success ? nextHeadingMatch.Index : doc.Length;

            // Build new document with replaced section
            var before = doc.Substring(0, sectionStart);
            var after = doc.Substring(sectionEnd);
            var replacement = "\n" + content.Trim() + "\n\n"; // ensure surrounding blank lines

            _content.Clear();
            _content.Append(before);
            _content.Append(replacement);
            _content.Append(after);
        }
    }

    public string ListHeadings()
    {
        lock (_lock)
        {
            var lines = _content.ToString().Split('\n');
            var headings = new List<string>();
            foreach (var line in lines)
            {
                if (Regex.IsMatch(line, "^#{1,6} "))
                {
                    headings.Add(line.Trim());
                }
            }
            return string.Join("\n", headings);
        }
    }

    public string SaveToFile(string path)
    {
        lock (_lock)
        {
            var fullPath = Path.GetFullPath(string.IsNullOrWhiteSpace(path) ? "conversation.md" : path);
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
            File.WriteAllText(fullPath, _content.ToString());
            return fullPath;
        }
    }
}
