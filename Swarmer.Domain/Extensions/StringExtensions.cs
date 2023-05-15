namespace Swarmer.Domain.Extensions;

public static class StringExtensions
{
	public static string Truncate(this string value, int maxChars)
	{
		if (string.IsNullOrEmpty(value))
			return value;

		return value.Length <= maxChars ? value : value[..(maxChars - 1)] + "…";
	}

	public static string FormatDimensions(this string value)
		=> value.Replace("{height}", "1080").Replace("{width}", "1920");
}
