using UnityEngine;
using Unity.MLAgents.Sensors;
using R3;

public class MeleeBossAgent : BaseMeleeAgent
{
    private float _originalAttackCooldown;
    private float _originalHeavyCooldown;

    protected override void Awake()
    {
        base.Awake();
        _originalAttackCooldown = AttackCooldown;
        _originalHeavyCooldown = HeavyAttackCooldown;

        if (MyHealth != null)
        {
            MyHealth.OnDamageTaken.Subscribe(CheckPhaseTransition).RegisterTo(destroyCancellationToken);
        }
    }

    public override void OnEpisodeBegin() 
    { 
        base.OnEpisodeBegin(); 
        _viewModel.IsPhase2.Value = false; 
        AttackCooldown = _originalAttackCooldown;
        HeavyAttackCooldown = _originalHeavyCooldown;
    }

    private void CheckPhaseTransition(float damage)
    {
        if (!_viewModel.IsPhase2.Value && (MyHealth.CurrentHP.CurrentValue / MyHealth.MaxHP) <= 0.5f) 
        { 
            _viewModel.IsPhase2.Value = true; 
            AttackCooldown = _originalAttackCooldown * 0.7f;
            HeavyAttackCooldown = _originalHeavyCooldown * 0.7f;
        }
    }

    // 💡 슈퍼아머 로직 적용 완료
    public override bool ReceiveAttack(float rawDamage, Vector3 attackSourcePos)
    {
        if (_viewModel.CurrentState.Value == FighterState.Dead) return false;

        float finalDamage = _viewModel.IsPhase2.Value ? rawDamage * 0.5f : rawDamage;
        Vector3 incomingDir = (attackSourcePos - transform.position).normalized;
        incomingDir.y = 0f;
        float dotProduct = Vector3.Dot(transform.forward, incomingDir);

        bool isValidBlock = _viewModel.IsBlocking.Value && (dotProduct > 0.5f);

        if (isValidBlock)
        {
            MyHealth.TakeDamage(finalDamage * BlockDamageReduction);
            _viewModel.AddReward(0.1f);
            return true;
        }
        else
        {
            MyHealth.TakeDamage(finalDamage);
            _viewModel.AddReward(-1.0f);
            return false; // 경직(Stun) 호출 안 함
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
}