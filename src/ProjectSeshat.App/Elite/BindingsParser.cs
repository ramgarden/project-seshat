using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;

namespace ProjectSeshat.App.Elite;

/// <summary>A single ED key binding: the named action, the device it lives on, and the key text.</summary>
public sealed record BindingEntry(
    string Name,
    string Device,
    string Key,
    string? Modifier,
    bool IsKeyboard);

/// <summary>Parses Elite Dangerous <c>*.binds</c> XML files into named bindings.</summary>
public static class BindingsParser
{
    private const string KeyboardDevice = "Keyboard";

    /// <summary>Parses a binds XML string, returning each keyboard/mouse action with an assigned key.</summary>
    public static IReadOnlyList<BindingEntry> Parse(string xml)
    {
        var entries = new List<BindingEntry>();
        if (string.IsNullOrWhiteSpace(xml))
        {
            return entries;
        }

        XDocument document;
        try
        {
            document = XDocument.Parse(xml);
        }
        catch
        {
            return entries;
        }

        var root = document.Root;
        if (root is null)
        {
            return entries;
        }

        foreach (var element in root.Elements())
        {
            var name = element.Name.LocalName;
            var primary = (string?)element.Attribute("Primary") ?? "";
            var secondary = (string?)element.Attribute("Secondary") ?? "";
            var key = (string?)element.Attribute("Key") ?? "";
            var modifier = (string?)element.Attribute("KeyModifier") ?? "";

            if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(key))
            {
                continue;
            }

            var device = string.IsNullOrWhiteSpace(primary) ? secondary : primary;

            entries.Add(new BindingEntry(
                name,
                device,
                key,
                string.IsNullOrWhiteSpace(modifier) ? null : modifier,
                IsKeyboard: device == KeyboardDevice));
        }

        return entries;
    }

    /// <summary>
    /// Returns the keyboard binding for a given action name, preferring the primary device and
    /// ignoring any entry with an empty key. Returns null when unbound or only on a non-keyboard device.
    /// </summary>
    public static BindingEntry? FindKeyboard(IReadOnlyList<BindingEntry> bindings, string bindingName)
        => bindings.FirstOrDefault(b =>
            string.Equals(b.Name, bindingName, StringComparison.OrdinalIgnoreCase) &&
            b.IsKeyboard &&
            !string.IsNullOrWhiteSpace(b.Key));
}
