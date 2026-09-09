using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Xunit;

namespace LeadYourPet.Tests
{
    public class SaveContractTests
    {
        [Fact]
        public void OriginalSaveTypesEnumsAndDefsRemainUnchanged()
        {
            DirectoryInfo root = new DirectoryInfo(Directory.GetCurrentDirectory());
            while (root != null && !File.Exists(Path.Combine(root.FullName, "NOTICE")))
                root = root.Parent;
            Assert.NotNull(root);
            XDocument manifest = XDocument.Load(Path.Combine(root.FullName, "Source", "Tests", "Fixtures", "SaveContracts.xml"));
            using (SHA256 sha = SHA256.Create())
            {
                foreach (XElement file in manifest.Root.Elements("file"))
                {
                    string source = File.ReadAllText(Path.Combine(root.FullName, (string)file.Attribute("path"))).Replace("\r\n", "\n");
                    byte[] bytes = Encoding.UTF8.GetBytes(Regex.Replace(source, @"[ \t]+(?=\n)", ""));
                    string hash = BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", "");
                    Assert.True(hash == (string)file.Attribute("sha256"), "Original save contract changed: " + file.Attribute("path"));
                }
            }
            Assert.Equal("LeadYourPet", typeof(LeadYourPetRules).Assembly.GetName().Name);
        }
    }
}
