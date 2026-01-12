using Newtonsoft.Json.Linq;
using System.Diagnostics;
using System.Text;

namespace NyxCEngine.Util.Captions
{
  internal sealed class AssCaptionRenderer
  {
    public const int OUTPUT_WIDTH = 1080;
    public const int OUTPUT_HEIGHT = 1920;

    internal sealed class CaptionStyle
    {
      public string FontName { get; set; } = "BowlbyOne-Regular";
      public int FontSize { get; set; } = 72;

      // ASS is AABBGGRR
      public string PrimaryColorAss { get; set; } = "&H008FFF34&";
      public string SecondaryColorAss { get; set; } = "&H0000FFFF&";
      public string OutlineColorAss { get; set; } = "&H00000000&";
      public string BackColorAss { get; set; } = "&H00000000&";

      public int Outline { get; set; } = 8;
      public int Shadow { get; set; } = 3;

      // Optional extra global offset via env var (applied in GenerateAss)
      public double TimeOffsetSeconds { get; set; } =
        double.TryParse(Environment.GetEnvironmentVariable("NIGHTSHIFT_CAPTION_OFFSET"), out var v) ? v : 0.0;

      public double MinGapSeconds { get; set; } = 0.02;
      public double EndHoldSeconds { get; set; } = 0.05;

      public string Preset { get; set; } = "capcut_green";
    }

    internal sealed record WordTiming(string Word, double Start, double End);
    internal sealed record CaptionChunk(WordTiming Word, double Start, double End);

    public static List<WordTiming> ParseWordsFromTimestampsJson(string timestampsJson)
    {
      var root = JObject.Parse(timestampsJson);
      var align = root["alignment"] as JObject ?? new JObject();

      var chars = align["characters"]?.ToObject<List<string>>() ?? new List<string>();
      var starts = align["character_start_times_seconds"]?.ToObject<List<double>>() ?? new List<double>();
      var ends = align["character_end_times_seconds"]?.ToObject<List<double>>() ?? new List<double>();

      if (chars.Count == 0 || chars.Count != starts.Count || chars.Count != ends.Count)
        throw new InvalidOperationException("Invalid alignment JSON: missing or mismatched arrays.");

      var punct = new HashSet<char>(new[] { '.', ',', '!', '?', ';', ':', '…' });

      var words = new List<WordTiming>();
      var current = new StringBuilder();
      double? wordStart = null;
      double? lastEnd = null;

      void Flush()
      {
        if (current.Length == 0 || wordStart is null || lastEnd is null)
        {
          current.Clear();
          wordStart = null;
          lastEnd = null;
          return;
        }

        var token = current.ToString().Trim();
        if (!string.IsNullOrWhiteSpace(token))
          words.Add(new WordTiming(token, wordStart.Value, lastEnd.Value));

        current.Clear();
        wordStart = null;
        lastEnd = null;
      }

      for (int i = 0; i < chars.Count; i++)
      {
        var chStr = chars[i];
        var st = starts[i];
        var et = ends[i];

        var ch = chStr.Length > 0 ? chStr[0] : '\0';

        if (char.IsWhiteSpace(ch))
        {
          Flush();
          continue;
        }

        if (wordStart is null) wordStart = st;

        current.Append(ch);
        lastEnd = et;

        if (punct.Contains(ch))
          Flush();
      }

      Flush();

      // Merge punctuation-only tokens into previous token
      var merged = new List<WordTiming>();
      bool IsPunctOnly(string s) => s.All(c => punct.Contains(c));

      foreach (var w in words)
      {
        if (merged.Count > 0 && IsPunctOnly(w.Word))
        {
          var prev = merged[^1];
          merged[^1] = new WordTiming(prev.Word + w.Word, prev.Start, w.End);
        }
        else
        {
          merged.Add(w);
        }
      }

      return merged;
    }

    public static List<CaptionChunk> ChunkOneWord(List<WordTiming> words, CaptionStyle style, double timeOffsetSeconds)
    {
      var chunks = new List<CaptionChunk>();

      for (int i = 0; i < words.Count; i++)
      {
        var w = words[i];
        double start = w.Start + timeOffsetSeconds;
        double end = w.End + timeOffsetSeconds;

        if (i < words.Count - 1)
        {
          var nextStart = words[i + 1].Start + timeOffsetSeconds;
          end = Math.Min(end + style.EndHoldSeconds, Math.Max(end, nextStart - style.MinGapSeconds));
        }
        else
        {
          end = end + style.EndHoldSeconds;
        }

        chunks.Add(new CaptionChunk(w, start, end));
      }

      // Clamp overlaps
      for (int i = 1; i < chunks.Count; i++)
      {
        var prev = chunks[i - 1];
        var cur = chunks[i];
        if (cur.Start < prev.End + style.MinGapSeconds)
        {
          var newStart = prev.End + style.MinGapSeconds;
          var newEnd = Math.Max(cur.End, newStart + 0.06);
          chunks[i] = new CaptionChunk(cur.Word, newStart, newEnd);
        }
      }

      return chunks;
    }

    public static string GenerateAss(List<CaptionChunk> chunks, CaptionStyle s)
    {
      var sb = new StringBuilder();

      sb.AppendLine("[Script Info]");
      sb.AppendLine("ScriptType: v4.00+");
      sb.AppendLine("Collisions: Normal");
      sb.AppendLine($"PlayResX: {OUTPUT_WIDTH}");
      sb.AppendLine($"PlayResY: {OUTPUT_HEIGHT}");
      sb.AppendLine("WrapStyle: 2");
      sb.AppendLine("ScaledBorderAndShadow: yes");
      sb.AppendLine();

      sb.AppendLine("[V4+ Styles]");
      sb.AppendLine("Format: Name, Fontname, Fontsize, PrimaryColour, SecondaryColour, OutlineColour, BackColour, " +
                    "Bold, Italic, Underline, StrikeOut, ScaleX, ScaleY, Spacing, Angle, BorderStyle, Outline, Shadow, " +
                    "Alignment, MarginL, MarginR, MarginV, Encoding");
      sb.AppendLine($"Style: Default,{s.FontName},{s.FontSize},{s.PrimaryColorAss},{s.SecondaryColorAss}," +
                    $"{s.OutlineColorAss},{s.BackColorAss},-1,0,0,0,100,100,0,0,1,{s.Outline},{s.Shadow}," +
                    $"5,10,10,0,1");
      sb.AppendLine();

      sb.AppendLine("[Events]");
      sb.AppendLine("Format: Layer, Start, End, Style, Name, MarginL, MarginR, MarginV, Effect, Text");

      foreach (var c in chunks)
      {
        var start = FormatAssTime(c.Start + s.TimeOffsetSeconds);
        var end = FormatAssTime(c.End + s.TimeOffsetSeconds);

        var dur = Math.Max(0.01, c.End - c.Start);
        var cs = Math.Max(1, (int)Math.Round(dur * 100.0));

        var token = AssEscape(c.Word.Word);
        var karaoke = $"{{\\k{cs}}}{token}";

        var anim = AnimationTags(s.Preset);
        sb.AppendLine($"Dialogue: 0,{start},{end},Default,,0,0,0,,{anim}{karaoke}");
      }

      return sb.ToString();
    }

    private static string AnimationTags(string preset)
    {
      preset = (preset ?? "capcut_green").ToLowerInvariant();

      var pos = @"\an5\pos(540,620)";

      if (preset is "none" or "off")
        return "{" + pos + "}";

      if (preset is "fade")
        return @"{" + pos + @"\fad(60,80)}";

      return
        @"{" + pos +
        @"\fad(35,70)" +
        @"\t(0,80,\fscx118\fscy118)" +
        @"\t(80,150,\fscx100\fscy100)" +
        @"}";
    }

    private static string AssEscape(string s)
      => s.Replace(@"\", @"\\").Replace("{", @"\{").Replace("}", @"\}");

    private static string FormatAssTime(double t)
    {
      if (t < 0) t = 0;
      int h = (int)(t / 3600);
      int m = (int)((t % 3600) / 60);
      int s = (int)(t % 60);
      int cs = (int)Math.Round((t - Math.Floor(t)) * 100);
      if (cs >= 100) cs = 99;
      return $"{h}:{m:00}:{s:00}.{cs:00}";
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

      return double.Parse(stdout, System.Globalization.CultureInfo.InvariantCulture);
    }

    private static string EscapeFfmpegFilterPath(string path)
    {
      var p = path.Replace("\\", "/");
      p = p.Replace(":", @"\:");
      p = p.Replace("'", @"\'");
      return p;
    }

    public static void Render(
      string backgroundPath,
      string audioWavPath,
      string assPath,
      string outputPath,
      double durationSeconds,
      double startTimeSeconds,
      bool needsLoop,
      double audioDelaySeconds = 0.0)
    {
      var safeSub = EscapeFfmpegFilterPath(assPath);

      var fontsDir = (Environment.GetEnvironmentVariable("NIGHTSHIFT_FONTS_DIR") ?? "").Trim();
      var fontsPart = !string.IsNullOrWhiteSpace(fontsDir)
        ? $":fontsdir='{EscapeFfmpegFilterPath(fontsDir)}'"
        : "";

      var subtitleFilter = $"ass=filename='{safeSub}':original_size={OUTPUT_WIDTH}x{OUTPUT_HEIGHT}{fontsPart}";

      var vf =
        $"scale={OUTPUT_WIDTH}:{OUTPUT_HEIGHT}:force_original_aspect_ratio=increase," +
        $"crop={OUTPUT_WIDTH}:{OUTPUT_HEIGHT}," +
        $"{subtitleFilter}";

      var delayMs = Math.Max(0, (int)Math.Round(audioDelaySeconds * 1000.0));
      var totalDuration = durationSeconds + audioDelaySeconds;

      var encEnv = (Environment.GetEnvironmentVariable("NIGHTSHIFT_VIDEO_ENCODER") ?? "").Trim();
      var encoders = new List<string>();
      if (!string.IsNullOrWhiteSpace(encEnv)) encoders.Add(encEnv);
      encoders.AddRange(new[] { "h264_v4l2m2m", "h264_omx", "libx264" });

      Exception? last = null;

      foreach (var enc in encoders.Where(e => !string.IsNullOrWhiteSpace(e)))
      {
        var psi = new ProcessStartInfo
        {
          FileName = "ffmpeg",
          RedirectStandardOutput = true,
          RedirectStandardError = true,
          UseShellExecute = false
        };

        psi.ArgumentList.Add("-y");

        if (needsLoop)
        {
          psi.ArgumentList.Add("-stream_loop");
          psi.ArgumentList.Add("-1");
        }

        psi.ArgumentList.Add("-ss");
        psi.ArgumentList.Add(startTimeSeconds.ToString("0.000", System.Globalization.CultureInfo.InvariantCulture));
        psi.ArgumentList.Add("-i");
        psi.ArgumentList.Add(backgroundPath);

        psi.ArgumentList.Add("-i");
        psi.ArgumentList.Add(audioWavPath);

        if (delayMs > 0)
        {
          // Lead-in delay: push audio later while video begins immediately.
          psi.ArgumentList.Add("-af");
          psi.ArgumentList.Add($"adelay={delayMs}|{delayMs}");
        }

        // Total video duration includes lead-in so we also get a natural trailing hold.
        psi.ArgumentList.Add("-t");
        psi.ArgumentList.Add(totalDuration.ToString("0.000", System.Globalization.CultureInfo.InvariantCulture));

        psi.ArgumentList.Add("-vf");
        psi.ArgumentList.Add(vf);

        psi.ArgumentList.Add("-map");
        psi.ArgumentList.Add("0:v:0");
        psi.ArgumentList.Add("-map");
        psi.ArgumentList.Add("1:a:0");

        psi.ArgumentList.Add("-c:v");
        psi.ArgumentList.Add(enc);
        psi.ArgumentList.Add("-pix_fmt");
        psi.ArgumentList.Add("yuv420p");
        psi.ArgumentList.Add("-r");
        psi.ArgumentList.Add("30");
        psi.ArgumentList.Add("-c:a");
        psi.ArgumentList.Add("aac");
        psi.ArgumentList.Add("-b:a");
        psi.ArgumentList.Add("192k");
        psi.ArgumentList.Add("-shortest");
        psi.ArgumentList.Add(outputPath);

        try
        {
          using var p = Process.Start(psi) ?? throw new Exception("Failed to start ffmpeg");
          var stderr = p.StandardError.ReadToEnd();
          p.WaitForExit();

          if (p.ExitCode != 0)
            throw new Exception(stderr);

          return;
        }
        catch (Exception ex)
        {
          last = ex;
        }
      }

      throw new Exception($"FFmpeg failed with all encoders tried. Last error:\n{last}");
    }
  }
}