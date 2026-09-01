using Jazztures.Core.Music;

namespace Jazztures.Core.Sampling
{
    /// <summary>
    /// Parses Salamander Grand sample file names of the form <c>&lt;pitch&gt;v&lt;L|H&gt;</c>
    /// — e.g. <c>"D#4vH"</c>, <c>"A0vL.wav"</c> — into a <see cref="Pitch"/> and a
    /// <see cref="VelocityLayer"/>. A file-format concern kept here, in pure code, only
    /// so it can be unit-tested without Unity; the Unity audio layer calls it while
    /// scanning the sample folder.
    /// </summary>
    public static class SampleFileName
    {
        public static bool TryParse(string? fileName, out Pitch root, out VelocityLayer layer)
        {
            root = default;
            layer = default;
            if (string.IsNullOrEmpty(fileName))
            {
                return false;
            }

            string name = fileName!;
            int dot = name.LastIndexOf('.');
            if (dot >= 0)
            {
                name = name.Substring(0, dot);
            }

            if (name.Length < 3)
            {
                return false;
            }

            char layerChar = name[name.Length - 1];
            if (name[name.Length - 2] != 'v')
            {
                return false;
            }

            switch (layerChar)
            {
                case 'L':
                case 'l':
                    layer = VelocityLayer.Soft;
                    break;
                case 'H':
                case 'h':
                    layer = VelocityLayer.Hard;
                    break;
                default:
                    return false;
            }

            string pitchToken = name.Substring(0, name.Length - 2);
            return Pitch.TryParse(pitchToken, out root);
        }
    }
}
