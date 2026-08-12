namespace CoffeeTalk.Core.Interfaces;

public interface IPdfDocumentExporter
{
    Task ExportAsync(string markdown, string outputPath, CancellationToken cancellationToken = default);
}
