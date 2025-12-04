using Tellurian.Trains.Dispatch.Utilities;

namespace Tellurian.Trains.Dispatch.Trains;
/// <summary>
/// A <see cref="BlockSignal"/> is a way to divide a <see cref="DispatchStretch"/> into
/// sections, where each section can have one <see cref="TrainStretch">train</see>.
/// </summary>
/// <param name="Name">Name of place associated with the <see cref="BlockSignal"/></param>
/// <param name="ControlledBy">The <see cref="IDispatcher"/> that controls this <see cref="BlockSignal"/></param>
/// <param name="IsJunction">True if there is at least one diverging track.</param>
/// <param name="SequenceNumber"></param>
/// <remarks>
/// A <see cref="BlockSignal"/> is assumed to exist in both directions on a <see cref="DispatchStretch"/>.
/// A <see cref="DispatchStretch"/> can have several <see cref="BlockSignal">block signals</see>
/// and they are in the order of the <see cref="SequenceNumber"/>
/// and a <see cref="TrainStretch"/> must be passed in that order.
/// </remarks>
public record BlockSignal(string Name, IDispatcher ControlledBy, bool IsJunction = false)
{
    public int Id { get; set { field = value.OrNextId; } }
    public override string ToString() =>
        IsJunction ? $"{Name}, junction controlled by {ControlledBy.Name}" : $"{Name} controlled by {ControlledBy.Name}";
}
/// <summary>
/// Each <see cref="TrainStretch"/> passing a <see cref="BlockSignal"/> must be confirmed as 
/// passed by the <see cref="BlockSignal.ControlledBy">controlling dispatcher.</see>.
/// </summary>
/// <param name="BlockSignal"></param>
/// <param name="TrainStretch"></param>
/// <remarks>
/// A <see cref="DispatchStretch"/> can have 
/// </remarks>
public record BlockSignalPassage(BlockSignal BlockSignal, TrainStretch TrainStretch)
{
    public BlockPassageState State { get; internal set; } = BlockPassageState.Expected;
}

public enum BlockPassageState
{
    Expected,
    Canceled,
    Passed,
}



internal static class BlockSignalPassageExtensions
{
    extension(IEnumerable<BlockSignalPassage> passages)
    {
        public bool AllCompleted => passages.All(p => p.IsPassed || p.IsCanceled);

        public BlockSignalPassage[] Next => [.. passages.Where(p => p.IsExpected)];

    }

    extension(BlockSignal blockSignal)
    {
        public bool IsOn(TrainStretch stretch) =>
            stretch.DispatchStretch.IntermediateBlockSignals.Any(p => p.Id == blockSignal.Id);

    }

    extension(TrainStretch trainStretch)
    {
        internal IEnumerable<BlockSignalPassage> CreateBlockSignalPassages() =>
            trainStretch.DispatchStretch.IntermediateBlockSignals
            .Select(p => new BlockSignalPassage(p, trainStretch));

        internal int NumberOfBlocks => trainStretch.DispatchStretch.IntermediateBlockSignals.Count + 1;

        internal int CurrentBlockIndex => trainStretch.BlockSignalPassages.Count(p => p.IsPassed);


        internal bool HasPassedAllBlockSignals =>
            trainStretch.BlockSignalPassages.Count == 0 ||
            trainStretch.BlockSignalPassages.All(p => p.IsPassed || p.IsCanceled);

        internal BlockSignalPassage? NextBlockSignalToPass =>
            trainStretch.BlockSignalPassages.FirstOrDefault(p => p.IsExpected);

        internal bool IsInLastBlock => trainStretch.CurrentBlockIndex == trainStretch.NumberOfBlocks - 1;
    }

    extension(BlockSignalPassage[] passages)
    {
        public int NumberOfPassages => passages.Length;
    }

    extension(BlockSignalPassage passage)
    {
        public bool IsPassed => passage.State == BlockPassageState.Passed;
        public bool IsCanceled => passage.State == BlockPassageState.Canceled;
        public bool IsExpected => passage.State == BlockPassageState.Expected;
    }
}
