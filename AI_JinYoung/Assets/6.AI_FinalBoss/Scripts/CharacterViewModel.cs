using System;
using R3;

public class CharacterViewModel : IDisposable
{
    public ReactiveProperty<FighterState> CurrentState { get; } = new(FighterState.Idle);
    public ReactiveProperty<float> CurrentStamina { get; } = new(100f);
    public ReactiveProperty<bool> IsBlocking { get; } = new(false);
    public ReactiveProperty<float> CurrentHP { get; } = new(100f);
    public ReactiveProperty<bool> IsPhase2 { get; } = new(false);

    public Subject<float> RewardStream { get; } = new();

    public void AddReward(float reward)
    {
        RewardStream.OnNext(reward);
    }

    public void Dispose()
    {
        CurrentState.Dispose();
        CurrentStamina.Dispose();
        IsBlocking.Dispose();
        CurrentHP.Dispose();
        IsPhase2.Dispose();
        RewardStream.Dispose();
    }
}