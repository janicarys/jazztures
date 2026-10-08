namespace Jazztures.Core.Timing
{
    /// <summary>Which click a <see cref="MetronomeClick"/> is, so the audio side can voice it differently.</summary>
    public enum MetronomeClickKind
    {
        /// <summary>Beat 1 of the bar.</summary>
        Downbeat = 0,

        /// <summary>Any other quarter-note beat.</summary>
        Beat = 1,

        /// <summary>A subdivision click between beats (<see cref="MetronomeSubdivision"/>).</summary>
        OffBeat = 2,
    }
}
