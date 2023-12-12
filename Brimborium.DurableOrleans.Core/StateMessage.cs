#if WEICHEI

namespace Orleans.DurableTask.Core;

/// <summary>
/// Deprecated Wrapper for the OrchestrationState in the Tracking Queue
/// </summary>
[Obsolete("This has been Replaced by a combination of the HistoryStateEvent and TaskMessage")]
[DataContract]
public class StateMessage {
    /// <summary>
    /// The Orchestration State
    /// </summary>
    [DataMember] public OrchestrationState State;
}
#endif