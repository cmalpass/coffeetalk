namespace CoffeeTalk.Models;

public enum ConversationTerminationReason
{
    Unknown,
    ConsensusReached,
    UserStopped,
    Cancelled,
    TurnBudgetExhausted,
    FailureBudgetExhausted,
    ConsensusBudgetExhausted,
    NoPersonas
}
