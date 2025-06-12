using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Packaging;
using System.Linq;
using System.Text;
using Durandal.API;
using Durandal.Common.NLP;

namespace TestPackager
{
    using Durandal.Common.Security;

    public class Program
    {
        public static void Main(string[] args)
        {
            CreatePackage("test.pkg");
            ReadPackage("test.pkg");
            Console.WriteLine("Done");
        }

        private static void CreatePackage(string path)
        {
            if (File.Exists(path))
                File.Delete(path);
            Package package = Package.Open(path, FileMode.OpenOrCreate, FileAccess.ReadWrite);
            PackageDigitalSignatureManager signer = new PackageDigitalSignatureManager(package);
            IList<Uri> partUris = new List<Uri>();
            partUris.Add(AddTrainingData(new FileInfo(".\\training.txt"), package));
            partUris.Add(AddBinaryData(new FileInfo(".\\binary.dll"), package));
            signer.Sign(partUris);
            Console.WriteLine("Signing...");
            package.Close();
        }

        private static void ReadPackage(string path)
        {
            Package package = Package.Open(path, FileMode.Open, System.IO.FileAccess.Read);
            PackageDigitalSignatureManager signer = new PackageDigitalSignatureManager(package);
            Console.WriteLine("Signed? " + signer.IsSigned);
            VerifyResult result = signer.VerifySignatures(false);
            Console.WriteLine("Verified? " + (result == VerifyResult.Success));
            foreach (PackagePart x in package.GetParts())
            {
                if (x.ContentType.StartsWith("cortana"))
                {
                    StreamReader reader = new StreamReader(x.GetStream());
                    Console.WriteLine("From part " + x.Uri + " (type " + x.ContentType + ") we get:");
                    Console.WriteLine(reader.ReadToEnd());
                }
            }
        }

        private static Uri AddTrainingData(FileInfo testDataFileName, Package package)
        {
            Uri partUri = PackUriHelper.CreatePartUri(new Uri("/training/" + testDataFileName.Name, UriKind.Relative));
            PackagePart newPart = package.CreatePart(partUri, "cortana/training", CompressionOption.Normal);
            StreamReader reader = new StreamReader(testDataFileName.FullName);
            StreamWriter writer = new StreamWriter(newPart.GetStream());
            writer.Write(reader.ReadToEnd());
            writer.Close();
            return partUri;
        }

        private static Uri AddBinaryData(FileInfo binaryFileName, Package package)
        {
            Uri partUri = PackUriHelper.CreatePartUri(new Uri("/binary/" + binaryFileName.Name, UriKind.Relative));
            PackagePart newPart = package.CreatePart(partUri, "cortana/plugin", CompressionOption.SuperFast);
            StreamReader reader = new StreamReader(binaryFileName.FullName);
            StreamWriter writer = new StreamWriter(newPart.GetStream());
            writer.Write(reader.ReadToEnd());
            writer.Close();
            return partUri;
        }
    }
}
