
namespace NyxCEngine.Util.Helpers
{
  public static class PremadePaths
  {
    public static string GetCustomerRoot(string premadeRoot, string customerId)
    {
      var path = Path.Combine(premadeRoot, customerId);
      Directory.CreateDirectory(path);
      return path;
    }

    public static string GetSeriesRoot(string premadeRoot, string customerId, string seriesName)
    {
      // Optional: sanitize seriesName for filesystem
      var safeName = string.Concat(seriesName.Where(c => !Path.GetInvalidFileNameChars().Contains(c))).Trim();
      if (string.IsNullOrWhiteSpace(safeName)) safeName = "series";

      var path = Path.Combine(premadeRoot, customerId, safeName);
      Directory.CreateDirectory(path);
      return path;
    }
  }
}
