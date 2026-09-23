using System;
using System.Collections;
// Column sort glyph helpers are UI-agnostic (used by Avalonia DataGrid).

namespace VeFoldery.Core.Models
{
    public class FileItemComparer
    {
        private readonly int _column;
        private readonly bool _ascending;

        public static void Sort(System.Collections.Generic.List<FileItem> list, int column, bool ascending)
        {
            if (list == null || list.Count <= 1) return;

            Comparison<FileItem> comparison = column switch
            {
                0 => (x, y) => string.Compare(x.Name, y.Name, StringComparison.CurrentCultureIgnoreCase),
                1 => CompareType,
                2 => (x, y) => DateTime.Compare(x.ModifiedDate, y.ModifiedDate),
                3 => (x, y) => DateTime.Compare(x.CreatedDate, y.CreatedDate),
                4 => (x, y) => string.Compare(x.TargetFolder, y.TargetFolder, StringComparison.CurrentCultureIgnoreCase),
                5 => (x, y) => string.Compare(x.TargetFolder, y.TargetFolder, StringComparison.CurrentCultureIgnoreCase),
                6 => CompareSize,
                _ => (x, y) => DateTime.Compare(x.ModifiedDate, y.ModifiedDate)
            };

            if (ascending) list.Sort(comparison);
            else list.Sort((x, y) => comparison(y, x));
        }

        public FileItemComparer(int column, bool ascending)
        {
            _column = column;
            _ascending = ascending;
        }

        // Non-generic IComparer removed: it existed only for legacy ListView sorting.

        private static int CompareType(FileItem f1, FileItem f2)
        {
            if (f1.IsDirectory != f2.IsDirectory)
                return f1.IsDirectory ? -1 : 1;
            return string.Compare(f1.TypeDisplay, f2.TypeDisplay, StringComparison.CurrentCultureIgnoreCase);
        }

        private static int CompareSize(FileItem f1, FileItem f2)
        {
            if (f1.IsDirectory && f2.IsDirectory) return string.Compare(f1.Name, f2.Name, StringComparison.CurrentCultureIgnoreCase);
            if (f1.IsDirectory) return -1;
            if (f2.IsDirectory) return 1;
            return f1.Size.CompareTo(f2.Size);
        }
    }
}
