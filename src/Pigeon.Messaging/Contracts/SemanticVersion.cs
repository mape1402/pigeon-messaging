namespace Pigeon.Messaging.Contracts
{
    using System;
    using System.IO;
    using System.Text.RegularExpressions;

    /// <summary>
    /// Represents a Semantic Versioning (SemVer) version with only Major, Minor, and Patch components.
    /// </summary>
    public readonly struct SemanticVersion : IComparable<SemanticVersion>, IEquatable<SemanticVersion>
    {
        /// <summary>
        /// Major version: incompatible API changes.
        /// </summary>
        public int Major { get; }

        /// <summary>
        /// Minor version: added functionality in a backwards-compatible manner.
        /// </summary>
        public int Minor { get; }

        /// <summary>
        /// Patch version: backwards-compatible bug fixes.
        /// </summary>
        public int Patch { get; }

        // Regex to validate and parse semantic version strings with only major.minor.patch
        private static readonly Regex SemVerRegex = new Regex(
            @"^(?<major>0|[1-9]\d*)" +   // Major
            @"\.(?<minor>0|[1-9]\d*)" +  // Minor
            @"\.(?<patch>0|[1-9]\d*)$",  // Patch
            RegexOptions.Compiled);

        /// <summary>
        /// Constructs a SemanticVersion with the given components.
        /// </summary>
        public SemanticVersion(int major, int minor, int patch)
        {
            if (major < 0) throw new ArgumentOutOfRangeException(nameof(major));
            if (minor < 0) throw new ArgumentOutOfRangeException(nameof(minor));
            if (patch < 0) throw new ArgumentOutOfRangeException(nameof(patch));

            Major = major;
            Minor = minor;
            Patch = patch;
        }

        /// <summary>
        /// Gets the default semantic version, typically representing the initial version "1.0.0".
        /// This can be used as a fallback or a standard starting version.
        /// </summary>
        public static SemanticVersion Default => new SemanticVersion(1, 0, 0);

        /// <summary>
        /// Attempts to parse a semantic version string (format: Major.Minor.Patch).
        /// </summary>
        public static bool TryParse(string version, out SemanticVersion semVer)
        {
            semVer = default;

            if (string.IsNullOrWhiteSpace(version))
                return false;

            var match = SemVerRegex.Match(version.Trim());
            if (!match.Success)
                return false;

            if (!int.TryParse(match.Groups["major"].Value, out int major)) return false;
            if (!int.TryParse(match.Groups["minor"].Value, out int minor)) return false;
            if (!int.TryParse(match.Groups["patch"].Value, out int patch)) return false;

            semVer = new SemanticVersion(major, minor, patch);
            return true;
        }

        /// <summary>
        /// Parses a string into a SemanticVersion instance.
        /// </summary>
        /// <param name="version">The version string in the format "Major.Minor.Patch".</param>
        /// <returns>A SemanticVersion instance parsed from the input string.</returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown if the input string is null or whitespace.
        /// </exception>
        /// <exception cref="FormatException">
        /// Thrown if the input string does not match the expected semantic version format.
        /// </exception>
        public static SemanticVersion Parse(string version)
        {
            if (string.IsNullOrWhiteSpace(version))
                throw new ArgumentNullException(nameof(version));

            var match = SemVerRegex.Match(version.Trim());

            var exception = new FormatException("Invalid Semantic Version format.");

            if (!match.Success)
                throw exception;

            if (!int.TryParse(match.Groups["major"].Value, out int major))
                throw exception;

            if (!int.TryParse(match.Groups["minor"].Value, out int minor))
                throw exception;

            if (!int.TryParse(match.Groups["patch"].Value, out int patch))
                throw exception;

            return new SemanticVersion(major, minor, patch);
        }

        /// <summary>
        /// Returns the semantic version string representation.
        /// </summary>
        public override string ToString() => $"{Major}.{Minor}.{Patch}";

        /// <summary>
        /// Compares this instance to another SemanticVersion.
        /// </summary>
        public int CompareTo(SemanticVersion other)
        {
            int result = Major.CompareTo(other.Major);
            if (result != 0) return result;

            result = Minor.CompareTo(other.Minor);
            if (result != 0) return result;

            return Patch.CompareTo(other.Patch);
        }

        /// <inheritdoc/>
        public override bool Equals(object obj) => obj is SemanticVersion other && Equals(other);

        /// <inheritdoc/>
        public bool Equals(SemanticVersion other) =>
            Major == other.Major &&
            Minor == other.Minor &&
            Patch == other.Patch;

        /// <inheritdoc/>
        public override int GetHashCode() => HashCode.Combine(Major, Minor, Patch);

        /// <summary>
        /// Equality operator. Returns true if both SemanticVersion instances are equal.
        /// </summary>
        public static bool operator ==(SemanticVersion left, SemanticVersion right) => left.Equals(right);

        /// <summary>
        /// Inequality operator. Returns true if both SemanticVersion instances are not equal.
        /// </summary>
        public static bool operator !=(SemanticVersion left, SemanticVersion right) => !(left == right);

        /// <summary>
        /// Less-than operator. Returns true if left version is less than right version.
        /// </summary>
        public static bool operator <(SemanticVersion left, SemanticVersion right) => left.CompareTo(right) < 0;

        /// <summary>
        /// Less-than-or-equal operator. Returns true if left version is less than or equal to right version.
        /// </summary>
        public static bool operator <=(SemanticVersion left, SemanticVersion right) => left.CompareTo(right) <= 0;

        /// <summary>
        /// Greater-than operator. Returns true if left version is greater than right version.
        /// </summary>
        public static bool operator >(SemanticVersion left, SemanticVersion right) => left.CompareTo(right) > 0;

        /// <summary>
        /// Greater-than-or-equal operator. Returns true if left version is greater than or equal to right version.
        /// </summary>
        public static bool operator >=(SemanticVersion left, SemanticVersion right) => left.CompareTo(right) >= 0;

        /// <summary>
        /// Allows implicit conversion from a string to a SemanticVersion.
        /// This lets you assign a valid version string directly to a SemanticVersion variable
        /// without calling Parse manually.
        /// </summary>
        /// <param name="source">
        /// The version string in the format "Major.Minor.Patch".
        /// </param>
        /// <returns>
        /// A SemanticVersion instance parsed from the string.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown if the input string is null or whitespace.
        /// </exception>
        /// <exception cref="FormatException">
        /// Thrown if the input string is not a valid semantic version.
        /// </exception>
        public static implicit operator SemanticVersion(string source)
            => Parse(source);

        /// <summary>
        /// Allows implicit conversion from a SemanticVersion instance to its string representation.
        /// This lets you assign a SemanticVersion directly to a string variable without calling ToString() explicitly.
        /// </summary>
        /// <param name="version">
        /// The SemanticVersion instance to convert.
        /// </param>
        /// <returns>
        /// A string in the format "Major.Minor.Patch".
        /// </returns>
        public static implicit operator string(SemanticVersion version) => version.ToString();
    }
}
