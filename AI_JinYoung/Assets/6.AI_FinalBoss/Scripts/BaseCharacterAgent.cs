using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Sensors;
using Unity.MLAgents.Actuators;
using R3;
using Cysharp.Threading.Tasks;
using System.Threading;
using VContainer;
using System;

[RequireComponent(typeof(Rigidbody), typeof(Animator))]
public abstract class BaseCharacterAgent : Agent
{
    [Header("Targets")]
    [SerializeField] private Transform _targetTransform;
    [SerializeField] private BaseCharacterAgent _targetAgent;
    [SerializeField] private HealthSystem _myHealth;
    [SerializeField] private HealthSystem _targetHealth;

    [Header("Durations & Cooldowns")]
    [SerializeField] private float _attackCooldown = 2.0f;
    [SerializeField] private float _heavyAttackCooldown = 5.0f;
    [SerializeField] private float _lightAttackDuration = 1.7f;
    [SerializeField] private float _heavyAttackDuration = 3.5f;
    [SerializeField] private float _rollDuration = 1.3f;
    [SerializeField] private float _vulnerableDuration = 0.5f;
    [SerializeField] private float _stunDuration = 0.5f;
    [SerializeField] private float _deathDuration = 2.0f;
    [SerializeField] private float _rollCooldown = 2.0f;

    [Header("Combat Settings")]
    [SerializeField] private float _attackRange = 1.3f;
    [SerializeField] private float _heavyAttackRangeMultiplier = 2.5f;
    [SerializeField, Range(0f, 1f)] private float _blockDamageReduction = 0.2f;

    [Header("Movement & Physics")]
    [SerializeField] private bool _useRootMotion = false;
    [SerializeField] private float _forceMultiplier = 40f;
    [SerializeField] private float _turnSpeed = 150f;

    [Header("Stamina System")]
    [SerializeField] private float _maxStamina = 100f;
    [SerializeField] private float _staminaRegenRate = 15f;
    [SerializeField] private float _lightAttackCost = 25f;
    [SerializeField] private float _heavyAttackCost = 40f;
    [SerializeField] private float _rollCost = 30f;
    [SerializeField] private float _blockStaminaRegen = 2f;

    [Header("Debug")]
    [SerializeField] private float _currentHPDebug;

    protected CharacterViewModel _viewModel;
    [Inject]
    public void Construct(CharacterViewModel viewModel)
    {
        _viewModel = viewModel;
    }

    public CharacterViewModel ViewModel => _viewModel;
    public abstract CharacterClass ClassType { get; }
    public int CurrentAttackType { get; private set; } = 0;

    protected Rigidbody _rBody;
    protected Animator _anim;
    protected Vector3 _currentMoveSignal = Vector3.zero;
    protected float _currentTurnSignal = 0f;
    protected float _lastDistance = 0f;
    protected float _stateTimer = 0f;
    protected float _lastAttackTime = -10f;
    protected float _lastHeavyAttackTime = -10f;
    protected float _lastRollTime = -10f;
    protected float _defaultLinearDamping;

    public float AttackCooldown { get => _attackCooldown; protected set => _attackCooldown = value; }
    public float HeavyAttackCooldown { get => _heavyAttackCooldown; protected set => _heavyAttackCooldown = value; }
    public float LastRollTime { get => _lastRollTime; protected set => _lastRollTime = value; }
    public float LastAttackTime { get => _lastAttackTime; protected set => _lastAttackTime = value; }
    public float MaxStamina => _maxStamina;
    protected float AttackRange { get => _attackRange; set => _attackRange = value; }
    protected float HeavyAttackRangeMultiplier => _heavyAttackRangeMultiplier;
    protected float LightAttackCost => _lightAttackCost;
    protected float HeavyAttackCost => _heavyAttackCost;
    protected float LightAttackDuration => _lightAttackDuration;
    protected float HeavyAttackDuration => _heavyAttackDuration;
    protected float RollDuration => _rollDuration;
    protected float RollCost => _rollCost;
    protected float RollCooldown => _rollCooldown;
    protected float ForceMultiplier { get => _forceMultiplier; set => _forceMultiplier = value; }
    protected bool UseRootMotion { get => _useRootMotion; set => _useRootMotion = value; }
    protected BaseCharacterAgent TargetAgent => _targetAgent;
    protected HealthSystem MyHealth => _myHealth;
    protected float BlockDamageReduction => _blockDamageReduction;

    private const int STATE_COUNT = 6;
    private const float SPEED_PENALTY_MULTIPLIER = 0.5f;

    protected override void Awake()
    {
        base.Awake();
        _rBody = GetComponent<Rigidbody>();
        _anim = GetComponent<Animator>();
        _defaultLinearDamping = _rBody.linearDamping;

        if (_targetTransform != null && _targetAgent == null)
            _targetAgent = _targetTransform.GetComponent<BaseCharacterAgent>();

        if (_myHealth != null)
        {
            _myHealth.OnDeath.Subscribe(_ => HandleDeath()).RegisterTo(destroyCancellationToken);
            _myHealth.CurrentHP.Subscribe(hp => _currentHPDebug = hp).RegisterTo(destroyCancellationToken);
        }

        _viewModel ??= new CharacterViewModel();
        _viewModel.RewardStream.Subscribe(AddReward).RegisterTo(destroyCancellationToken);
    }

    public override void OnEpisodeBegin()
    {
        _rBody.linearVelocity = Vector3.zero;
        _rBody.angularVelocity = Vector3.zero;
        _myHealth.ResetHP();
        if (_targetHealth != null) _targetHealth.ResetHP();

        _viewModel.CurrentState.Value = FighterState.Idle;
        _viewModel.IsBlocking.Value = false;
        _viewModel.CurrentStamina.Value = _maxStamina;
        CurrentAttackType = 0;
        _stateTimer = 0f;

        if (_anim != null) { _anim.Rebind(); _anim.Update(0f); }

        Vector3 lookDir = _targetTransform.localPosition - transform.localPosition;
        lookDir.y = 0f;
        _lastDistance = Vector3.Distance(transform.localPosition, _targetTransform.localPosition);
        if (lookDir != Vector3.zero) transform.rotation = Quaternion.LookRotation(lookDir);
    }

    public override void CollectObservations(VectorSensor sensor)
    {
        sensor.AddObservation(transform.localPosition);
        sensor.AddObservation(_targetTransform.localPosition);
        sensor.AddObservation(_myHealth.CurrentHP.CurrentValue / _myHealth.MaxHP);
        if (_targetHealth != null) sensor.AddObservation(_targetHealth.CurrentHP.CurrentValue / _targetHealth.MaxHP);

        float distance = Vector3.Distance(transform.localPosition, _targetTransform.localPosition);
        sensor.AddObservation(distance);

        Vector3 dirToTarget = (_targetTransform.localPosition - transform.localPosition).normalized;
        sensor.AddObservation(transform.InverseTransformDirection(dirToTarget));

        sensor.AddOneHotObservation((int)_viewModel.CurrentState.Value, STATE_COUNT);

        if (_targetAgent != null)
        {
            sensor.AddOneHotObservation((int)_targetAgent.ViewModel.CurrentState.Value, STATE_COUNT);
            sensor.AddObservation(_targetAgent.ViewModel.IsBlocking.Value ? 1f : 0f);
            sensor.AddOneHotObservation((int)_targetAgent.ClassType, 2);
        }
        else
        {
            sensor.AddOneHotObservation(0, STATE_COUNT);
            sensor.AddObservation(0f);
            sensor.AddOneHotObservation(0, 2);
        }

        sensor.AddObservation(Time.time - _lastAttackTime > _attackCooldown ? 1f : 0f);
        sensor.AddObservation(Time.time - _lastRollTime > _rollCooldown ? 1f : 0f);
        sensor.AddObservation(Time.time - _lastHeavyAttackTime > _heavyAttackCooldown ? 1f : 0f);
        sensor.AddObservation(_viewModel.IsBlocking.Value ? 1f : 0f);
        sensor.AddObservation(_viewModel.CurrentStamina.Value / _maxStamina);
        sensor.AddOneHotObservation((int)ClassType, 2);

        AddExtraObservations(sensor);
    }

    protected virtual void AddExtraObservations(VectorSensor sensor) { }

    public override void OnActionReceived(ActionBuffers actionBuffers)
    {
        if (_viewModel.CurrentState.Value == FighterState.Dead) return;

        _viewModel.AddReward(-0.0005f);

        _currentMoveSignal.x = actionBuffers.ContinuousActions[0];
        _currentMoveSignal.z = actionBuffers.ContinuousActions[1];
        _currentTurnSignal = actionBuffers.ContinuousActions[2];

        float distToTarget = Vector3.Distance(transform.localPosition, _targetTransform.localPosition);
        Vector3 toTarget = (_targetTransform.localPosition - transform.localPosition).normalized;
        float lookAtTarget = Vector3.Dot(transform.forward, toTarget);
        float deltaDistance = distToTarget - _lastDistance;

        if (_viewModel.CurrentStamina.Value <= 0f) _viewModel.AddReward(-0.002f);

        if (_viewModel.CurrentState.Value == FighterState.Idle)
        {
            int mainAction = actionBuffers.DiscreteActions[0];
            int blockAction = actionBuffers.DiscreteActions[1];

            ProcessDefenseAction(blockAction, distToTarget);
            ProcessOffenseAction(mainAction, distToTarget, lookAtTarget);
        }

        ProcessMovementRewards(distToTarget, deltaDistance, lookAtTarget);
        CheckFall();
        _lastDistance = distToTarget;
    }

    protected abstract void ProcessOffenseAction(int mainAction, float distToTarget, float lookAtTarget);
    protected virtual void ProcessMovementRewards(float distToTarget, float deltaDistance, float lookAtTarget) { }

    protected virtual void ProcessDefenseAction(int blockAction, float distToTarget)
    {
        if (blockAction == 1 && _viewModel.CurrentStamina.Value > 5f)
        {
            _viewModel.IsBlocking.Value = true;
            _anim.SetBool("IsBlocking", true);
            _forceMultiplier = 10f;
            _rBody.linearDamping = _defaultLinearDamping * 2f;
        }
        else
        {
            _viewModel.IsBlocking.Value = false;
            _anim.SetBool("IsBlocking", false);
            _forceMultiplier = 40f;
            if (_viewModel.CurrentState.Value == FighterState.Idle) _rBody.linearDamping = _defaultLinearDamping;
        }
    }

    protected void ChangeState(FighterState newState, float duration, int attackType, float staminaCost)
    {
        _viewModel.CurrentState.Value = newState;
        _stateTimer = duration;
        _viewModel.CurrentStamina.Value -= staminaCost;
        _viewModel.IsBlocking.Value = false;
        _anim.SetBool("IsBlocking", false);

        _rBody.linearDamping = _defaultLinearDamping;
        SetLayerWeightSafely(1, 0f);
        _anim.applyRootMotion = _useRootMotion;

        if (newState == FighterState.Attacking)
        {
            CurrentAttackType = attackType;
            _anim.SetTrigger((attackType == 1 || attackType == 4) ? "LightAttack" : "HeavyAttack");
        }
        else if (newState == FighterState.Rolling)
        {
            _lastRollTime = Time.time;
            _anim.SetTrigger("Roll");
        }
    }

    public virtual bool ReceiveAttack(float rawDamage, Vector3 attackSourcePos)
    {
        if (_viewModel.CurrentState.Value == FighterState.Dead) return false;

        Vector3 incomingDir = (attackSourcePos - transform.position).normalized;
        incomingDir.y = 0f;
        float dotProduct = Vector3.Dot(transform.forward, incomingDir);

        bool isValidBlock = _viewModel.IsBlocking.Value && (dotProduct > 0.5f);

        if (isValidBlock)
        {
            _myHealth.TakeDamage(rawDamage * _blockDamageReduction);
            _viewModel.AddReward(0.1f);
            return true;
        }
        else
        {
            _myHealth.TakeDamage(rawDamage);
            _viewModel.AddReward(-1.0f);
            ChangeStateToStun();
            return false;
        }
    }

    protected void ChangeStateToStun()
    {
        _viewModel.CurrentState.Value = FighterState.Stunned;
        _stateTimer = _stunDuration;
        _viewModel.IsBlocking.Value = false;
        _anim.SetBool("IsBlocking", false);
        CurrentAttackType = 0;
        _anim.SetTrigger("Hit");
        SetLayerWeightSafely(1, 0f);
        _anim.applyRootMotion = false;
        _rBody.linearDamping = 10f;
    }

    private void CheckFall()
    {
        if (transform.localPosition.y < -2f)
        {
            SetReward(-5.0f);
            if (_targetAgent != null) _targetAgent.EndEpisode();
            EndEpisode();
        }
    }

    private void HandleDeath()
    {
        if (_viewModel.CurrentState.Value == FighterState.Dead) return;
        _viewModel.CurrentState.Value = FighterState.Dead;
        _anim.SetTrigger("Die");
        _anim.applyRootMotion = true;
        _rBody.linearDamping = 10f;
        DeathSequenceAsync(this.GetCancellationTokenOnDestroy()).Forget();
    }

    private async UniTaskVoid DeathSequenceAsync(CancellationToken token)
    {
        SetReward(-2.0f);
        if (_targetAgent != null) _targetAgent.SetReward(2.0f);

        await UniTask.Delay(TimeSpan.FromSeconds(_deathDuration), ignoreTimeScale: false, PlayerLoopTiming.FixedUpdate, token);

        if (_targetAgent != null) _targetAgent.EndEpisode();
        EndEpisode();
    }

    protected virtual void FixedUpdate()
    {
        UpdateStateMachine();
        UpdateStamina();
        UpdateMovement();
    }

    private void UpdateStateMachine()
    {
        if (_viewModel.CurrentState.Value == FighterState.Idle || _viewModel.CurrentState.Value == FighterState.Dead) return;
        _stateTimer -= Time.fixedDeltaTime;
        if (_stateTimer <= 0)
        {
            if (_viewModel.CurrentState.Value == FighterState.Attacking || _viewModel.CurrentState.Value == FighterState.Rolling)
            {
                _viewModel.CurrentState.Value = FighterState.Vulnerable;
                _stateTimer = _vulnerableDuration;
                SetLayerWeightSafely(1, 1f);
                _anim.applyRootMotion = false;
                _rBody.linearDamping = 10f;
                CurrentAttackType = 0;
            }
            else if (_viewModel.CurrentState.Value == FighterState.Vulnerable || _viewModel.CurrentState.Value == FighterState.Stunned)
            {
                _viewModel.CurrentState.Value = FighterState.Idle;
                _anim.applyRootMotion = _useRootMotion;
                _rBody.linearDamping = _defaultLinearDamping;
            }
        }
    }

    private void UpdateStamina()
    {
        if (_viewModel.CurrentState.Value == FighterState.Idle)
        {
            float regenRate = _viewModel.IsBlocking.Value ? _blockStaminaRegen : _staminaRegenRate;
            _viewModel.CurrentStamina.Value = Mathf.Clamp(_viewModel.CurrentStamina.Value + regenRate * Time.fixedDeltaTime, 0f, _maxStamina);
        }
    }

    private void UpdateMovement()
    {
        if (_viewModel.CurrentState.Value != FighterState.Idle)
        {
            if (_useRootMotion) { _anim.SetFloat("MoveX", 0f); _anim.SetFloat("MoveZ", 0f); }
            else { _anim.SetFloat("MoveSpeed", 0f); }
            return;
        }

        transform.Rotate(0, _currentTurnSignal * _turnSpeed * Time.fixedDeltaTime, 0);
        float moveZ = _currentMoveSignal.z;
        if (moveZ < 0f) moveZ *= 0.6f; // 소울라이크 뒷걸음질 너프

        if (_useRootMotion)
        {
            _anim.SetFloat("MoveX", _currentMoveSignal.x, 0.1f, Time.fixedDeltaTime);
            _anim.SetFloat("MoveZ", moveZ, 0.1f, Time.fixedDeltaTime);
        }
        else
        {
            Vector3 localMove = (transform.forward * moveZ) + (transform.right * _currentMoveSignal.x);
            float speedPenalty = (_viewModel.CurrentStamina.Value <= 0f) ? SPEED_PENALTY_MULTIPLIER : 1.0f;
            _rBody.AddForce(localMove * _forceMultiplier * speedPenalty);
            _anim.SetFloat("MoveSpeed", _rBody.linearVelocity.magnitude);
        }
    }

    private void OnAnimatorMove()
    {
        if (_anim != null && _anim.applyRootMotion && _useRootMotion && _viewModel.CurrentState.Value != FighterState.Dead)
        {
            float speedMultiplier = (_viewModel.CurrentState.Value == FighterState.Idle && _viewModel.CurrentStamina.Value <= 0f) ? SPEED_PENALTY_MULTIPLIER : 1.0f;
            Vector3 newPosition = _rBody.position + (_anim.deltaPosition * speedMultiplier);
            _rBody.MovePosition(newPosition);
        }
    }

    protected void SetLayerWeightSafely(int layerIndex, float weight)
    {
        if (_anim != null && _anim.layerCount > layerIndex) _anim.SetLayerWeight(layerIndex, weight);
    }
}