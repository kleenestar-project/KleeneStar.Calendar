using System;
using WebExpress.WebCore.WebParameter;

namespace KleeneStar.Calendar.WebParameter
{
    /// <summary>
    /// The route parameter naming a layer of a workspace calendar.
    /// </summary>
    public sealed class SourceIdParameter : IParameterStatic
    {
        /// <summary>
        /// Gets the key of the parameter.
        /// </summary>
        public static string Key => "sourceid";

        /// <summary>
        /// Gets or sets the scope of the parameter.
        /// </summary>
        public ParameterScope Scope { get; set; }

        /// <summary>
        /// Gets or sets the value of the parameter.
        /// </summary>
        public string Value { get; set; }

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        public SourceIdParameter()
        {
        }

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="value">The layer.</param>
        public SourceIdParameter(Guid value)
        {
            Value = value.ToString();
        }

        /// <summary>
        /// Returns the key of the parameter.
        /// </summary>
        /// <returns>The key.</returns>
        public string GetKey()
        {
            return Key;
        }
    }
}
