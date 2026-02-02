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
            _content.Insert(insertIndex, insertion);
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
            var replacement = "\n" + content.Trim() + "\n\n"; // ensure surrounding blank lines

            _content.Remove(sectionStart, sectionEnd - sectionStart);
            _content.Insert(sectionStart, replacement);
        }
    }

    public string ListHeadings()
    {
        lock (_lock)
        {
            var result = new StringBuilder();

            bool isStartOfLine = true;
            bool checkingHashes = false;
            int hashCount = 0;
            bool isHeading = false;
            int currentHeadingStartIndex = -1;

            foreach (var chunk in _content.GetChunks())
            {
                var span = chunk.Span;
                for (int i = 0; i < span.Length; i++)
                {
                    char c = span[i];

                    if (isStartOfLine)
                    {
                        if (c == '#')
                        {
                            checkingHashes = true;
                            hashCount = 1;
                            isStartOfLine = false;
                        }
                        else if (c == '\n')
                        {
                            isStartOfLine = true;
                        }
                        else
                        {
                            isStartOfLine = false;
                        }
                    }
                    else if (checkingHashes)
                    {
                        if (c == '#')
                        {
                            hashCount++;
                            if (hashCount > 6)
                            {
                                checkingHashes = false;
                            }
                        }
                        else if (c == ' ')
                        {
                            if (hashCount >= 1 && hashCount <= 6)
                            {
                                isHeading = true;
                                if (result.Length > 0) result.Append('\n');

                                result.Append('#', hashCount);
                                result.Append(' ');
                                currentHeadingStartIndex = result.Length;
                            }
                            checkingHashes = false;
                        }
                        else
                        {
                            checkingHashes = false;
                            if (c == '\n') isStartOfLine = true;
                        }
                    }
                    else if (isHeading)
                    {
                        if (c == '\n')
                        {
                            // End of heading line. Trim trailing whitespace.
                            while (result.Length > currentHeadingStartIndex && char.IsWhiteSpace(result[result.Length - 1]))
                            {
                                result.Length--;
                            }

                            isHeading = false;
                            isStartOfLine = true;
                        }
                        else if (c != '\r')
                        {
                            result.Append(c);
                        }
                    }
                    else
                    {
                        // Just scanning for newline
                        if (c == '\n')
                        {
                            isStartOfLine = true;
                        }
                    }
                }
            }

            if (isHeading)
            {
                while (result.Length > currentHeadingStartIndex && char.IsWhiteSpace(result[result.Length - 1]))
                {
                    result.Length--;
                }
            }

            return result.ToString();
        }
    }

    public async Task<string> SaveToFileAsync(string path)
    {
        string contentToWrite;
        lock (_lock)
        {
            contentToWrite = _content.ToString();
        }

        var fullPath = Path.GetFullPath(string.IsNullOrWhiteSpace(path) ? "conversation.md" : path);
        // Ensure directory exists
        var dir = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrEmpty(dir))
        {
            Directory.CreateDirectory(dir);
        }
        await File.WriteAllTextAsync(fullPath, contentToWrite);
        return fullPath;
    }
}
