# differ

Simple command-line tool to see if directories contain identical files

## Usage

```txt
differ <left> <right>
```

The tool will recursively iterate through both the _left_ and the _right_ directories, comparing the contents of files in each directory. It writes to the console a status for each file.

* __Added__ - The file is present only in _right_.
* __Deleted__ - The file is present only in _left_.
* __Identical__ - The file is present in both _left_ and _right_ and the contents have not changed.
* __Modified__ - The file is present in both _left_ and _right_ and the contents have changed.

The tool compares filenames using a case-insensitive comparer. File renames are not tracked. If a file is renamed from _a.txt_ to _b.txt_ between the two directories, this will be reported as an addition (for _b.txt_) and a deletion (for _a.txt_).
