using RagnaForge.Application.Abstractions;
using RagnaForge.Domain.Discovery;

namespace RagnaForge.Infrastructure.GrfEditorIntegration;

public sealed class GrfEditorProbe : IGrfEditorProbe
{
    public GrfEditorDiscoveryResult Probe(string grfEditorPath)
    {
        if (!Directory.Exists(grfEditorPath))
        {
            return new GrfEditorDiscoveryResult(
                grfEditorPath,
                false,
                [],
                [],
                [],
                ["GRF Editor path was not found."]);
        }

        var executables = new[]
        {
            "GRF Editor.exe",
            "GrfCL.exe"
        }.Select(name => SnapshotExecutable(Path.Combine(grfEditorPath, name))).ToArray();

        var configs = Directory.EnumerateFiles(grfEditorPath, "*.config", SearchOption.TopDirectoryOnly)
            .OrderBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var capabilities = new[]
        {
            "Containers: grf, gpf, thor, rgz (inferred from binary metadata and local resources)",
            "Formats: spr, act, pal, rsw, gnd, gat, rsm, rsm2, str, tga, lua, lub (inferred)",
            "Preview/render stack: WPF plus OpenTK/OpenGL (inferred)",
            "CLI adapter candidate: GrfCL.exe",
            "Direct assembly adapter candidate: embedded GRF.dll via GrfCL.exe"
        };

        var warnings = new List<string>();
        if (executables.Any(executable => !executable.Exists))
        {
            warnings.Add("One or more expected GRF Editor executables were not found.");
        }

        warnings.Add("Installed package does not expose loose DLLs; embedded-resource loading must stay isolated in GrfEditorIntegration.");

        return new GrfEditorDiscoveryResult(
            grfEditorPath,
            true,
            executables,
            configs,
            capabilities,
            warnings);
    }

    private static GrfEditorExecutable SnapshotExecutable(string path)
    {
        if (!File.Exists(path))
        {
            return new GrfEditorExecutable(
                Path.GetFileName(path),
                path,
                false,
                0,
                null,
                null,
                null);
        }

        var file = new FileInfo(path);
        var version = System.Diagnostics.FileVersionInfo.GetVersionInfo(path);

        return new GrfEditorExecutable(
            file.Name,
            file.FullName,
            true,
            file.Length,
            version.ProductName,
            version.ProductVersion,
            version.FileVersion);
    }
}
