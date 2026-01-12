using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Text;

namespace NyxCEngine.Util.Audio
{
  internal static class WavWriter
  {
    public static void WritePcm16Mono(string outputPath, ReadOnlySpan<byte> pcmBytes, int sampleRateHz)
    {
      if (sampleRateHz <= 0) throw new ArgumentOutOfRangeException(nameof(sampleRateHz));
      if (pcmBytes.Length == 0) throw new ArgumentException("PCM bytes are empty.", nameof(pcmBytes));

      Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);

      const short audioFormatPcm = 1;
      const short channels = 1;
      const short bitsPerSample = 16;

      short blockAlign = (short)(channels * (bitsPerSample / 8));
      int byteRate = sampleRateHz * blockAlign;

      // RIFF chunk sizes
      int dataChunkSize = pcmBytes.Length;
      int riffChunkSize = 36 + dataChunkSize;

      using var fs = new FileStream(outputPath, FileMode.Create, FileAccess.Write, FileShare.Read);
      Span<byte> header = stackalloc byte[44];

      // "RIFF"
      header[0] = (byte)'R'; header[1] = (byte)'I'; header[2] = (byte)'F'; header[3] = (byte)'F';
      BinaryPrimitives.WriteInt32LittleEndian(header.Slice(4, 4), riffChunkSize);

      // "WAVE"
      header[8] = (byte)'W'; header[9] = (byte)'A'; header[10] = (byte)'V'; header[11] = (byte)'E';

      // "fmt "
      header[12] = (byte)'f'; header[13] = (byte)'m'; header[14] = (byte)'t'; header[15] = (byte)' ';
      BinaryPrimitives.WriteInt32LittleEndian(header.Slice(16, 4), 16); // PCM fmt chunk size

      BinaryPrimitives.WriteInt16LittleEndian(header.Slice(20, 2), audioFormatPcm);
      BinaryPrimitives.WriteInt16LittleEndian(header.Slice(22, 2), channels);
      BinaryPrimitives.WriteInt32LittleEndian(header.Slice(24, 4), sampleRateHz);
      BinaryPrimitives.WriteInt32LittleEndian(header.Slice(28, 4), byteRate);
      BinaryPrimitives.WriteInt16LittleEndian(header.Slice(32, 2), blockAlign);
      BinaryPrimitives.WriteInt16LittleEndian(header.Slice(34, 2), bitsPerSample);

      // "data"
      header[36] = (byte)'d'; header[37] = (byte)'a'; header[38] = (byte)'t'; header[39] = (byte)'a';
      BinaryPrimitives.WriteInt32LittleEndian(header.Slice(40, 4), dataChunkSize);

      fs.Write(header);
      fs.Write(pcmBytes);
      fs.Flush(true);
    }

    public static double EstimateDurationSecondsPcm16Mono(int pcmByteLength, int sampleRateHz)
    {
      // 16-bit mono => 2 bytes per sample
      return pcmByteLength / (2.0 * sampleRateHz);
    }
  }
}
