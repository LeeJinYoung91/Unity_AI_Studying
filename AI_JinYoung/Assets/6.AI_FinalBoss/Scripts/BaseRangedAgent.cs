using UnityEngine;
using Cysharp.Threading.Tasks;
using System.Threading;
using System;

public abstract class BaseRangedAgent : BaseCharacterAgent
{
    [Header("Ranged Weapons")]
    [SerializeField] private ProjectilePool _projectilePool;
    [SerializeField] private Transform _firePoint;
    [SerializeField] private float _lightAttackFireDelay = 0.3f;
    [SerializeField] private float _heavyAttackFireDelay = 2f;

    private CancellationTokenSource _attackCts;
    public override CharacterClass ClassType => CharacterClass.Ranged;

    protected override void Awake() 
    { 
        base.Awake(); 
        UseRootMotion = true; 
    }

    protected override void ProcessDefenseAction(int blockAction, float distToTarget) 
    { 
        _viewModel.IsBlocking.Value = false; 
        ForceMultiplier = 40f; 
    }

    protected override void ProcessOffenseAction(int mainAction, float distToTarget, float lookAtTarget)
    {
        if (mainAction == 0) return;

        if (mainAction == 3 && Time.time - _lastRollTime > RollCooldown && _viewModel.CurrentStamina.Value >= RollCost)
        {
            float rollDirX = _currentMoveSignal.x; float rollDirZ = _currentMoveSignal.z;
            if (Mathf.Abs(rollDirX) < 0.1f && Mathf.Abs(rollDirZ) < 0.1f) rollDirZ = -1f;
            _anim.SetFloat("RollX", rollDirX); _anim.SetFloat("RollZ", rollDirZ);

            if (TargetAgent != null && TargetAgent.ViewModel.CurrentState.Value == FighterState.Attacking && distToTarget < 4.0f) _viewModel.AddReward(0.05f);
            else _viewModel.AddReward(-0.05f); 

            ChangeState(FighterState.Rolling, RollDuration, 0, RollCost);
            _lastRollTime = Time.time;
            _attackCts?.Cancel(); 
        }
        else if (mainAction == 4 && Time.time - _lastAttackTime > AttackCooldown && !_viewModel.IsBlocking.Value && _viewModel.CurrentStamina.Value >= LightAttackCost)
        {
            ChangeState(FighterState.Attacking, LightAttackDuration, 4, LightAttackCost);
            _lastAttackTime = Time.time;

            if (lookAtTarget < 0.8f || distToTarget > AttackRange) _viewModel.AddReward(-0.05f);
            if (distToTarget < 5.0f) _viewModel.AddReward(-0.1f); 

            FireProjectileAsync(_lightAttackFireDelay, 4).Forget();
        }
        else if (mainAction == 2 && Time.time - _lastHeavyAttackTime > HeavyAttackCooldown && !_viewModel.IsBlocking.Value && _viewModel.CurrentStamina.Value >= HeavyAttackCost)
        {
            ChangeState(FighterState.Attacking, HeavyAttackDuration, 2, HeavyAttackCost);
            _lastHeavyAttackTime = Time.time;
            float currentHeavyRange = AttackRange * HeavyAttackRangeMultiplier;

            if (lookAtTarget < 0.8f || distToTarget > currentHeavyRange) _viewModel.AddReward(-0.05f);
            if (distToTarget < 5.0f) _viewModel.AddReward(-0.1f);

            FireProjectileAsync(_heavyAttackFireDelay, 2).Forget();
        }
    }

    private async UniTaskVoid FireProjectileAsync(float delay, int attackType) 
    {
        _attackCts?.Cancel(); 
        _attackCts = new CancellationTokenSource();
        CancellationToken token = CancellationTokenSource.CreateLinkedTokenSource(_attackCts.Token, this.GetCancellationTokenOnDestroy()).Token;
        
        bool isCancelled = await UniTask.Delay(TimeSpan.FromSeconds(delay), ignoreTimeScale: false, PlayerLoopTiming.FixedUpdate, cancellationToken: token).SuppressCancellationThrow();
        if (isCancelled) return;
        if (_viewModel.CurrentState.Value != FighterState.Attacking || CurrentAttackType != attackType) return;
        
        FireProjectile();
    }

    protected void FireProjectile() 
    {
        if (_projectilePool != null && _firePoint != null) 
        {
            Projectile proj = _projectilePool.GetProjectile();
            proj.transform.position = _firePoint.position;
            Vector3 fireDir = transform.forward;
            proj.transform.rotation = Quaternion.LookRotation(fireDir);
            proj.Fire(this, fireDir, _projectilePool.ReleaseProjectile);
        }
    }

    protected override void ProcessMovementRewards(float distToTarget, float deltaDistance, float lookAtTarget)
    {
        if (_viewModel.CurrentState.Value != FighterState.Idle || TargetAgent == null) return;

        if (lookAtTarget > 0.9f) _viewModel.AddReward(0.0005f);
        else if (lookAtTarget < 0.5f) _viewModel.AddReward(-0.001f);

        if (distToTarget < 8.0f)
        {
            if (deltaDistance > 0.001f) _viewModel.AddReward(0.001f);
            else if (deltaDistance < -0.001f) _viewModel.AddReward(-0.002f);
        }
    }
}