using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Exports the procedural footstep signals and verifies their basic signal
/// properties. This is an Editor-only check; it does not claim human listening
/// validation or evaluate the final room mix.
///
/// Batch:
///   Unity -batchmode -projectPath <project> -executeMethod FrontRoomsAudioVerification.Run -quit
/// </summary>
public static class FrontRoomsAudioVerification
{
    private const int SampleRate = 44100;
    private const float SilencePeak = 0.0001f;
    private const float SilenceRms = 0.00001f;
    private const float DistinctMeanAbsDelta = 0.01f;

    [Serializable]
    private sealed class ClipReport
    {
        public string name;
        public string description;
        public int samples;
        public int sampleRate;
        public float seconds;
        public float peak;
        public float rms;
        public bool nonSilent;
        public bool noClipping;
        public string wav;
    }

    [MenuItem("FrontRooms/Audio/Verify + export footstep WAVs")]
    public static void VerifyAndExport()
    {
        Run();
    }

    public static void Run()
    {
        var output = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Verification", "audio");
        Directory.CreateDirectory(output);

        var clips = new[]
        {
            new ClipSpec("player-walk", "Player / walk: soft, short sole transient", FrontRoomsAudio.PlayerStep()),
            new ClipSpec("player-run", "Player / run: sharper and louder sole impact", FrontRoomsAudio.PlayerRunStep()),
            new ClipSpec("hunter", "Hunter: low hollow impact with metallic scrape tail", FrontRoomsAudio.HunterStep())
        };
        var reports = new List<ClipReport>();
        var samples = new List<float[]>();
        var pass = true;
        foreach (var spec in clips)
        {
            var data = ReadMono(spec.clip);
            samples.Add(data);
            var stats = Measure(data);
            var wavPath = Path.Combine(output, spec.name + ".wav");
            WriteWav(wavPath, data, SampleRate);
            var report = new ClipReport
            {
                name = spec.name,
                description = spec.description,
                samples = data.Length,
                sampleRate = SampleRate,
                seconds = data.Length / (float)SampleRate,
                peak = stats.peak,
                rms = stats.rms,
                nonSilent = stats.peak > SilencePeak && stats.rms > SilenceRms,
                noClipping = stats.peak <= 1.000001f,
                wav = wavPath
            };
            reports.Add(report);
            pass &= report.nonSilent && report.noClipping;
            UnityEngine.Object.DestroyImmediate(spec.clip);
        }

        var pairwise = new StringBuilder();
        for (var i = 0; i < samples.Count; i++)
        {
            for (var j = i + 1; j < samples.Count; j++)
            {
                var delta = MeanAbsoluteDelta(samples[i], samples[j]);
                var distinct = delta > DistinctMeanAbsDelta;
                pairwise.AppendLine(string.Format("{0} vs {1}: meanAbsDelta={2:0.000000}, distinct={3}", clips[i].name, clips[j].name, delta, distinct));
                pass &= distinct;
            }
        }

        var montage = BuildMontage(samples[0], samples[1], samples[2], SampleRate);
        var montagePath = Path.Combine(output, "footsteps-montage.wav");
        WriteWav(montagePath, montage, SampleRate);
        var json = BuildJson(reports, montagePath, pairwise.ToString(), pass);
        File.WriteAllText(Path.Combine(output, "footsteps-report.json"), json);
        File.WriteAllText(Path.Combine(output, "footsteps-report.txt"),
            (pass ? "PASS" : "FAIL") + "\n" +
            "Signals are non-silent, unclipped, and pairwise distinct at the sample level.\n" +
            "This report does not replace human listening or final mix validation.\n\n" + pairwise);
        Debug.Log("[FrontRoomsAudio] " + (pass ? "PASS" : "FAIL") + " · exported WAVs to " + output);
        if (Application.isBatchMode) EditorApplication.Exit(pass ? 0 : 1);
    }

    private sealed class ClipSpec
    {
        public readonly string name;
        public readonly string description;
        public readonly AudioClip clip;
        public ClipSpec(string name, string description, AudioClip clip) { this.name = name; this.description = description; this.clip = clip; }
    }

    private struct Stats { public float peak; public float rms; }

    private static float[] ReadMono(AudioClip clip)
    {
        var data = new float[clip.samples * clip.channels];
        clip.GetData(data, 0);
        if (clip.channels == 1) return data;
        var mono = new float[clip.samples];
        for (var i = 0; i < mono.Length; i++)
        {
            var sum = 0f;
            for (var c = 0; c < clip.channels; c++) sum += data[i * clip.channels + c];
            mono[i] = sum / clip.channels;
        }
        return mono;
    }

    private static Stats Measure(float[] data)
    {
        var peak = 0f;
        var sum = 0d;
        for (var i = 0; i < data.Length; i++)
        {
            var a = Mathf.Abs(data[i]);
            if (a > peak) peak = a;
            sum += data[i] * data[i];
        }
        return new Stats { peak = peak, rms = Mathf.Sqrt((float)(sum / Mathf.Max(1, data.Length))) };
    }

    private static float MeanAbsoluteDelta(float[] a, float[] b)
    {
        var count = Mathf.Min(a.Length, b.Length);
        var sum = 0d;
        for (var i = 0; i < count; i++) sum += Mathf.Abs(a[i] - b[i]);
        return (float)(sum / Mathf.Max(1, count));
    }

    private static float[] BuildMontage(float[] walk, float[] run, float[] hunter, int rate)
    {
        // A simple audition strip: walk / pause / run / pause / hunter, with a
        // second hunter step to expose its longer low-frequency tail.
        var pause = Mathf.RoundToInt(rate * 0.38f);
        var sequence = new[] { walk, new float[pause], walk, new float[pause], run, new float[pause], run, new float[Mathf.RoundToInt(rate * 0.52f)], hunter, new float[pause], hunter };
        var length = 0;
        foreach (var part in sequence) length += part.Length;
        var montage = new float[length];
        var offset = 0;
        foreach (var part in sequence) { Array.Copy(part, 0, montage, offset, part.Length); offset += part.Length; }
        return montage;
    }

    private static void WriteWav(string path, float[] data, int rate)
    {
        using (var stream = new FileStream(path, FileMode.Create, FileAccess.Write))
        using (var writer = new BinaryWriter(stream))
        {
            var dataBytes = data.Length * 2;
            writer.Write(Encoding.ASCII.GetBytes("RIFF"));
            writer.Write(36 + dataBytes);
            writer.Write(Encoding.ASCII.GetBytes("WAVEfmt "));
            writer.Write(16); writer.Write((short)1); writer.Write((short)1);
            writer.Write(rate); writer.Write(rate * 2); writer.Write((short)2); writer.Write((short)16);
            writer.Write(Encoding.ASCII.GetBytes("data")); writer.Write(dataBytes);
            for (var i = 0; i < data.Length; i++)
            {
                var clamped = Mathf.Clamp(data[i], -1f, 1f);
                writer.Write((short)Mathf.RoundToInt(clamped * (clamped < 0f ? 32768f : 32767f)));
            }
        }
    }

    private static string BuildJson(List<ClipReport> reports, string montagePath, string pairwise, bool pass)
    {
        var sb = new StringBuilder();
        sb.AppendLine("{");
        sb.AppendLine("  \"passed\": " + (pass ? "true" : "false") + ",");
        sb.AppendLine("  \"humanListeningValidated\": false,");
        sb.AppendLine("  \"montage\": \"" + Escape(montagePath) + "\",");
        sb.AppendLine("  \"montageSequence\": \"player walk, pause, player walk, pause, player run, pause, player run, pause, hunter, pause, hunter\",");
        sb.AppendLine("  \"clips\": [");
        for (var i = 0; i < reports.Count; i++)
        {
            var r = reports[i];
            sb.AppendLine("    {");
            sb.AppendLine("      \"name\": \"" + Escape(r.name) + "\",");
            sb.AppendLine("      \"description\": \"" + Escape(r.description) + "\",");
            sb.AppendLine("      \"samples\": " + r.samples + ", \"sampleRate\": " + r.sampleRate + ",");
            sb.AppendLine("      \"seconds\": " + r.seconds.ToString("0.000", System.Globalization.CultureInfo.InvariantCulture) + ",");
            sb.AppendLine("      \"peak\": " + r.peak.ToString("0.000000", System.Globalization.CultureInfo.InvariantCulture) + ", \"rms\": " + r.rms.ToString("0.000000", System.Globalization.CultureInfo.InvariantCulture) + ",");
            sb.AppendLine("      \"nonSilent\": " + (r.nonSilent ? "true" : "false") + ", \"noClipping\": " + (r.noClipping ? "true" : "false") + ",");
            sb.AppendLine("      \"wav\": \"" + Escape(r.wav) + "\"");
            sb.Append("    }");
            sb.AppendLine(i == reports.Count - 1 ? "" : ",");
        }
        sb.AppendLine("  ],");
        sb.AppendLine("  \"pairwise\": \"" + Escape(pairwise.Trim()) + "\"");
        sb.AppendLine("}");
        return sb.ToString();
    }

    private static string Escape(string value) => value.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\r", "").Replace("\n", "\\n");
}
