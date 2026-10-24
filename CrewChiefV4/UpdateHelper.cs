using System;
using System.IO;
using System.Linq;

using CrewChiefV4.Audio;

namespace CrewChiefV4
{
    class UpdateHelper
    {
        public static void MoveDirectory(string source, string target)
        {
            var sourcePath = source.TrimEnd('\\', ' ');
            var targetPath = target.TrimEnd('\\', ' ');
            var files = Directory.EnumerateFiles(sourcePath, "*", SearchOption.AllDirectories)
                                 .GroupBy(s => Path.GetDirectoryName(s));
            foreach (var folder in files)
            {
                var targetFolder = folder.Key.Replace(sourcePath, targetPath);
                Directory.CreateDirectory(targetFolder);
                foreach (var file in folder)
                {
                    var targetFile = Path.Combine(targetFolder, Path.GetFileName(file));
                    if (File.Exists(targetFile))
                    {
                        File.Delete(targetFile);
                    }

                    File.Move(file, targetFile);
                }
            }
            Directory.Delete(source, true);
        }

        /// <summary>
        /// Check the downloaded sound pack for optional updates.txt file
        /// which allows for deleting or renaming files from earlier sound packs
        /// </summary>
        public static void ProcessFileUpdates(String unzippedSoundPackFolder)
        {
            var updatesTxt = Path.Combine(unzippedSoundPackFolder, "updates.txt");
            if (!File.Exists(updatesTxt))
            {
                Log.Commentary("Optional updates.txt not in sound pack update");
                return;
            }
            try {
                StreamReader file = new StreamReader(updatesTxt);
                int deletedCount = 0;
                int renamedCount = 0;
                string line;
                while ((line = file.ReadLine()) != null)
                {
                    lock (MainWindow.instanceLock)
                    {
                        if (MainWindow.instance == null)
                        {
                            return;
                        }
                    }
                    if (line.Trim().Length > 0 && !line.StartsWith("#"))
                    {
                        try
                        {
                            String[] directives = line.Split('|');
                            if (directives[0] == "rename")
                            {
                                File.Move(AudioPlayer.soundFilesPathNoChiefOverride + @"\" + directives[1], AudioPlayer.soundFilesPathNoChiefOverride + @"\" + directives[2]);
                                Log.Commentary($"Rename {directives[1]} from previous sound pack to {directives[2]}");
                                renamedCount++;
                            }
                            else if (directives[0] == "delete")
                            {
                                File.Delete(AudioPlayer.soundFilesPathNoChiefOverride + @"\" + directives[1]);
                                Log.Commentary($"Remove {directives[1]} from previous sound pack");
                                deletedCount++;
                            }
                        }
                        catch (Exception e) { Log.Exception(e); }
                    }
                }
                Console.WriteLine("Successfully deleted " + deletedCount + " and renamed " + renamedCount + " sound files");
                file.Close();
                File.Delete(updatesTxt);
            }
            catch (Exception ex) 
            {
                Log.Exception(ex, "Error reading updates.txt in sound pack update"); 
            }
        }
    }
}
