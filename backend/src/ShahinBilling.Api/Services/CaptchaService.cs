using System.Collections.Concurrent;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using ShahinBilling.Api.Infrastructure;

namespace ShahinBilling.Api.Services;

/// <summary>A picture to show (Image is a data URL) and the id to send back with the answer. Answer never leaves the server.</summary>
public record CaptchaChallenge(string Id, string Image, string Answer);

/// <summary>
/// The "type the letters" check shown before login. The server draws bent, rotated letters as vector paths (there is no
/// readable text in the picture) with crossing lines and dots, and remembers the answer for 3 minutes.
/// Every picture can be tried once, right or wrong, so guessing means asking for a new picture each time (and that is rate limited).
/// Switched off with Security:RequireCaptcha=false. Memory only, so a restart simply makes open pictures expire.
/// </summary>
public class CaptchaService(IConfiguration config)
{
    // Letters and digits that are hard to mix up (no I, O, 0, 1, S, 5, B, 8, Z, 2, Q, G)
    private const string Alphabet = "ACDEFHJKLMNPRTUVWXY34679";
    private const int Length = 5;
    private static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(3);
    private const int MaxOpen = 20000;

    private readonly ConcurrentDictionary<string, (string Answer, DateTime Expires)> _open = new();

    public bool Enabled => config.GetValue("Security:RequireCaptcha", true);

    public CaptchaChallenge Create()
    {
        Sweep();
        if (_open.Count >= MaxOpen) throw new AppException("Too many requests. Please wait a moment and try again.", 429);

        var chars = new char[Length];
        for (var i = 0; i < Length; i++) chars[i] = Alphabet[RandomNumberGenerator.GetInt32(Alphabet.Length)];
        var answer = new string(chars);
        var id = Convert.ToHexString(RandomNumberGenerator.GetBytes(16));
        _open[id] = (answer, DateTime.UtcNow + Lifetime);

        var svg = Render(answer);
        return new CaptchaChallenge(id, "data:image/svg+xml;base64," + Convert.ToBase64String(Encoding.UTF8.GetBytes(svg)), answer);
    }

    /// <summary>Throws unless the answer matches the picture. The picture is used up either way.</summary>
    public void Require(string? id, string? answer)
    {
        if (!Enabled) return;
        if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(answer))
            throw new AppException("Type the letters you see in the picture.");
        if (!_open.TryRemove(id, out var entry) || entry.Expires < DateTime.UtcNow)
            throw new AppException("That picture has expired. Please type the letters from the new one.");
        var given = Encoding.UTF8.GetBytes(answer.Trim().ToUpperInvariant());
        if (!CryptographicOperations.FixedTimeEquals(given, Encoding.UTF8.GetBytes(entry.Answer)))
            throw new AppException("The letters don't match. Please try the new picture.");
    }

    private void Sweep()
    {
        if (_open.Count < 500) return;
        var now = DateTime.UtcNow;
        foreach (var kv in _open) if (kv.Value.Expires < now) _open.TryRemove(kv.Key, out _);
    }

    // ---------------------------------------------------------------- drawing

    /// <summary>Each letter as strokes on a 6 x 8 grid. Drawn as lines, so no font is needed on the server.</summary>
    private static readonly Dictionary<char, double[][][]> Glyphs = new()
    {
        ['A'] = [[[0, 8], [3, 0], [6, 8]], [[1.2, 5.2], [4.8, 5.2]]],
        ['C'] = [[[6, 1.5], [4.5, 0], [1.5, 0], [0, 1.5], [0, 6.5], [1.5, 8], [4.5, 8], [6, 6.5]]],
        ['D'] = [[[0, 0], [0, 8], [3.5, 8], [6, 6], [6, 2], [3.5, 0], [0, 0]]],
        ['E'] = [[[6, 0], [0, 0], [0, 8], [6, 8]], [[0, 4], [4.5, 4]]],
        ['F'] = [[[6, 0], [0, 0], [0, 8]], [[0, 4], [4.5, 4]]],
        ['H'] = [[[0, 0], [0, 8]], [[6, 0], [6, 8]], [[0, 4], [6, 4]]],
        ['J'] = [[[6, 0], [6, 6.5], [4.5, 8], [1.5, 8], [0, 6.5]]],
        ['K'] = [[[0, 0], [0, 8]], [[6, 0], [0, 4.5]], [[2, 3.2], [6, 8]]],
        ['L'] = [[[0, 0], [0, 8], [6, 8]]],
        ['M'] = [[[0, 8], [0, 0], [3, 5], [6, 0], [6, 8]]],
        ['N'] = [[[0, 8], [0, 0], [6, 8], [6, 0]]],
        ['P'] = [[[0, 8], [0, 0], [4.5, 0], [6, 1.5], [6, 3.5], [4.5, 5], [0, 5]]],
        ['R'] = [[[0, 8], [0, 0], [4.5, 0], [6, 1.5], [6, 3.5], [4.5, 5], [0, 5]], [[3, 5], [6, 8]]],
        ['T'] = [[[0, 0], [6, 0]], [[3, 0], [3, 8]]],
        ['U'] = [[[0, 0], [0, 6.5], [1.5, 8], [4.5, 8], [6, 6.5], [6, 0]]],
        ['V'] = [[[0, 0], [3, 8], [6, 0]]],
        ['W'] = [[[0, 0], [1.5, 8], [3, 3], [4.5, 8], [6, 0]]],
        ['X'] = [[[0, 0], [6, 8]], [[6, 0], [0, 8]]],
        ['Y'] = [[[0, 0], [3, 4.2], [6, 0]], [[3, 4.2], [3, 8]]],
        ['3'] = [[[0, 1], [1.5, 0], [4.5, 0], [6, 1.5], [6, 3], [4.5, 4], [2.5, 4]], [[4.5, 4], [6, 5], [6, 6.5], [4.5, 8], [1.5, 8], [0, 7]]],
        ['4'] = [[[4.5, 8], [4.5, 0], [0, 5.5], [6, 5.5]]],
        ['6'] = [[[5.5, 1], [4, 0], [1.5, 0], [0, 1.5], [0, 6.5], [1.5, 8], [4.5, 8], [6, 6.5], [6, 5], [4.5, 3.5], [1.5, 3.5], [0, 5]]],
        ['7'] = [[[0, 0], [6, 0], [2.5, 8]]],
        ['9'] = [[[0.5, 7], [2, 8], [4.5, 8], [6, 6.5], [6, 1.5], [4.5, 0], [1.5, 0], [0, 1.5], [0, 3], [1.5, 4.5], [4.5, 4.5], [6, 3]]],
    };

    private static readonly string[] Inks = ["#1f3a5f", "#7a2438", "#1d5a46", "#5b3a8c", "#8a4b0f", "#0f5257", "#3b3b3b"];

    private static string N(double v) => v.ToString("0.#", CultureInfo.InvariantCulture);

    private static string Render(string text)
    {
        const int W = 230, H = 72;
        var rnd = Random.Shared;
        double R(double a, double b) => a + rnd.NextDouble() * (b - a);
        var sb = new StringBuilder();
        sb.Append($"<svg xmlns='http://www.w3.org/2000/svg' width='{W}' height='{H}' viewBox='0 0 {W} {H}'>");
        sb.Append($"<rect width='{W}' height='{H}' rx='10' fill='#f3f6fb'/>");

        // soft background squiggles
        for (var i = 0; i < 4; i++)
            sb.Append($"<path d='M{N(R(0, 40))} {N(R(10, 60))} C{N(R(50, 90))} {N(R(0, 72))} {N(R(120, 170))} {N(R(0, 72))} {N(R(190, 230))} {N(R(10, 60))}' fill='none' stroke='#d5dcea' stroke-width='{N(R(5, 9))}' stroke-linecap='round'/>");

        var step = (W - 28.0) / text.Length;
        for (var i = 0; i < text.Length; i++)
        {
            var strokes = Glyphs[text[i]];
            var scale = R(3.7, 4.5);
            var rot = R(-0.38, 0.38);
            var cx = 14 + step * (i + 0.5) + R(-3, 3);
            var cy = H / 2.0 + R(-5, 5);
            var phase = R(0, 6.28);
            var amp = R(0.8, 2.2);
            var ink = Inks[rnd.Next(Inks.Length)];
            var d = new StringBuilder();
            foreach (var stroke in strokes)
            {
                for (var p = 0; p < stroke.Length; p++)
                {
                    // centre the glyph, bend it with a sine wave, turn it, move it into place, add a little shake
                    var x = (stroke[p][0] - 3) * scale;
                    var y = (stroke[p][1] - 4) * scale;
                    x += Math.Sin(y * 0.2 + phase) * amp;
                    var rx = x * Math.Cos(rot) - y * Math.Sin(rot);
                    var ry = x * Math.Sin(rot) + y * Math.Cos(rot);
                    d.Append(p == 0 ? 'M' : 'L').Append(N(cx + rx + R(-0.6, 0.6))).Append(' ').Append(N(cy + ry + R(-0.6, 0.6))).Append(' ');
                }
            }
            sb.Append($"<path d='{d}' fill='none' stroke='{ink}' stroke-width='{N(R(3.2, 4.2))}' stroke-linecap='round' stroke-linejoin='round'/>");
        }

        // lines crossing the letters, and dots
        for (var i = 0; i < 4; i++)
            sb.Append($"<path d='M{N(R(0, 30))} {N(R(5, 67))} Q{N(R(60, 170))} {N(R(0, 72))} {N(R(200, 230))} {N(R(5, 67))}' fill='none' stroke='{Inks[rnd.Next(Inks.Length)]}' stroke-width='{N(R(1.2, 1.9))}' opacity='0.6'/>");
        for (var i = 0; i < 32; i++)
            sb.Append($"<circle cx='{N(R(2, W - 2))}' cy='{N(R(2, H - 2))}' r='{N(R(0.8, 1.9))}' fill='{Inks[rnd.Next(Inks.Length)]}' opacity='0.6'/>");

        sb.Append("</svg>");
        return sb.ToString();
    }
}
