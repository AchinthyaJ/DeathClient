using System.Text.Json.Serialization;

namespace OfflineMinecraftLauncher;

internal sealed class CurseForgeSearchResponse
{
    [JsonPropertyName("data")]
    public List<CurseForgeMod> Data { get; set; } = [];
}

internal sealed class CurseForgeFilesResponse
{
    [JsonPropertyName("data")]
    public List<CurseForgeFile> Data { get; set; } = [];
}

internal sealed class CurseForgeMod
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("summary")]
    public string Summary { get; set; } = string.Empty;

    [JsonPropertyName("authors")]
    public List<CurseForgeAuthor> Authors { get; set; } = [];

    [JsonPropertyName("logo")]
    public CurseForgeLogo? Logo { get; set; }

    [JsonPropertyName("downloadCount")]
    public decimal DownloadCount { get; set; }

    [JsonPropertyName("latestFiles")]
    public List<CurseForgeFile> LatestFiles { get; set; } = [];

    public ModrinthProject ToModrinthProject()
    {
        return new ModrinthProject
        {
            ProjectId = Id.ToString(),
            Slug = Name.ToLowerInvariant().Replace(" ", "-"),
            Title = Name,
            Description = Summary,
            ProjectType = "mod", // Default to mod, but can be pack
            Author = Authors.FirstOrDefault()?.Name ?? "Unknown",
            IconUrl = Logo?.ThumbnailUrl ?? Logo?.Url,
            Downloads = (int)DownloadCount,
            IsCurseForge = true
        };
    }
}

internal sealed class CurseForgeAuthor
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;
}

internal sealed class CurseForgeLogo
{
    [JsonPropertyName("url")]
    public string Url { get; set; } = string.Empty;

    [JsonPropertyName("thumbnailUrl")]
    public string ThumbnailUrl { get; set; } = string.Empty;
}

internal sealed class CurseForgeFile
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("displayName")]
    public string DisplayName { get; set; } = string.Empty;

    [JsonPropertyName("fileName")]
    public string FileName { get; set; } = string.Empty;

    [JsonPropertyName("downloadUrl")]
    public string? DownloadUrl { get; set; }

    [JsonPropertyName("fileLength")]
    public long FileLength { get; set; }

    [JsonPropertyName("gameVersions")]
    public List<string> GameVersions { get; set; } = [];

    [JsonPropertyName("hashes")]
    public List<CurseForgeHash> Hashes { get; set; } = [];
}

internal sealed class CurseForgeHash
{
    [JsonPropertyName("value")]
    public string Value { get; set; } = string.Empty;

    [JsonPropertyName("algo")]
    public int Algo { get; set; } // 1 for Sha1, 2 for Md5
}
