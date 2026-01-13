using System.Diagnostics;
using System.Globalization;
using System.Text;

namespace NyxCEngine.Util.Thumbnails
{
  internal static class FfmpegThumbnailRenderer
  {
    public const int WIDTH = 1080;
    public const int HEIGHT = 1920;

    public static void RenderCenteredTextThumb(
      string inputVideoPath,
      string outputJpgPath,
      string text,
      double timestampSeconds,
      string fontFilePath,
      int fontSize,
      double overlayDarkness = 0.40,
      int borderW = 10)
    {
      if (string.IsNullOrWhiteSpace(text))
        throw new ArgumentException("Text is required.", nameof(text));

      static string EscapeText(string s)
      {
        return s
          .Replace("\\", "\\\\")
          .Replace(":", "\\:")
          .Replace("'", "\\'")
          .Replace("\r", "")
          .Replace("\n", "\\n");
      }

      static string EscapeFontPath(string path)
      {
        var p = path.Replace("\\", "/");
        p = p.Replace(":", "\\:");
        p = p.Replace("'", "\\'");
        return p;
      }

      var safeFont = EscapeFontPath(fontFilePath);
      var safeText = EscapeText(text);

      // NOTE: Some ffmpeg builds are picky with drawtext expressions.
      // Use simple centering expressions only.
      var xExpr = "(w-text_w)/2";
      var yExpr = "(h-text_h)/2";

      var vf =
        $"scale={WIDTH}:{HEIGHT}:force_original_aspect_ratio=increase," +
        $"crop={WIDTH}:{HEIGHT}," +

        // Make the background more colourful / punchy
        $"eq=saturation=1.35:contrast=1.05," +

        // MUCH lighter overlay so video stays colourful (or delete this line entirely if you want none)
        $"drawbox=x=0:y=0:w=iw:h=ih:color=black@{overlayDarkness.ToString("0.00", CultureInfo.InvariantCulture)}:t=fill," +

        // Text
        $"drawtext=fontfile='{safeFont}':" +
        $"text='{safeText}':" +
        $"fontsize={fontSize}:" +
        $"fontcolor=#FFCC00:" +                
        $"borderw={borderW}:" +
        $"bordercolor=black:" +
        $"shadowx=2:shadowy=2:shadowcolor=black@0.6:" +
        $"line_spacing=18:" +
        $"box=1:boxcolor=black@0.22:boxborderw=28:" +
        $"x={xExpr}:" +
        $"y={yExpr}";

      var psi = new ProcessStartInfo
      {
        FileName = "ffmpeg",
        RedirectStandardError = true,
        RedirectStandardOutput = true,
        UseShellExecute = false
      };

      psi.ArgumentList.Add("-hide_banner");
      psi.ArgumentList.Add("-y");

      psi.ArgumentList.Add("-ss");
      psi.ArgumentList.Add(timestampSeconds.ToString("0.000", CultureInfo.InvariantCulture));

      psi.ArgumentList.Add("-i");
      psi.ArgumentList.Add(inputVideoPath);

      psi.ArgumentList.Add("-vframes");
      psi.ArgumentList.Add("1");

      psi.ArgumentList.Add("-vf");
      psi.ArgumentList.Add(vf);

      psi.ArgumentList.Add(outputJpgPath);

      using var p = Process.Start(psi) ?? throw new Exception("Failed to start ffmpeg");
      var stderr = p.StandardError.ReadToEnd();
      p.WaitForExit();

      if (p.ExitCode != 0)
        throw new Exception($"ffmpeg thumbnail failed ({p.ExitCode}):\n{stderr}");
    }

    public static double ProbeDurationSeconds(string path)
    {
      var psi = new ProcessStartInfo
      {
        FileName = "ffprobe",
        RedirectStandardOutput = true,
        RedirectStandardError = true,
        UseShellExecute = false
      };

      psi.ArgumentList.Add("-v");
      psi.ArgumentList.Add("error");
      psi.ArgumentList.Add("-show_entries");
      psi.ArgumentList.Add("format=duration");
      psi.ArgumentList.Add("-of");
      psi.ArgumentList.Add("default=noprint_wrappers=1:nokey=1");
      psi.ArgumentList.Add(path);

      using var p = Process.Start(psi) ?? throw new Exception("Failed to start ffprobe");
      var stdout = p.StandardOutput.ReadToEnd().Trim();
      var stderr = p.StandardError.ReadToEnd();
      p.WaitForExit();

      if (p.ExitCode != 0)
        throw new Exception($"ffprobe failed: {stderr}");

      return double.Parse(stdout, CultureInfo.InvariantCulture);
    }

    public static string Stylize(string text)
    {
      text = (text ?? "").Trim();
      if (text.Length == 0) return text;
      text = string.Join(" ", text.Split(' ', StringSplitOptions.RemoveEmptyEntries));
      return text.ToUpperInvariant();
    }

    public static string WrapToTwoLines(string text, int maxCharsPerLine = 14)
    {
      text = (text ?? "").Trim();
      if (text.Length == 0) return text;

      var words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
      if (words.Length <= 2) return text;

      var line1 = new StringBuilder();
      var line2 = new StringBuilder();

      foreach (var w in words)
      {
        if (line2.Length > 0)
        {
          line2.Append(' ').Append(w);
          continue;
        }

        if (line1.Length == 0)
        {
          line1.Append(w);
          continue;
        }

        if (line1.Length + 1 + w.Length <= maxCharsPerLine)
          line1.Append(' ').Append(w);
        else
          line2.Append(w);
      }

      var l1 = line1.ToString().Trim();
      var l2 = line2.ToString().Trim();

      if (string.IsNullOrWhiteSpace(l2))
        return l1;

      const int maxLine2 = 18;
      if (l2.Length > maxLine2)
        l2 = l2.Substring(0, maxLine2).TrimEnd() + "…";

      return $"{l1}\n{l2}";
    }

    public static int ChooseFontSizeForText(string text, int big = 165, int medium = 140, int small = 115, int tiny = 92)
    {
      var lines = (text ?? "").Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
      var maxLen = lines.Length == 0 ? 0 : lines.Max(l => l.Length);

      if (maxLen <= 10) return big;
      if (maxLen <= 14) return medium;
      if (maxLen <= 18) return small;
      return tiny;
    }


    public static string OneWordPerLine(string text, int maxLines = 4)
    {
      if (string.IsNullOrWhiteSpace(text))
        return "NIGHTSHIFT";

      text = text.Replace("\r\n", " ").Replace("\n", " ").Trim();

      // Split on whitespace, remove empties
      var words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
      if (words.Count == 0) return "NIGHTSHIFT";

      // Keep only first N words; add ellipsis if we clipped
      var clipped = words.Count > maxLines;
      words = words.Take(maxLines).ToList();

      if (clipped)
      {
        // Make the last line end with …
        words[^1] = words[^1].TrimEnd('.', '!', '?') + "…";
      }

      return string.Join("\n", words);
    }
  }
}
