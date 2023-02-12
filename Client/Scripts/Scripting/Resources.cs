﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using ICSharpCode.SharpZipLib.Zip;
using RageCoop.Core;
using RageCoop.Core.Scripting;

namespace RageCoop.Client.Scripting
{
    internal class Resources
    {
        public static string TempPath;

        internal readonly ConcurrentDictionary<string, ClientResource> LoadedResources = new();

        static Resources()
        {
            TempPath = Path.Combine(Path.GetTempPath(), "RageCoop");
            if (Directory.Exists(TempPath))
                try
                {
                    Directory.Delete(TempPath, true);
                }
                catch
                {
                }

            TempPath = CoreUtils.GetTempDirectory(TempPath);
            Directory.CreateDirectory(TempPath);
        }

        public Resources()
        {
            Logger = Main.Logger;
        }

        private Logger Logger { get; }

        public void Load(string path, string[] zips)
        {
            LoadedResources.Clear();
            foreach (var zip in zips)
            {
                var zipPath = Path.Combine(path, zip);
                Logger?.Info($"Loading resource: {Path.GetFileNameWithoutExtension(zip)}");
                Unpack(zipPath, Path.Combine(path, "Data"));
            }

            Directory.GetFiles(path, "*.dll", SearchOption.AllDirectories).Where(x => x.CanBeIgnored())
                .ForEach(File.Delete);

            // TODO Core.ScheduleLoad()...
        }

        public void Unload()
        {
            // TODO Core.ScheduleUnload()...
        }

        private ClientResource Unpack(string zipPath, string dataFolderRoot)
        {
            var r = new ClientResource
            {
                Logger = Logger,
                Scripts = new List<ClientScript>(),
                Name = Path.GetFileNameWithoutExtension(zipPath),
                DataFolder = Path.Combine(dataFolderRoot, Path.GetFileNameWithoutExtension(zipPath)),
                ScriptsDirectory = Path.Combine(TempPath, Path.GetFileNameWithoutExtension(zipPath))
            };
            Directory.CreateDirectory(r.DataFolder);
            var scriptsDir = r.ScriptsDirectory;
            if (Directory.Exists(scriptsDir))
                Directory.Delete(scriptsDir, true);
            else if (File.Exists(scriptsDir)) File.Delete(scriptsDir);
            Directory.CreateDirectory(scriptsDir);

            new FastZip().ExtractZip(zipPath, scriptsDir, null);


            foreach (var dir in Directory.GetDirectories(scriptsDir, "*", SearchOption.AllDirectories))
                r.Files.Add(dir, new ResourceFile
                {
                    IsDirectory = true,
                    Name = dir.Substring(scriptsDir.Length + 1).Replace('\\', '/')
                });
            var assemblies = new Dictionary<ResourceFile, Assembly>();
            foreach (var file in Directory.GetFiles(scriptsDir, "*", SearchOption.AllDirectories))
            {
                if (Path.GetFileName(file).CanBeIgnored())
                {
                    try
                    {
                        File.Delete(file);
                    }
                    catch (Exception ex)
                    {
                        Logger.Warning(
                            $"Failed to delete API assembly: {file}. This may or may cause some unexpected behaviours.\n{ex}");
                    }

                    continue;
                }

                var relativeName = file.Substring(scriptsDir.Length + 1).Replace('\\', '/');
                var rfile = new ResourceFile
                {
                    GetStream = () => { return new FileStream(file, FileMode.Open, FileAccess.Read); },
                    IsDirectory = false,
                    Name = relativeName
                };
                r.Files.Add(relativeName, rfile);
            }

            LoadedResources.TryAdd(r.Name, r);
            return r;
        }
    }
}