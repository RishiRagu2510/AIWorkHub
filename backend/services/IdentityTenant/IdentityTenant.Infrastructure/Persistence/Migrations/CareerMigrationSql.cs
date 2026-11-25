using System.Reflection;
using System.Text.RegularExpressions;
namespace IdentityTenant.Infrastructure.Persistence.Migrations;

internal static class CareerMigrationSql
{
    internal static string Read(string name)
    {
        using var stream = typeof(CareerMigrationSql).Assembly.GetManifestResourceStream("IdentityTenant.Sql." + name)
            ?? throw new InvalidOperationException("Missing migration SQL: " + name);
        using var reader = new StreamReader(stream);
        
        return Regex.Replace(reader.ReadToEnd(), @"^\s*(BEGIN|COMMIT);\s*$", "", RegexOptions.Multiline);
    }
}
