using System.Diagnostics.CodeAnalysis;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace System;
#pragma warning restore IDE0130 // Namespace does not match folder structure

public static class StringExtensions
{
    public static bool IsNullOrEmpty([NotNullWhen(false)] this string? s) => string.IsNullOrEmpty(s);
    public static bool IsNullOrWhiteSpace([NotNullWhen(false)] this string? s) => string.IsNullOrWhiteSpace(s);
}
