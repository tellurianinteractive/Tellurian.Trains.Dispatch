using Tellurian.Trains.Dispatch.Brokers;
using Tellurian.Trains.Dispatch.Trains;
using Tellurian.Trains.Dispatch.Utilities;

namespace Tellurian.Trains.Dispatch;

public static class ActionContextExtensions
{
    extension(ActionContext context)
    {
        /// <summary>
        /// Executes the action represented by this context, updating the appropriate state.
        /// </summary>
        /// <returns>An Option indicating success or failure with error message.</returns>
        public Option<ActionContext> Execute()
        {
            return context.Action switch
            {
                // Dispatch actions - change TrainSection.State
                DispatchAction.Request => context.ExecuteDispatchAction(DispatchState.Requested),
                DispatchAction.Accept => context.ExecuteDispatchAction(DispatchState.Accepted),
                DispatchAction.Reject => context.ExecuteDispatchAction(DispatchState.Rejected),
                DispatchAction.Revoke => context.ExecuteDispatchAction(DispatchState.Revoked),
                DispatchAction.Depart => context.ExecuteDepart(),
                DispatchAction.Pass => context.ExecutePass(),
                DispatchAction.Arrive => context.ExecuteDispatchAction(DispatchState.Arrived),
                DispatchAction.Clear => context.ExecuteDispatchAction(DispatchState.Canceled),

                // Train actions - change Train.State
                DispatchAction.Manned => context.ExecuteTrainAction(TrainState.Manned),
                DispatchAction.Running => context.ExecuteTrainAction(TrainState.Running),
                DispatchAction.Canceled => context.ExecuteTrainAction(TrainState.Canceled),
                DispatchAction.Aborted => context.ExecuteTrainAction(TrainState.Aborted),
                DispatchAction.Completed => context.ExecuteTrainAction(TrainState.Completed),
                DispatchAction.UndoTrainState => context.ExecuteUndoTrainState(),

                _ => Option<ActionContext>.Fail($"Unknown action: {context.Action}")
            };
        }

        /// <summary>
        /// Executes the action and records the state change to the provided state providers.
        /// </summary>
        /// <param name="stateProvider">The composite state provider to record changes to.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>An Option indicating success or failure with error message.</returns>
        public async Task<Option<ActionContext>> ExecuteAndRecordAsync(
            ICompositeStateProvider? stateProvider,
            CancellationToken cancellationToken = default)
        {
            var result = context.Execute();

            if (!result.HasValue || stateProvider is null)
                return result;

            // Record the state change based on action type
            await context.RecordStateChangeAsync(stateProvider, cancellationToken).ConfigureAwait(false);

            return result;
        }

        private async Task RecordStateChangeAsync(ICompositeStateProvider stateProvider, CancellationToken cancellationToken)
        {
            var train = context.Section.Departure.Train;
            var section = context.Section;

            switch (context.Action)
            {
                // Dispatch actions - record to DispatchStateProvider
                case DispatchAction.Request:
                    await stateProvider.DispatchStateProvider.RecordDispatchStateChangeAsync(
                        section.Id, DispatchState.Requested, cancellationToken: cancellationToken).ConfigureAwait(false);
                    break;

                case DispatchAction.Accept:
                    await stateProvider.DispatchStateProvider.RecordDispatchStateChangeAsync(
                        section.Id, DispatchState.Accepted, cancellationToken: cancellationToken).ConfigureAwait(false);
                    break;

                case DispatchAction.Reject:
                    await stateProvider.DispatchStateProvider.RecordDispatchStateChangeAsync(
                        section.Id, DispatchState.Rejected, cancellationToken: cancellationToken).ConfigureAwait(false);
                    break;

                case DispatchAction.Revoke:
                    await stateProvider.DispatchStateProvider.RecordDispatchStateChangeAsync(
                        section.Id, DispatchState.Revoked, cancellationToken: cancellationToken).ConfigureAwait(false);
                    break;

                case DispatchAction.Depart:
                    await stateProvider.DispatchStateProvider.RecordDispatchStateChangeAsync(
                        section.Id, DispatchState.Departed, trackStretchIndex: 0, cancellationToken: cancellationToken).ConfigureAwait(false);
                    // Also record implicit train state change to Running if it changed
                    if (train.State == TrainState.Running && train.PreviousState == TrainState.Manned)
                    {
                        await stateProvider.TrainStateProvider.RecordTrainStateChangeAsync(
                            train.Id, TrainState.Running, cancellationToken).ConfigureAwait(false);
                    }
                    break;

                case DispatchAction.Pass when context.PassTarget is not null:
                    await stateProvider.DispatchStateProvider.RecordPassAsync(
                        section.Id, context.PassTarget.Id, section.CurrentTrackStretchIndex, cancellationToken).ConfigureAwait(false);
                    break;

                case DispatchAction.Arrive:
                    await stateProvider.DispatchStateProvider.RecordDispatchStateChangeAsync(
                        section.Id, DispatchState.Arrived, cancellationToken: cancellationToken).ConfigureAwait(false);
                    break;

                case DispatchAction.Clear:
                    await stateProvider.DispatchStateProvider.RecordDispatchStateChangeAsync(
                        section.Id, DispatchState.Canceled, cancellationToken: cancellationToken).ConfigureAwait(false);
                    break;

                // Train actions - record to TrainStateProvider
                case DispatchAction.Manned:
                    await stateProvider.TrainStateProvider.RecordTrainStateChangeAsync(
                        train.Id, TrainState.Manned, cancellationToken).ConfigureAwait(false);
                    break;

                case DispatchAction.Running:
                    await stateProvider.TrainStateProvider.RecordTrainStateChangeAsync(
                        train.Id, TrainState.Running, cancellationToken).ConfigureAwait(false);
                    break;

                case DispatchAction.Canceled:
                    await stateProvider.TrainStateProvider.RecordTrainStateChangeAsync(
                        train.Id, TrainState.Canceled, cancellationToken).ConfigureAwait(false);
                    break;

                case DispatchAction.Aborted:
                    await stateProvider.TrainStateProvider.RecordTrainStateChangeAsync(
                        train.Id, TrainState.Aborted, cancellationToken).ConfigureAwait(false);
                    break;

                case DispatchAction.Completed:
                    await stateProvider.TrainStateProvider.RecordTrainStateChangeAsync(
                        train.Id, TrainState.Completed, cancellationToken).ConfigureAwait(false);
                    break;

                case DispatchAction.UndoTrainState:
                    // Record the resulting state after undo
                    await stateProvider.TrainStateProvider.RecordTrainStateChangeAsync(
                        train.Id, train.State, cancellationToken).ConfigureAwait(false);
                    break;
            }
        }

        private Option<ActionContext> ExecuteDispatchAction(DispatchState newState)
        {
            context.Section.State = newState;
            return Option<ActionContext>.Success(context);
        }

        private Option<ActionContext> ExecuteDepart()
        {
            context.Section.State = DispatchState.Departed;
            context.Section.CurrentTrackStretchIndex = 0;

            // Implicitly set train to Running when departing from Manned state
            var train = context.Section.Departure.Train;
            if (train.State == TrainState.Manned)
            {
                train.State = TrainState.Running;
            }

            return Option<ActionContext>.Success(context);
        }

        private Option<ActionContext> ExecutePass()
        {
            if (context.PassTarget is null)
                return Option<ActionContext>.Fail("Pass action requires a PassTarget");

            if (!context.Section.AdvanceToNextTrackStretch())
                return Option<ActionContext>.Fail("Cannot advance to next track stretch");

            return Option<ActionContext>.Success(context);
        }

        private Option<ActionContext> ExecuteTrainAction(TrainState newState)
        {
            context.Section.Departure.Train.State = newState;
            return Option<ActionContext>.Success(context);
        }

        private Option<ActionContext> ExecuteUndoTrainState()
        {
            var train = context.Section.Departure.Train;
            if (train.PreviousState is null)
                return Option<ActionContext>.Fail("No previous state to revert to");

            train.RevokeLastStateChange();
            return Option<ActionContext>.Success(context);
        }
    }
}
