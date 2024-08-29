using System;
using System.Text.Json;

public class SnakeCaseNamingPolicy : JsonNamingPolicy
{
    public override string ConvertName(string name)
    {
        if (string.IsNullOrEmpty(name))
            return name;

        // Convert from camelCase to snake_case
        var newName = System.Text.RegularExpressions.Regex.Replace(
            System.Text.RegularExpressions.Regex.Replace(name, "([a-z])([A-Z])", "$1_$2"), 
            "([A-Z])([A-Z][a-z])", 
            "$1_$2"
        ).ToLowerInvariant();

        return newName;
    }
}