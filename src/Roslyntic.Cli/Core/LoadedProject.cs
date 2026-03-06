using Microsoft.CodeAnalysis;

internal sealed class LoadedProject
{
    public required string Name { get; init; }
    public required string ProjectFilePath { get; init; }
    public required string ProjectDirectoryPath { get; init; }
    public required string AssemblyName { get; init; }
    public required Layer? Layer { get; init; }
    public required Compilation Compilation { get; init; }
}
