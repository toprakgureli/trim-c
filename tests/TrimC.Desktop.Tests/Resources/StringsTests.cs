// Copyright (c) Toprak Gureli. All rights reserved.
// Licensed under the MIT license. See LICENSE in the repository root for details.

using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Resources;
using System.Text.RegularExpressions;
using Xunit;

namespace TrimC.Desktop.Resources.Tests
{
    public partial class StringsTests
    {
        private static readonly ResourceManager s_resources = new("TrimC.Desktop.Resources.Strings", typeof(Strings).Assembly);

        public static TheoryData<string> Keys()
        {
            TheoryData<string> keys = [];
            foreach (PropertyInfo property in typeof(Strings).GetProperties(BindingFlags.Public | BindingFlags.Static))
            {
                keys.Add(property.Name);
            }

            return keys;
        }

        [Theory]
        [MemberData(nameof(Keys))]
        public void EveryProperty_HasEnglishAndTurkishText(string key)
        {
            Assert.False(string.IsNullOrWhiteSpace(s_resources.GetString(key, CultureInfo.InvariantCulture)));

            ResourceSet turkish = s_resources.GetResourceSet(CultureInfo.GetCultureInfo("tr"), createIfNotExists: true, tryParents: false)!;
            Assert.False(string.IsNullOrWhiteSpace(turkish.GetString(key)), $"'{key}' has no Turkish text.");
        }

        [Theory]
        [MemberData(nameof(Keys))]
        public void Translations_KeepTheSamePlaceholders(string key)
        {
            string english = s_resources.GetString(key, CultureInfo.InvariantCulture)!;
            string turkish = s_resources.GetString(key, CultureInfo.GetCultureInfo("tr"))!;

            Assert.Equal(Placeholders(english), Placeholders(turkish));
        }

        [Fact]
        public void EveryResource_HasAProperty()
        {
            HashSet<string> properties = [];
            foreach (PropertyInfo property in typeof(Strings).GetProperties(BindingFlags.Public | BindingFlags.Static))
            {
                properties.Add(property.Name);
            }

            ResourceSet english = s_resources.GetResourceSet(CultureInfo.InvariantCulture, createIfNotExists: true, tryParents: false)!;
            foreach (DictionaryEntry entry in english)
            {
                Assert.Contains((string)entry.Key, properties);
            }
        }

        private static SortedSet<string> Placeholders(string text)
        {
            SortedSet<string> placeholders = [];
            foreach (Match match in PlaceholderPattern().Matches(text))
            {
                placeholders.Add(match.Value);
            }

            return placeholders;
        }

        [GeneratedRegex(@"\{\d+\}")]
        private static partial Regex PlaceholderPattern();
    }
}
