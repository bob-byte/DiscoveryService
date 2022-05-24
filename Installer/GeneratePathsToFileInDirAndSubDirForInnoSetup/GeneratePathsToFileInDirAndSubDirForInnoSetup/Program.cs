using System;
using System.Collections.Generic;
using System.IO;

namespace GeneratePathsToFileInDirAndSubDirForInnoSetup
{
    class Program
    {
        static void Main(string[] args)
        {
            while (true)
            {
                String rootFolder;
                do
                {
                    Console.Write("Input path to root folder to get all files in it and in subdirectories: ");
                    rootFolder = Console.ReadLine();
                }
                while (!Directory.Exists(rootFolder));
                

                Int32 whetherUsePattern;
                do
                {
                    Console.WriteLine("Do you want to use search pattern?\n" +
                    "1 - yes\n" +
                    "2 - no");
                }
                while (!Int32.TryParse(Console.ReadLine(), out whetherUsePattern));

                String searchPattern = "";
                if (whetherUsePattern == 1)
                {
                    Console.Write("Input the search pattern: ");
                    searchPattern = Console.ReadLine();
                }

                Console.WriteLine("Paths to the files:");
                IEnumerable<String> paths = FileSearch.FilesInDirAndSubdir(rootFolder, searchPattern);
                try
                {
                    //show paths
                    using (StreamWriter writer = new StreamWriter($"{Environment.CurrentDirectory}\\Paths to files.txt"))
                    {
                        List<String> rowsOfIcon = new List<String>();
                        InnoSetup innoSetup = new InnoSetup();

                        foreach (var fullPathToFile in paths)
                        {
                            GetInfoAboutFile(fullPathToFile, rootFolder, out String subFolder, out String pathToFileFromBin);
                            if(subFolder == InnoSetup.IconsAttr)//to show separately icons from others folders
                            {
                                rowsOfIcon.Add(fullPathToFile);
                                continue;
                            }

                            var rowForInno = RowForInno(innoSetup, subFolder, pathToFileFromBin);

                            Console.WriteLine(rowForInno);
                            writer.WriteLine(rowForInno);
                        }

                        foreach (var fullPathToFile in rowsOfIcon)
                        {
                            GetInfoAboutFile(fullPathToFile, rootFolder, out String subFolder, out String pathToFileFromBin);
                            var rowForInno = RowForInno(innoSetup, subFolder, pathToFileFromBin);

                            Console.WriteLine(rowForInno);
                            writer.WriteLine(rowForInno);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.Message);
                }

                Console.WriteLine("Press any key to continue");
                Console.ReadKey(intercept: true);
            }
        }

        private static void GetInfoAboutFile(String pathToFile, String rootFolder,
            out String subFolder, out String pathToFileFromBin)
        {
            pathToFileFromBin = pathToFile.Replace(rootFolder, "").Substring(1);
            subFolder = "";
            
            if (pathToFileFromBin.Contains("\\"))
            {
                var indexOfLine = pathToFileFromBin.IndexOf("\\");
                subFolder = $"{pathToFileFromBin.Substring(0, length: indexOfLine)}";
            }
        }
        
        private static String RowForInno(InnoSetup innoSetup, String subFolder, String pathToFileFromBin)
        {
            var shortPathToFileForInnoSetup = $"..\\WpfClient\\LUC.WpfClient\\bin\\Debug";
            var shortPathForInnoSetup = $"{shortPathToFileForInnoSetup}\\{pathToFileFromBin}";
            var rowInInnoSetup = innoSetup.RowInInnoSetup(shortPathForInnoSetup, subFolder, InnoSetup.ValueFlagsAttr[FlagValue.IgnoreVersion]);

            return rowInInnoSetup;
        }
    }
}
