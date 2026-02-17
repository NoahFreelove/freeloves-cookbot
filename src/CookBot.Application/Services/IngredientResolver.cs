using System.Text.RegularExpressions;
using CookBot.Domain.Entities;
using CookBot.Domain.Enums;
using CookBot.Domain.Interfaces;

namespace CookBot.Application.Services;

public class IngredientResolver
{
    public static string Normalize(string name)
    {
        var lower = name.ToLowerInvariant().Trim();
        lower = Regex.Replace(lower, @"[-_]", " ");
        lower = Regex.Replace(lower, @"\s+", " ");
        return lower;
    }
}
