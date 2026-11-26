using KleeneStar.Core.WebRestApi;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.Json;
using WebExpress.WebCore.WebMessage;
using WebExpress.WebCore.WebParameter;

namespace KleeneStar.Calendar.WebRestApi
{
    /// <summary>
    /// The submitted values of a dialog, read without regard to how the form sent them: a JSON
    /// object (the data forms) or form fields, a check box as <c>true</c>, <c>"true"</c> or
    /// <c>"on"</c>, a multiple selection as an array or a list separated by semicolons or commas.
    /// </summary>
    public sealed class FormPayload
    {
        private readonly Dictionary<string, string> _values;
        private readonly Dictionary<string, (string Name, byte[] Data)> _files;

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="values">The values, keyed by input name.</param>
        /// <param name="files">The uploaded files, keyed by input name.</param>
        public FormPayload(IDictionary<string, string> values, IDictionary<string, (string Name, byte[] Data)> files = null)
        {
            _values = new Dictionary<string, string>(values ?? new Dictionary<string, string>(), StringComparer.OrdinalIgnoreCase);
            _files = new Dictionary<string, (string, byte[])>(files ?? new Dictionary<string, (string, byte[])>(), StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Reads the payload of a request.
        /// </summary>
        /// <param name="request">The request.</param>
        /// <returns>The payload; empty when the request carries none.</returns>
        public static FormPayload Read(IRequest request)
        {
            var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var files = new Dictionary<string, (string, byte[])>(StringComparer.OrdinalIgnoreCase);

            var json = JsonApi.Read<Dictionary<string, JsonElement>>(request);

            foreach (var (key, element) in json ?? [])
            {
                values[key] = Text(element);
            }

            foreach (var parameter in request?.Parameters ?? [])
            {
                if (parameter is ParameterFile file && !string.IsNullOrWhiteSpace(file.Key))
                {
                    files[file.Key] = (file.Value, file.Data);
                }
            }

            return new FormPayload(values, files);
        }

        /// <summary>
        /// Determines whether the payload carries an input.
        /// </summary>
        /// <param name="name">The input name.</param>
        /// <returns><see langword="true"/> when it does.</returns>
        public bool Has(string name)
        {
            return _values.ContainsKey(name);
        }

        /// <summary>
        /// Returns a submitted text, trimmed; <see langword="null"/> for a blank or missing one.
        /// </summary>
        /// <param name="name">The input name.</param>
        /// <returns>The text.</returns>
        public string Text(string name)
        {
            return _values.TryGetValue(name, out var value) && !string.IsNullOrWhiteSpace(value) ? value.Trim() : null;
        }

        /// <summary>
        /// Returns a submitted text as it was sent; <see langword="null"/> for a missing one.
        /// </summary>
        /// <param name="name">The input name.</param>
        /// <returns>The text.</returns>
        public string Raw(string name)
        {
            return _values.TryGetValue(name, out var value) ? value : null;
        }

        /// <summary>
        /// Returns a submitted check box.
        /// </summary>
        /// <param name="name">The input name.</param>
        /// <param name="fallback">The value of a missing input.</param>
        /// <returns>The value.</returns>
        public bool Flag(string name, bool fallback = false)
        {
            if (!_values.TryGetValue(name, out var value) || value is null)
            {
                return fallback;
            }

            return value.Trim().ToLowerInvariant() is "true" or "on" or "1" or "yes" or "checked";
        }

        /// <summary>
        /// Returns a submitted whole number.
        /// </summary>
        /// <param name="name">The input name.</param>
        /// <returns>The number, or <see langword="null"/>.</returns>
        public int? Number(string name)
        {
            return int.TryParse(Text(name), NumberStyles.Integer, CultureInfo.InvariantCulture, out var number) ? number : null;
        }

        /// <summary>
        /// Returns a submitted id.
        /// </summary>
        /// <param name="name">The input name.</param>
        /// <returns>The id, or <see langword="null"/>.</returns>
        public Guid? Id(string name)
        {
            return Ids(name).Cast<Guid?>().FirstOrDefault();
        }

        /// <summary>
        /// Returns the ids of a multiple selection.
        /// </summary>
        /// <param name="name">The input name.</param>
        /// <returns>The distinct ids, in the given order.</returns>
        public IReadOnlyList<Guid> Ids(string name)
        {
            return [.. List(name)
                .Select(x => Guid.TryParse(x, out var id) ? id : Guid.Empty)
                .Where(x => x != Guid.Empty)
                .Distinct()];
        }

        /// <summary>
        /// Returns the items of a multiple selection or list.
        /// </summary>
        /// <param name="name">The input name.</param>
        /// <returns>The items.</returns>
        public IReadOnlyList<string> List(string name)
        {
            var text = Raw(name);

            if (string.IsNullOrWhiteSpace(text))
            {
                return [];
            }

            return [.. text
                .Split([';', ','], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(x => x.Length > 0)];
        }

        /// <summary>
        /// Returns the content of an uploaded file: a file part of a multipart request, or a
        /// data address (<c>data:…;base64,…</c>) or plain text the form sent as the value.
        /// </summary>
        /// <param name="name">The input name.</param>
        /// <returns>The content as text, or <see langword="null"/>.</returns>
        public string File(string name)
        {
            if (_files.TryGetValue(name, out var file) && file.Data is { Length: > 0 })
            {
                return Decode(file.Data);
            }

            var value = Raw(name);

            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            var comma = value.IndexOf(',');

            if (value.StartsWith("data:", StringComparison.OrdinalIgnoreCase) && comma > 0)
            {
                var header = value[..comma];
                var body = value[(comma + 1)..];

                try
                {
                    return header.Contains(";base64", StringComparison.OrdinalIgnoreCase)
                        ? Decode(Convert.FromBase64String(body))
                        : Uri.UnescapeDataString(body);
                }
                catch (FormatException)
                {
                    return null;
                }
            }

            // a file input that sends the bare name carries no content
            return value.Contains("BEGIN:", StringComparison.OrdinalIgnoreCase) ? value : null;
        }

        /// <summary>
        /// Writes a JSON value as text.
        /// </summary>
        /// <param name="element">The value.</param>
        /// <returns>The text.</returns>
        private static string Text(JsonElement element)
        {
            return element.ValueKind switch
            {
                JsonValueKind.String => element.GetString(),
                JsonValueKind.True => "true",
                JsonValueKind.False => "false",
                JsonValueKind.Null or JsonValueKind.Undefined => null,
                JsonValueKind.Number => element.GetRawText(),
                JsonValueKind.Array => string.Join(";", element.EnumerateArray().Select(Text).Where(x => !string.IsNullOrWhiteSpace(x))),
                JsonValueKind.Object => element.TryGetProperty("id", out var id) ? Text(id)
                    : element.TryGetProperty("value", out var value) ? Text(value)
                    : element.GetRawText(),
                _ => element.GetRawText()
            };
        }

        /// <summary>
        /// Decodes the bytes of a file, UTF-8 with or without byte order mark.
        /// </summary>
        /// <param name="data">The bytes.</param>
        /// <returns>The text.</returns>
        private static string Decode(byte[] data)
        {
            return new UTF8Encoding(false).GetString(data).TrimStart('﻿');
        }
    }
}
