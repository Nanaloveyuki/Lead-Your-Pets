using System;
using System.IO;
using Xunit;

namespace LeadYourPet.Tests
{
    public class FloatMenuSourceTests
    {
        [Fact]
        public void ColonistChildMenuLabelsRemainReadableChinese()
        {
            string source = File.ReadAllText(FindFloatMenuSourcePath());

            Assert.Contains("不再看着孩子", source);
            Assert.Contains("陪孩子玩...", source);
            Assert.Contains("看着孩子并牵着", source);
            Assert.Contains("不能继续看着孩子", source);
        }

        private static string FindFloatMenuSourcePath()
        {
            DirectoryInfo directory = new DirectoryInfo(Directory.GetCurrentDirectory());
            while (directory != null)
            {
                string path = Path.Combine(directory.FullName, "Source", "FloatMenuOptionProvider_LeadYourPet.cs");
                if (File.Exists(path))
                {
                    return path;
                }

                directory = directory.Parent;
            }

            throw new InvalidOperationException("未找到 FloatMenuOptionProvider_LeadYourPet.cs。");
        }
    }
}
