using UnityEngine;

public abstract class BaseMeleeAgent : BaseCharacterAgent
{
    public override CharacterClass ClassType => CharacterClass.Melee;

    protected override void ProcessOffenseAction(int mainAction, float distToTarget, float lookAtTarget)
    {
        if (mainAction == 0) return;

        if (mainAction == 1 && Time.time - _lastAttackTime > AttackCooldown && _viewModel.CurrentStamina.Value >= LightAttackCost)
        {
            _lastAttackTime = Time.time;
            ChangeState(FighterState.Attacking, LightAttackDuration, 1, LightAttackCost);
            if (distToTarget > AttackRange || lookAtTarget <= 0.5f) _viewModel.AddReward(-0.1f);
        }
        else if (mainAction == 2 && Time.time - _lastHeavyAttackTime > HeavyAttackCooldown && _viewModel.CurrentStamina.Value >= HeavyAttackCost)
        {
            _lastHeavyAttackTime = Time.time;
            ChangeState(FighterState.Attacking, HeavyAttackDuration, 2, HeavyAttackCost);
            if (distToTarget > AttackRange * HeavyAttackRangeMultiplier || lookAtTarget <= 0.5f) _viewModel.AddReward(-0.1f);
        }
        else if (mainAction == 3 && Time.time - _lastRollTime > RollCooldown && _viewModel.CurrentStamina.Value >= RollCost)
        {
            ChangeState(FighterState.Rolling, RollDuration, 3, RollCost);
            if (TargetAgent != null && TargetAgent.ViewModel.CurrentState.Value == FighterState.Attacking && distToTarget < 4.0f)
            {
                _viewModel.AddReward(0.05f);
            }
        }
    }

    protected override void ProcessMovementRewards(float distToTarget, float deltaDistance, float lookAtTarget)
    {
        if (_viewModel.CurrentState.Value != FighterState.Idle || TargetAgent == null) return;

        if (lookAtTarget > 0.9f) _viewModel.AddReward(0.0005f);
        else if (lookAtTarget < 0.5f) _viewModel.AddReward(-0.001f);

        if (TargetAgent.ClassType == CharacterClass.Ranged)
        {
            if (deltaDistance < -0.001f) _viewModel.AddReward(0.001f);
            else if (deltaDistance > 0.001f) _viewModel.AddReward(-0.002f);
        }
    }
}