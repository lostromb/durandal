using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using Spark;
using Spark.FileSystem;

namespace RazorCompile
{
    public class Program
    {
        public static void Main(string[] args)
        {
            Console.WriteLine("Running Razor compiler; args are \"" + string.Join(" ", args) + "\"");
            /*TransformFile(new FileInfo(
                @"C:\Users\lostromb\Documents\Visual Studio 2013\Projects\Durandal\RazorCompile\Html.spark"),
                @"C:\Users\lostromb\Documents\Visual Studio 2013\Projects\Durandal\RazorCompile\Html.cs");*/

            if (args.Length >= 1)
            {
                //Args[0] is the current working directory
                try
                {
                    string rawDir = args[0].Trim('\\', '\"');
                    if (string.IsNullOrWhiteSpace(rawDir))
                    {
                        Console.Error.WriteLine("Input .cshtml directory is empty!");
                    }
                    else
                    {
                        DirectoryInfo dir = new DirectoryInfo(rawDir);
                        RecurseDirectories(dir);
                    }
                }
                catch (Exception e)
                {
                    Console.Error.WriteLine(e.GetType() + " " + e.Message);
                    Console.Error.WriteLine(e.StackTrace);
                }
            }
            else
            {
                Console.Error.WriteLine("Arg 1 should be directory to traverse containing .cshtml files");
            }
        }

        private static void RecurseDirectories(DirectoryInfo currentDir)
        {
            foreach (FileInfo file in currentDir.GetFiles())
            {
                if (file.Extension.Equals(".cshtml", StringComparison.OrdinalIgnoreCase) ||
                    file.Extension.Equals(".spark", StringComparison.OrdinalIgnoreCase))
                {
                    TransformFile(file, currentDir.FullName + "\\" + FileNameWithoutExtension(file) + ".cs");
                }
            }
            foreach (DirectoryInfo dir in currentDir.GetDirectories())
            {
                RecurseDirectories(dir);
            }
        }

        private static string FileNameWithoutExtension(FileInfo file)
        {
            return file.Name.Substring(0, file.Name.Length - file.Extension.Length);
        }

        private static void TransformFile(FileInfo inFile, string outFile)
        {
            if (inFile.Exists)
            {
                Console.WriteLine("Transforming " + inFile.Name + " into " + outFile);
                IList<string> result = TransformFile(inFile);
                // Overwrite any existing file
                if (File.Exists(outFile))
                    File.Delete(outFile);
                File.WriteAllLines(outFile, result);
            }
            else
            {
                Console.Error.WriteLine("Input file " + inFile.FullName + " not found!");
            }
        }

        private static IList<string> TransformFile(FileInfo file)
        {
            if (!file.Exists)
                return null;
            string[] lines = File.ReadAllLines(file.FullName);
            return TransformFile(lines, file.Name.Substring(0, file.Name.LastIndexOf('.')));
        }

        private static IList<string> TransformFile(string[] input, string fileName)
        {
            SparkTemplate template = new SparkTemplate(fileName);
            return template.Render(input, fileName);
        }
    }
}
