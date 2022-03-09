using System.IO.MemoryMappedFiles;
using System.Threading.Channels;

namespace Differ
{
    internal sealed class DiffRunner
    {
        private readonly DirectoryInfo _left;
        private readonly DirectoryInfo _right;
        private readonly ChannelWriter<DiffResult> _writer;

        public DiffRunner(DirectoryInfo left, DirectoryInfo right, ChannelWriter<DiffResult> writer)
        {
            _left = left;
            _right = right;
            _writer = writer;
        }

        public void Process()
        {
            Task.Run(async () =>
            {
                List<Task> tasks = new List<Task>();

                HashSet<string> filenames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (string filename in GetRecursiveFileList(_left).Concat(GetRecursiveFileList(_right)))
                {
                    if (filenames.Add(filename))
                    {
                        // first time this file has been seen - process it!
                        var newTask = Task.Run(() => _writer.WriteAsync(new DiffResult(filename, GetStatus(filename))));
                        tasks.Add(newTask);
                    }
                }

                await Task.WhenAll(tasks);
                _writer.Complete();
            });
        }

        private DiffStatus GetStatus(string relativeFilename)
        {
            try
            {
                FileInfo left = new FileInfo(_left.FullName + relativeFilename);
                FileInfo right = new FileInfo(_right.FullName + relativeFilename);

                bool leftExists = left.Exists;
                bool rightExists = right.Exists;

                if (leftExists && !rightExists) { return DiffStatus.Deleted; }
                if (rightExists && !leftExists) { return DiffStatus.Added; }
                if (!leftExists && !rightExists) { return DiffStatus.Error; }

                long leftLength = left.Length;
                long rightLength = right.Length;

                if (leftLength != rightLength) { return DiffStatus.Modified; }
                if (leftLength == 0 && rightLength == 0) { return DiffStatus.Identical; }
                if (leftLength == 0 || rightLength == 0) { return DiffStatus.Modified; }

                using var leftMMF = OpenFile(left);
                using var rightMMF = OpenFile(right);

                return AreFilesIdentical(leftMMF, rightMMF, leftLength) ? DiffStatus.Identical : DiffStatus.Modified;
            }
            catch
            {
                return DiffStatus.Error;
            }
        }

        private static MemoryMappedFile OpenFile(FileInfo file)
        {
            FileStream fs = file.Open(FileMode.Open, FileAccess.Read, FileShare.Read);
            return MemoryMappedFile.CreateFromFile(fs, null, 0, MemoryMappedFileAccess.Read, HandleInheritability.None, false);
        }

        private static unsafe bool AreFilesIdentical(MemoryMappedFile left, MemoryMappedFile right, long actualLength)
        {
            using var leftAccr = left.CreateViewAccessor(0, actualLength, MemoryMappedFileAccess.Read);
            using var rightAccr = right.CreateViewAccessor(0, actualLength, MemoryMappedFileAccess.Read);

            var leftHnd = leftAccr.SafeMemoryMappedViewHandle;
            var rightHnd = rightAccr.SafeMemoryMappedViewHandle;

            if (leftHnd.ByteLength != rightHnd.ByteLength) { return false; }

            byte* leftPtr = null;
            byte* rightPtr = null;

            try
            {
                leftHnd.AcquirePointer(ref leftPtr);
                rightHnd.AcquirePointer(ref rightPtr);

                ulong currentOffset = 0;
                ulong remainingByteLength = leftHnd.ByteLength;
                while (remainingByteLength > 0)
                {
                    int thisSpanSize = (int)Math.Min(int.MaxValue, remainingByteLength);
                    ReadOnlySpan<byte> leftSpan = new ReadOnlySpan<byte>(leftPtr + currentOffset, thisSpanSize);
                    ReadOnlySpan<byte> rightSpan = new ReadOnlySpan<byte>(rightPtr + currentOffset, thisSpanSize);
                    if (!leftSpan.SequenceEqual(rightSpan))
                    {
                        return false;
                    }

                    currentOffset += (uint)thisSpanSize;
                    remainingByteLength -= (uint)thisSpanSize;
                }

                return true;
            }
            finally
            {
                if (leftPtr != null) { leftHnd.ReleasePointer(); }
                if (rightPtr != null) { rightHnd.ReleasePointer(); }
            }
        }

        private static IEnumerable<string> GetRecursiveFileList(DirectoryInfo path)
        {
            int pathLength = path.FullName.Length;
            var allFiles = Directory.EnumerateFiles(path.FullName, "*", SearchOption.AllDirectories);
            foreach (var file in allFiles)
            {
                yield return file.Substring(pathLength);
            }
        }
    }
}
