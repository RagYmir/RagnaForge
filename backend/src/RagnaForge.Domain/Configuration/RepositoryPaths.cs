namespace RagnaForge.Domain.Configuration;

public sealed record RepositoryPaths(
    string RathenaPath,
    string PatchPath,
    string GrfRepositoryPath,
    string GrfEditorPath)
{
    public IEnumerable<(string Name, string Path)> Enumerate()
    {
        yield return ("rAthena", RathenaPath);
        yield return ("Patch", PatchPath);
        yield return ("GRF repository", GrfRepositoryPath);
        yield return ("GRF Editor", GrfEditorPath);
    }
}

