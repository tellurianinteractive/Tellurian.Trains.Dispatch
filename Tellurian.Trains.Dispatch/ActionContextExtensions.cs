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

                _ => Option<ActionContext>.Fail($"Unknown action: {context.Action}")
            };
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
    }
}
