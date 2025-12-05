using Tellurian.Trains.Dispatch;
using Tellurian.Trains.Dispatch.Layout;
using Tellurian.Trains.Dispatch.Trains;

namespace Tellurian.Trains.Dispatch;

/// <summary>
/// Each <see cref="TrainSection"/> passing a <see cref="BlockSignal"/> must be confirmed as 
/// passed by the <see cref="BlockSignal.ControlledBy">controlling dispatcher.</see>.
/// </summary>
/// <param name="BlockSignal"></param>
/// <param name="TrainSection"></param>
/// <remarks>
/// A <see cref="DispatchStretch"/> can have 
/// </remarks>
public record BlockSignalPassage(BlockSignal BlockSignal, TrainSection TrainSection)
{
    public BlockSignalPassageState State { get; internal set; } = BlockSignalPassageState.Expected;
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
        public bool IsOn(TrainSection stretch) =>
            stretch.StretchDirection.IntermediateBlockSignals.Any(p => p.Id == blockSignal.Id);

    }

    extension(TrainSection trainSection)
    {
        internal IEnumerable<BlockSignalPassage> CreateBlockSignalPassages() =>
            trainSection.StretchDirection.IntermediateBlockSignals
            .Select(p => new BlockSignalPassage(p, trainSection));

        internal int NumberOfBlocks => trainSection.StretchDirection.IntermediateBlockSignals.Count + 1;

        internal int CurrentBlockIndex => trainSection.BlockSignalPassages.Count(p => p.IsPassed);


        internal bool HasPassedAllBlockSignals =>
            trainSection.BlockSignalPassages.Count == 0 ||
            trainSection.BlockSignalPassages.All(p => p.IsPassed || p.IsCanceled);

        internal BlockSignalPassage? NextBlockSignalToPass =>
            trainSection.BlockSignalPassages.FirstOrDefault(p => p.IsExpected);

        internal bool IsInLastBlock => trainSection.CurrentBlockIndex == trainSection.NumberOfBlocks - 1;
    }

    extension(BlockSignalPassage[] passages)
    {
        public int NumberOfPassages => passages.Length;
    }

    extension(BlockSignalPassage passage)
    {
        public bool IsPassed => passage.State == BlockSignalPassageState.Passed;
        public bool IsCanceled => passage.State == BlockSignalPassageState.Canceled;
        public bool IsExpected => passage.State == BlockSignalPassageState.Expected;
    }
}

