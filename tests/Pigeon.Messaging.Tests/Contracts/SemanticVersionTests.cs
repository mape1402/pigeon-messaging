namespace Pigeon.Messaging.Tests.Contracts
{
    using Pigeon.Messaging.Contracts;

    public class SemanticVersionTests
    {
        [Fact]
        public void Constructor_Should_Set_Properties()
        {
            var version = new SemanticVersion(1, 2, 3);
            Assert.Equal(1, version.Major);
            Assert.Equal(2, version.Minor);
            Assert.Equal(3, version.Patch);
        }

        [Theory]
        [InlineData(-1, 0, 0)]
        [InlineData(0, -1, 0)]
        [InlineData(0, 0, -1)]
        public void Constructor_Should_Throw_ArgumentOutOfRange(int major, int minor, int patch)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new SemanticVersion(major, minor, patch));
        }

        [Theory]
        [InlineData("1.2.3")]
        [InlineData("10.20.30")]
        public void TryParse_Should_Parse_Valid_String(string input)
        {
            var result = SemanticVersion.TryParse(input, out var version);

            Assert.True(result);
            Assert.NotEqual(default, version);
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("abc")]
        [InlineData("1.2")]
        [InlineData("1.2.3.4")]
        public void TryParse_Should_Return_False_For_Invalid(string input)
        {
            var result = SemanticVersion.TryParse(input, out _);
            Assert.False(result);
        }

        [Fact]
        public void Parse_Should_Throw_For_Invalid()
        {
            Assert.Throws<FormatException>(() => SemanticVersion.Parse("abc"));
        }

        [Fact]
        public void Default_Should_Return_1_0_0()
        {
            var version = SemanticVersion.Default;
            Assert.Equal(new SemanticVersion(1, 0, 0), version);
        }

        [Fact]
        public void Operators_Should_Work()
        {
            var v1 = new SemanticVersion(1, 0, 0);
            var v2 = new SemanticVersion(1, 0, 1);

            Assert.True(v1 < v2);
            Assert.True(v2 > v1);
            Assert.True(v1 <= v2);
            Assert.True(v2 >= v1);
            Assert.True(v1 == new SemanticVersion(1, 0, 0));
            Assert.True(v1 != v2);
        }

        [Fact]
        public void Implicit_Conversion_Should_Work()
        {
            SemanticVersion version = "2.3.4";
            string versionString = version;
            Assert.Equal("2.3.4", versionString);
        }
    }
}
