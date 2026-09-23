using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using Xunit;

namespace LeadYourPet.Tests
{
    public class LeadYourPetApiTests
    {
        [Fact]
        public void CopiesDistinctPetsAndEndsEveryCopiedPet()
        {
            object first = new object();
            object second = new object();
            List<object> copied = new List<object> { first };
            List<object> ended = new List<object>();

            Assert.True(LeadYourPetApiRules.CopyLinkedPets(new[] { first, second, first, null }, copied));
            Assert.Equal(new[] { first, second }, copied);
            Assert.False(LeadYourPetApiRules.CopyLinkedPets(new[] { first }, copied));
            Assert.False(LeadYourPetApiRules.CopyLinkedPets<object>(null, copied));
            Assert.False(LeadYourPetApiRules.CopyLinkedPets(copied, null));

            LeadYourPetApiRules.EndForMaster(new[] { first, null, second }, ended.Add);
            Assert.Equal(new[] { first, second }, ended);
            LeadYourPetApiRules.EndForMaster<object>(null, ended.Add);
            LeadYourPetApiRules.EndForMaster(copied, null);
        }

        [Theory]
        [InlineData(0, false)]
        [InlineData(1, true)]
        [InlineData(2, true)]
        [InlineData(3, false)]
        [InlineData(-1, false)]
        public void AcceptsOnlyExistingSpecialSources(int source, bool accepted)
        {
            Assert.Equal(accepted, LeadYourPetApiRules.IsAcceptedSpecialSource(source));
            Assert.Equal(0, LeadYourPetApiRules.None);
            Assert.Equal(1, LeadYourPetApiRules.BeggarFamily);
            Assert.Equal(2, LeadYourPetApiRules.ChildExchange);
        }

        [Fact]
        public void PublicFacadeForwardsWithoutExposingInternalTypes()
        {
            string source = File.ReadAllText(Path.Combine(RepoRoot(), "Source", "LeadYourPetApi.cs")).Replace("\r\n", "\n");
            string api = ExtractType(source, "LeadYourPetApi");
            Assert.Contains("public static bool Available => LeadYourPetUtility.Component != null;", api);
            Assert.Contains("component.TryStartRatkinMotherLeash(adult, child, false)", api);
            Assert.Contains("component.StartLeash(adult, child, true, false)", api);
            Assert.Contains("return component.LinkCount > before", api);
            Assert.Contains("component.AnchorLeashedPetsToCell(master, cell)", api);
            Assert.Contains("component.EndLeashForPet(pet, false)", api);
            Assert.Contains("LeadYourPetApiRules.EndForMaster(", api);
            Assert.Contains("component.ClearMouseEggPetState(pawn)", api);
            Assert.Contains("component.ClearTravelStock(pawn)", api);
            Assert.Contains("LeadYourPetApiRules.CopyLinkedPets(", api);
            Assert.DoesNotContain("return component;", api);
            Assert.Contains("component.GetLinkForPet(pet) != null", api);
            Assert.Contains("Log.ErrorOnce(", api);
            Assert.DoesNotContain("return new LeashLink", api);
            Assert.DoesNotContain("return new MouseEggState", api);
            Assert.DoesNotContain("MouseEggSpecialSource source", api);

            foreach (Match signature in Regex.Matches(api, @"public static (?<signature>[^{;=]+)\("))
            {
                string text = signature.Groups["signature"].Value;
                Assert.DoesNotContain("LeashLink", text);
                Assert.DoesNotContain("MouseEggState", text);
                Assert.DoesNotContain("LeadYourPetGameComponent", text);
                Assert.DoesNotContain("MouseEggSpecialSource", text);
            }
        }

        private static string ExtractType(string source, string typeName)
        {
            int start = source.IndexOf("class " + typeName, StringComparison.Ordinal);
            Assert.True(start >= 0);
            int next = source.IndexOf("\n    internal static class ", start + 1, StringComparison.Ordinal);
            Assert.True(next > start);
            return source.Substring(start, next - start);
        }

        private static string RepoRoot()
        {
            DirectoryInfo root = new DirectoryInfo(Directory.GetCurrentDirectory());
            while (root != null && !File.Exists(Path.Combine(root.FullName, "NOTICE")))
            {
                root = root.Parent;
            }

            Assert.NotNull(root);
            return root.FullName;
        }
    }
}
