namespace Jazztures.Core.Music
{
    /// <summary>
    /// The five chord-tone slots the right hand plays (CLAUDE.md §3.1): root, 3rd, 5th,
    /// 7th, 9th. The <b>slot</b>, not the pitch — target #k keeps its
    /// <see cref="ScaleDegree"/> across every chord change so the learner builds a
    /// spatial map of scale degree, not of absolute pitch. See <see cref="ChordToneSet"/>.
    /// </summary>
    public enum ScaleDegree
    {
        Root = 0,
        Third = 1,
        Fifth = 2,
        Seventh = 3,
        Ninth = 4,
    }
}
