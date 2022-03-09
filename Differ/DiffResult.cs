namespace Differ
{
    public readonly record struct DiffResult(string Path, DiffStatus Status);

    public enum DiffStatus
    {
        Error,
        Identical,
        Modified,
        Added,
        Deleted
    }
}
