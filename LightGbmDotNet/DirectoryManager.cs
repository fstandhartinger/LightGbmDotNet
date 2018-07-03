using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;

namespace LightGbmDotNet
{
    public class DirectoryManager
    {
        private const string TempDirName = "LightGbmDotNet";
        public static readonly DirectoryManager Instance = new DirectoryManager();

        private static readonly object lockObj = new object();

        private readonly HashSet<DirectoryInfo> keepAliveDirectories = new HashSet<DirectoryInfo>(DirInfoEqualityComparer.Instance);

        private DirectoryManager()
        {
            TempDir = new DirectoryInfo(Path.Combine(Path.GetTempPath(), TempDirName));
        }

        private DirectoryInfo tempDir;
        /// <summary>
        /// Gets or sets the directory in which LightGBM temp data will be stored (and automatically cleaned after disposing LightGbm instances)
        /// </summary>
        public DirectoryInfo TempDir
        {
            get => tempDir;
            set
            {
                if (DirInfoEqualityComparer.Instance.Equals(tempDir, value)) return;
                tempDir = value;
                OnTempDirChanged();
            }
        }

        private void OnTempDirChanged()
        {
            StartHeartbeat();
            StartAutoCleanup();
        }

        private void StartHeartbeat()
        {
            var t = new Thread(() =>
            {
                var dir = TempDir;
                while (true)
                {
                    lock (lockObj)
                    {
                        foreach (var d in keepAliveDirectories)
                        {
                            var keepAliveFilePath = GetKeepAliveFilePath(d);
                            if (keepAliveFilePath.Exists)
                                keepAliveFilePath.LastWriteTime = DateTime.Now;
                        }
                        if (dir != TempDir && keepAliveDirectories.Count == 0)
                            break;
                    }
                    Thread.Sleep(60 * 1000);
                }
            })
            { IsBackground = true };
            t.Start();
        }

        private void StartAutoCleanup()
        {
            var dir = TempDir;
            var t = new Thread(() =>
                {
                    while (true)
                    {
                        lock (lockObj)
                        {
                            if (dir != TempDir && keepAliveDirectories.Count == 0)
                                break;
                            if (!dir.Exists) continue;
                            foreach (var d in dir.GetDirectories())
                            {
                                try
                                {
                                    if (keepAliveDirectories.Contains(d))
                                        continue;
                                    var keepAliveFilePath = GetKeepAliveFilePath(d);
                                    if ((DateTime.Now - keepAliveFilePath.LastWriteTime).TotalMinutes > 2)
                                        CleanupDirectory(d); //seems to be from an older, cancelled run, cleanup
                                }
                                catch
                                {
                                    //nevermind
                                }
                            }
                        }
                        Thread.Sleep(60 * 1000);
                    }
                })
            { IsBackground = true };
            t.Start();
        }

        public void CleanupDirectory(DirectoryInfo d)
        {
            lock (lockObj)
            {
                keepAliveDirectories.Remove(d);
                try
                {
                    var files = d.GetFiles("*.*", SearchOption.AllDirectories);
                    foreach (var f in files)
                    {
                        try
                        {
                            f.Delete();
                        }
                        catch
                        {
                            //ignore, file still in use
                        }
                    }

                    d.Delete(true);
                }
                catch
                {
                    //ignore, still in use, clean as much as possible
                }
            }
        }

        private FileInfo GetKeepAliveFilePath(DirectoryInfo d)
        {
            return new FileInfo(Path.Combine(d.FullName, "heartbeat"));
        }

        public DirectoryInfo CreateTempDirectory(bool useGpu, string explicitlyDefinedTempDir = null)
        {
            lock (lockObj)
            {
                DirectoryInfo tempDirectory;
                string dir;
                if (explicitlyDefinedTempDir == null)
                {
                    var rnd = new Random();
                    do
                    {
                        dir = Path.Combine(TempDir.FullName, rnd.Next(1000).ToString("0000"));
                    } while (Directory.Exists(dir));
                    tempDirectory = new DirectoryInfo(dir);
                    Directory.CreateDirectory(dir);
                }
                else
                {
                    dir = explicitlyDefinedTempDir;
                    tempDirectory = new DirectoryInfo(dir);
                    if (!Directory.Exists(dir))
                        Directory.CreateDirectory(dir);
                }
                keepAliveDirectories.Add(tempDirectory);
                File.WriteAllText(GetKeepAliveFilePath(tempDirectory).FullName, string.Empty);

                var assembly = typeof(LightGbm).Assembly;
                var version = useGpu ? "GPU" : "CPU";
                foreach (var rn in assembly.GetManifestResourceNames().Where(n => n.Contains(version)))
                {
                    var fn = string.Join(".", rn.Split('.').Reverse().Take(2).Reverse());
                    using (var s = File.Create(Path.Combine(dir, fn)))
                    {
                        assembly.GetManifestResourceStream(rn).CopyTo(s);
                    }
                }

                return tempDirectory;
            }
        }


        private sealed class DirInfoEqualityComparer : IEqualityComparer<DirectoryInfo>
        {
            private DirInfoEqualityComparer() { }
            public static readonly DirInfoEqualityComparer Instance = new DirInfoEqualityComparer();

            public bool Equals(DirectoryInfo x, DirectoryInfo y)
            {
                if (ReferenceEquals(x, y)) return true;
                if (ReferenceEquals(x, null)) return false;
                if (ReferenceEquals(y, null)) return false;
                if (x.GetType() != y.GetType()) return false;
                return string.Equals(x.FullName, y.FullName, StringComparison.OrdinalIgnoreCase);
            }

            public int GetHashCode(DirectoryInfo obj)
            {
                return obj.FullName != null ? obj.FullName.GetHashCode() : 0;
            }
        }
    }
}