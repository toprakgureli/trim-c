// Copyright (c) Toprak Gureli. All rights reserved.
// Licensed under the MIT license. See LICENSE in the repository root for details.

using System.Text.Json.Serialization;

namespace TrimC.Desktop.Settings
{
    /// <summary>
    /// Source-generated serialization metadata for the settings file. Enumerations are stored by name so that the file
    /// stays readable and survives reordering of enumeration members.
    /// </summary>
    [JsonSourceGenerationOptions(WriteIndented = true, UseStringEnumConverter = true, PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
    [JsonSerializable(typeof(AppSettings))]
    internal sealed partial class SettingsJsonContext : JsonSerializerContext
    {
    }
}
