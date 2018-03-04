using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

namespace LightGbmDotNet
{
    public class DataSet
    {
        private DataSet(string filePath, int columnCount, int rowCount)
        {
            FilePath = filePath;
            ColumnCount = columnCount;
            RowCount = rowCount;
        }

        public string FilePath { get; private set; }
        public bool Exists => File.Exists(FilePath);
        public string FileName => Path.GetFileName(FilePath);

        public string FilePathShort
        {
            get
            {
                var sb = new StringBuilder(300);
                GetShortPathName(FilePath, sb, 300);
                return sb.ToString();
            }
        }

        public int ColumnCount { get; }
        public int RowCount { get; }

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern int GetShortPathName(String pathName, StringBuilder shortName, int cbShortName);

        private static int dataSetCounter;

        public static DataSet CreateNew(DirectoryInfo d, IEnumerable<IEnumerable<double>> rows)
        {
            string filePath;
            do
            {
                filePath = Path.Combine(d.FullName, "DataSet_" + ++dataSetCounter + ".csv");
            } while (File.Exists(filePath));

            var rowAndColumnCount = WriteCsv(filePath, rows);
            return new DataSet(filePath, rowAndColumnCount.ColumnCount, rowAndColumnCount.RowCount);
        }

        private class RowAndColumnCount
        {
            public RowAndColumnCount(int columnCount, int rowCount)
            {
                ColumnCount = columnCount;
                RowCount = rowCount;
            }

            public int ColumnCount { get; }
            public int RowCount { get; }
        }

        private static RowAndColumnCount WriteCsv(string path, IEnumerable<IEnumerable<double>> rows)
        {
            var colCount = 0;
            var rowCount = 0;
            var oldCulture = Thread.CurrentThread.CurrentCulture;
            try
            {
                Thread.CurrentThread.CurrentCulture = englishCulture;
                using (var s = new StreamWriter(path, false, Encoding.Default))
                {
                    foreach (var row in rows.StartReadingAhead())
                    {
                        rowCount++;
                        var hasPredecessorColumn = false;
                        colCount = 0;
                        foreach (var value in row)
                        {
                            if (hasPredecessorColumn)
                                s.Write(',');
                            else
                                hasPredecessorColumn = true;
                            s.Write(value);
                            colCount++;
                        }

                        s.WriteLine();
                    }
                }

                return new RowAndColumnCount(colCount, rowCount);
            }
            finally
            {
                Thread.CurrentThread.CurrentCulture = oldCulture;
            }
        }

        private static readonly CultureInfo englishCulture = CultureInfo.GetCultureInfo("en");

        internal void BeginUse()
        {
            lock (useSyncLockObj)
            {
                while (startedUseCount > 0 && finishedUseCount == 0)
                    Thread.Sleep(10); //lets wait for the first one to complete (makes sure the binary file is written)
                startedUseCount++;
            }
        }

        internal void CompleteUse()
        {
            finishedUseCount++; //if the first run is finished, the binary file is written and the ones that are started in other threads may start running
        }

        internal void RollbackUse()
        {
            startedUseCount--; //if the first run is rolled back, the one that was in the wait loop above will start and the others waiting will wait
        }

        private int startedUseCount;
        private int finishedUseCount;
        private readonly object useSyncLockObj = new object();
    }
}