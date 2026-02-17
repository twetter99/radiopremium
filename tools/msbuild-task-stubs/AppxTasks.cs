using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Microsoft.Build.AppxPackage;

public sealed class RemovePayloadDuplicates : Microsoft.Build.Utilities.Task
{
    public ITaskItem[] Inputs { get; set; } = [];
    [Output] public ITaskItem[] Filtered { get; set; } = [];
    public string? ProjectName { get; set; }
    public string? Platform { get; set; }
    public string? VsTelemetrySession { get; set; }

    public override bool Execute()
    {
        Filtered = Inputs ?? [];
        return true;
    }
}

public sealed class ExpandPayloadDirectories : Microsoft.Build.Utilities.Task
{
    public ITaskItem[] Inputs { get; set; } = [];
    public ITaskItem[] TargetDirsToExclude { get; set; } = [];
    public ITaskItem[] TargetFilesToExclude { get; set; } = [];
    public string? VsTelemetrySession { get; set; }
    [Output] public ITaskItem[] Expanded { get; set; } = [];

    public override bool Execute()
    {
        Expanded = Inputs ?? [];
        return true;
    }
}

public sealed class GetPackageArchitecture : Microsoft.Build.Utilities.Task
{
    public string? Platform { get; set; }
    public ITaskItem[] ProjectArchitecture { get; set; } = [];
    public ITaskItem[] RecursiveProjectArchitecture { get; set; } = [];
    public string? VsTelemetrySession { get; set; }
    [Output] public string PackageArchitecture { get; set; } = "x64";

    public override bool Execute()
    {
        if (!string.IsNullOrWhiteSpace(Platform))
        {
            PackageArchitecture = Platform.ToLowerInvariant() switch
            {
                "x86" => "x86",
                "arm64" => "arm64",
                _ => "x64"
            };
        }

        return true;
    }
}

public sealed class GetDefaultResourceLanguage : Microsoft.Build.Utilities.Task
{
    public string? DefaultLanguage { get; set; }
    public ITaskItem[] SourceAppxManifest { get; set; } = [];
    public string? VsTelemetrySession { get; set; }
    [Output] public string DefaultResourceLanguage { get; set; } = "en-US";

    public override bool Execute()
    {
        if (!string.IsNullOrWhiteSpace(DefaultLanguage))
        {
            DefaultResourceLanguage = DefaultLanguage;
        }

        return true;
    }
}

public sealed class GetSdkFileFullPath : Microsoft.Build.Utilities.Task
{
    public string? FileName { get; set; }
    public string? FullFilePath { get; set; }
    public string? FileArchitecture { get; set; }
    public bool RequireExeExtension { get; set; }
    public string? TargetPlatformSdkRootOverride { get; set; }
    public string? SDKIdentifier { get; set; }
    public string? SDKVersion { get; set; }
    public string? TargetPlatformIdentifier { get; set; }
    public string? TargetPlatformMinVersion { get; set; }
    public string? TargetPlatformVersion { get; set; }
    public bool MSBuildExtensionsPath64Exists { get; set; }
    public string? VsTelemetrySession { get; set; }

    [Output] public string ActualFullFilePath { get; set; } = string.Empty;
    [Output] public string ActualFileArchitecture { get; set; } = "x64";

    public override bool Execute()
    {
        ActualFullFilePath = FullFilePath ?? string.Empty;
        ActualFileArchitecture = string.IsNullOrWhiteSpace(FileArchitecture) ? "x64" : FileArchitecture!;
        return true;
    }
}

public sealed class GetSdkPropertyValue : Microsoft.Build.Utilities.Task
{
    public string? TargetPlatformSdkRootOverride { get; set; }
    public string? SDKIdentifier { get; set; }
    public string? SDKVersion { get; set; }
    public string? TargetPlatformIdentifier { get; set; }
    public string? TargetPlatformMinVersion { get; set; }
    public string? TargetPlatformVersion { get; set; }
    public string? PropertyName { get; set; }
    public string? VsTelemetrySession { get; set; }

    [Output] public string PropertyValue { get; set; } = string.Empty;

    public override bool Execute() => true;
}

public sealed class ValidateConfiguration : Microsoft.Build.Utilities.Task
{
    public string? TargetPlatformMinVersion { get; set; }
    public string? TargetPlatformVersion { get; set; }
    public string? ProjectLanguage { get; set; }
    public string? VsTelemetrySession { get; set; }
    public string? TargetPlatformIdentifier { get; set; }
    public string? Platform { get; set; }

    public override bool Execute() => true;
}
