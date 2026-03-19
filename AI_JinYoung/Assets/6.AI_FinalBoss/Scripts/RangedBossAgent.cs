using UnityEngine;
using Unity.MLAgents.Sensors;
using R3; 

public class RangedBossAgent : BaseRangedAgent
{
    private float _originalAttackRange;

    protected override void Awake()
    {
        base.Awake();
        _originalAttackRange = AttackRange;

        if (MyHealth != null)
        {
            MyHealth.OnDamageTaken.Subscribe(CheckPhaseTransition).RegisterTo(destroyCancellationToken);
        }
    }

    public override void OnEpisodeBegin() 
    { 
        base.OnEpisodeBegin(); 
        _viewModel.IsPhase2.Value = false; 
        AttackRange = _originalAttackRange;
    }

    private void CheckPhaseTransition(float damage)
    {
        if (!_viewModel.IsPhase2.Value && (MyHealth.CurrentHP.CurrentValue / MyHealth.MaxHP) <= 0.5f) 
        { 
            _viewModel.IsPhase2.Value = true; 
            AttackRange = _originalAttackRange * 1.5f;
        }
    }

    protected override void AddExtraObservations(VectorSensor sensor)
    {
        sensor.AddObservation(_viewModel.IsPhase2.Value ? 1f : 0f);
        if (_viewModel.IsPhase2.Value && TargetAgent != null)
        {
            sensor.AddObservation((Time.time - TargetAgent.LastAttackTime < TargetAgent.AttackCooldown) ? 1f : 0f);
            sensor.AddObservation(TargetAgent.ViewModel.CurrentStamina.Value / TargetAgent.MaxStamina);
        }
        else 
        { 
            sensor.AddObservation(0f); sensor.AddObservation(0f); 
        }
    }

    public override bool ReceiveAttack(float rawDamage, Vector3 attackSourcePos)
    {
        float finalDamage = _viewModel.IsPhase2.Value ? rawDamage * 0.5f : rawDamage;
        return base.ReceiveAttack(finalDamage, attackSourcePos);
    }
}