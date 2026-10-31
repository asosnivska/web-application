namespace Slae.WebApi.Options
{
    public class JwtOptions
    {
        public const string SectionName = "Jwt";

        public string Key { get; set; } = string.Empty;
        public int ExpiresMinutes { get; set; } = 60;
    }
}
