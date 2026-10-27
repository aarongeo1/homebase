using System;
using System.Collections.Generic;

namespace Homebase.Domain
{
    /// <summary>
    /// Explicit enum &lt;-&gt; string code table. Layers (API contract, database) each declare their own maps so they can
    /// evolve independently, and codes like "armed-home" or "motion-sensor" never depend on C# identifier spelling.
    /// </summary>
    public sealed class CodeMap<T>
        where T : struct
    {
        private readonly Dictionary<T, string> _codes = new Dictionary<T, string>();
        private readonly Dictionary<string, T> _values = new Dictionary<string, T>(StringComparer.Ordinal);

        public CodeMap<T> Add(T value, string code)
        {
            _codes.Add(value, code);
            _values.Add(code, value);
            return this;
        }

        public string Code(T value)
        {
            string code;
            if (!_codes.TryGetValue(value, out code))
            {
                throw new ArgumentOutOfRangeException(nameof(value), value, "No code for " + typeof(T).Name + "." + value + ".");
            }

            return code;
        }

        public string CodeOrNull(T? value) => value.HasValue ? Code(value.Value) : null;

        public bool TryParse(string code, out T value)
        {
            value = default(T);
            return code != null && _values.TryGetValue(code, out value);
        }

        public IEnumerable<string> Codes => _values.Keys;
    }
}
