using UnityEngine;

/// <summary>
/// Procedural placeholder sounds, so the prototype ships without imported audio.
/// Each clip matches a noise the hunter can hear: the louder the action, the harsher the clip.
/// </summary>
public static class FrontRoomsAudio
{
    private const int Rate = 44100;
    private static readonly System.Random Rng = new System.Random(4096);

    private static float Noise() => (float)(Rng.NextDouble() * 2.0 - 1.0);

    private static AudioClip Make(string name, float seconds, System.Func<float, float> sample)
    {
        var count = Mathf.CeilToInt(seconds * Rate);
        var data = new float[count];
        for (var i = 0; i < count; i++) data[i] = Mathf.Clamp(sample(i / (float)Rate), -1f, 1f);
        var clip = AudioClip.Create(name, count, 1, Rate, false);
        clip.SetData(data, 0);
        return clip;
    }

    /// <summary>Fluorescent mains hum, seamless 2 s loop (all partials complete whole cycles).</summary>
    public static AudioClip Hum() => Make("hum", 2f, t =>
        0.10f * Mathf.Sin(2f * Mathf.PI * 60f * t) +
        0.08f * Mathf.Sin(2f * Mathf.PI * 120f * t) +
        0.05f * Mathf.Sin(2f * Mathf.PI * 180f * t) +
        0.03f * Mathf.Sin(2f * Mathf.PI * 240f * t) * (0.6f + 0.4f * Mathf.Sin(2f * Mathf.PI * 3f * t)) +
        0.012f * Noise());

    /// <summary>
    /// Player footfall: a short, soft carpet/sole transient.  It is intentionally
    /// higher and quieter than the hunter's low, resonant step so players can
    /// identify who is moving without relying on the HUD.
    /// </summary>
    public static AudioClip PlayerStep() => Make("player-step", 0.11f, t =>
    {
        var body = 0.24f * Mathf.Sin(2f * Mathf.PI * 145f * t) * Mathf.Exp(-t * 32f);
        var texture = 0.14f * Noise() * Mathf.Exp(-t * 62f);
        return body + texture;
    });

    /// <summary>A louder sole impact for sprinting, with a slightly sharper attack.</summary>
    public static AudioClip PlayerRunStep() => Make("player-run-step", 0.095f, t =>
    {
        var body = 0.32f * Mathf.Sin(2f * Mathf.PI * 118f * t) * Mathf.Exp(-t * 28f);
        var attack = 0.22f * Noise() * Mathf.Exp(-t * 74f);
        return body + attack;
    });

    /// <summary>
    /// Hunter footfall: low, hollow impact plus a metallic scrape.  This is a
    /// separate clip (and separate spatial source) so it reads as a threat even
    /// when both characters use the same corridor material.
    /// </summary>
    public static AudioClip HunterStep() => Make("hunter-step", 0.22f, t =>
    {
        var impact = 0.62f * Mathf.Sin(2f * Mathf.PI * 58f * t) * Mathf.Exp(-t * 15f);
        var ring = 0.18f * Mathf.Sin(2f * Mathf.PI * 311f * t) * Mathf.Exp(-t * 19f);
        var scrape = 0.16f * Noise() * Mathf.Exp(-t * 11f);
        return impact + ring + scrape;
    });

    // Backwards-compatible alias for older prototype callers.
    public static AudioClip Step() => PlayerStep();

    public static AudioClip Key() => Make("key", 0.32f, t =>
        (t < 0.12f ? Mathf.Sin(2f * Mathf.PI * 1047f * t) : Mathf.Sin(2f * Mathf.PI * 1568f * t)) *
        0.35f * Mathf.Exp(-(t < 0.12f ? t : t - 0.12f) * 14f));

    public static AudioClip DoorOpen() => Make("door-open", 0.45f, t =>
        0.25f * Mathf.Sin(2f * Mathf.PI * (190f - 110f * t) * t) * Mathf.Exp(-t * 4f) +
        0.08f * Noise() * Mathf.Exp(-t * 9f));

    public static AudioClip DoorSlam() => Make("door-slam", 0.3f, t =>
        0.7f * Mathf.Sin(2f * Mathf.PI * 62f * t) * Mathf.Exp(-t * 16f) +
        0.3f * Noise() * Mathf.Exp(-t * 40f));

    public static AudioClip DoorBang() => Make("door-bang", 0.22f, t =>
        0.8f * Mathf.Sin(2f * Mathf.PI * 48f * t) * Mathf.Exp(-t * 18f) +
        0.45f * Noise() * Mathf.Exp(-t * 30f));

    public static AudioClip Glass()
    {
        var tinkles = new float[9];
        var starts = new float[9];
        for (var i = 0; i < tinkles.Length; i++)
        {
            tinkles[i] = 2200f + (float)Rng.NextDouble() * 4200f;
            starts[i] = (float)Rng.NextDouble() * 0.5f;
        }
        return Make("glass", 1.1f, t =>
        {
            var v = 0.75f * Noise() * Mathf.Exp(-t * 11f);
            for (var i = 0; i < tinkles.Length; i++)
            {
                var local = t - starts[i];
                if (local > 0f) v += 0.12f * Mathf.Sin(2f * Mathf.PI * tinkles[i] * local) * Mathf.Exp(-local * 22f);
            }
            return v;
        });
    }

    public static AudioClip Heartbeat() => Make("heartbeat", 0.6f, t =>
    {
        var a = t < 0.3f ? t : t - 0.22f;
        var env = t < 0.3f ? Mathf.Exp(-t * 20f) : 0.7f * Mathf.Exp(-(t - 0.22f) * 20f);
        return 0.9f * Mathf.Sin(2f * Mathf.PI * 52f * a) * env;
    });

    public static AudioClip Caught() => Make("caught", 1.6f, t =>
        0.45f * (Mathf.Sin(2f * Mathf.PI * 55f * t) + Mathf.Sin(2f * Mathf.PI * 58.5f * t)) * Mathf.Min(1f, t * 6f) * Mathf.Exp(-t * 1.4f) +
        0.2f * Noise() * Mathf.Exp(-t * 3f));

    public static AudioClip Escape() => Make("escape", 1.4f, t =>
        0.18f * (Mathf.Sin(2f * Mathf.PI * 523f * t) + Mathf.Sin(2f * Mathf.PI * 659f * t) + Mathf.Sin(2f * Mathf.PI * 784f * t)) *
        Mathf.Min(1f, t * 8f) * Mathf.Exp(-t * 1.8f));
}
