using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Sensors;
using Unity.MLAgents.Actuators;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(Animator))]
public class CharacterAgent : Agent
{
    private Rigidbody _rBody;
    private Animator _anim;

    [Header("Targets")]
    [SerializeField] private Transform targetTransform;
    [SerializeField] private CharacterAgent targetAgent;
    [SerializeField] private HealthSystem myHealth;
    [SerializeField] private HealthSystem targetHealth;

    [Header("State Info")]
    [SerializeField] private FighterState currentState = FighterState.Idle;
    [SerializeField] private bool isBlocking = false;
    private int _currentAttackType = 0;

    public FighterState CurrentState => currentState;
    public bool IsBlocking => isBlocking;
    public int CurrentAttackType => _currentAttackType;
    public float LastRollTime => _lastRollTime;

    private float _stateTimer = 0f;
    private float _lastAttackTime = -10f;
    private float _lastHeavyAttackTime = -10f;
    private float _lastRollTime = -10f;

    [Header("Durations & Cooldowns")]
    [SerializeField] private float lightAttackDuration = 1.7f;
    [SerializeField] private float heavyAttackDuration = 3.5f;
    [SerializeField] private float rollDuration = 1.3f;
    [SerializeField] private float vulnerableDuration = 0.5f;
    [SerializeField] private float attackCooldown = 2.0f;
    [SerializeField] private float heavyAttackCooldown = 5.0f;
    [SerializeField] private float rollCooldown = 2.0f;

    [Header("Combat Settings")]
    [SerializeField] private float attackRange = 1.3f;
    [SerializeField] private float heavyAttackRangeMultiplier = 2.5f;

    [Header("Movement & Physics")]
    [SerializeField] private float forceMultiplier = 40f;
    [SerializeField] private float turnSpeed = 150f;
    private Vector3 _currentMoveSignal = Vector3.zero;
    private float _currentTurnSignal = 0f;
    private float _lastDistance = 0f;

    [Header("Stamina System")]
    [SerializeField] private float maxStamina = 100f;
    [SerializeField] private float currentStamina = 100f;
    [SerializeField] private float staminaRegenRate = 15f;
    [SerializeField] private float lightAttackCost = 25f;
    [SerializeField] private float heavyAttackCost = 40f;
    [SerializeField] private float rollCost = 30f;
    [SerializeField] private float blockStaminaRegen = 2f;

    [Header("Spawn Settings")]
    [SerializeField] private float spawnRangeX = 8f;
    [SerializeField] private float spawnRangeZ = 5f;
    [SerializeField] private float minDistance = 2f;
    [SerializeField] private bool isMaster = false;

    private const int StateCount = 4;

    protected override void Awake()
    {
        base.Awake();
        _rBody = GetComponent<Rigidbody>();
        _anim = GetComponent<Animator>();

        if (targetTransform != null && targetAgent == null)
        {
            targetAgent = targetTransform.GetComponent<CharacterAgent>();
        }
    }

    public override void OnEpisodeBegin()
    {
        _rBody.linearVelocity = Vector3.zero;
        _rBody.angularVelocity = Vector3.zero;
        myHealth.ResetHP();
        targetHealth.ResetHP();

        currentState = FighterState.Idle;
        isBlocking = false;
        _currentAttackType = 0;
        currentStamina = maxStamina;
        _stateTimer = 0f;

        if (isMaster)
        {
            float playerX = Random.Range(-spawnRangeX, -minDistance);
            float targetX = Random.Range(minDistance, spawnRangeX);
            float playerZ = Random.Range(-spawnRangeZ, spawnRangeZ);
            float targetZ = Random.Range(-spawnRangeZ, spawnRangeZ);

            transform.localPosition = new Vector3(playerX, 0.5f, playerZ);
            targetTransform.localPosition = new Vector3(targetX, 0.5f, targetZ);
        }

        Vector3 lookDir = targetTransform.localPosition - transform.localPosition;
        lookDir.y = 0f;
        _lastDistance = Vector3.Distance(transform.localPosition, targetTransform.localPosition);

        if (lookDir != Vector3.zero)
        {
            transform.rotation = Quaternion.LookRotation(lookDir);
        }
    }

    public override void CollectObservations(VectorSensor sensor)
    {
        sensor.AddObservation(transform.localPosition);
        sensor.AddObservation(targetTransform.localPosition);
        sensor.AddObservation(myHealth.CurrentHP.CurrentValue / myHealth.MaxHP);
        sensor.AddObservation(targetHealth.CurrentHP.CurrentValue / targetHealth.MaxHP);

        float distance = Vector3.Distance(transform.localPosition, targetTransform.localPosition);
        sensor.AddObservation(distance);

        Vector3 dirToTarget = (targetTransform.localPosition - transform.localPosition).normalized;
        sensor.AddObservation(transform.InverseTransformDirection(dirToTarget));


        sensor.AddOneHotObservation((int)currentState, StateCount);

        if (targetAgent != null)
        {
            sensor.AddOneHotObservation((int)targetAgent.CurrentState, StateCount);
            sensor.AddObservation(targetAgent.IsBlocking ? 1f : 0f);
        }
        else
        {
            sensor.AddOneHotObservation(0, StateCount);
            sensor.AddObservation(0f);
        }

        sensor.AddObservation(Time.time - _lastAttackTime > attackCooldown ? 1f : 0f);
        sensor.AddObservation(Time.time - _lastRollTime > rollCooldown ? 1f : 0f);
        sensor.AddObservation(Time.time - _lastHeavyAttackTime > heavyAttackCooldown ? 1f : 0f);
        sensor.AddObservation(isBlocking ? 1f : 0f);
        sensor.AddObservation(currentStamina / maxStamina);
    }

    public override void OnActionReceived(ActionBuffers actionBuffers)
    {
        AddReward(-0.001f);

        _currentMoveSignal.x = actionBuffers.ContinuousActions[0];
        _currentMoveSignal.z = actionBuffers.ContinuousActions[1];
        _currentTurnSignal = actionBuffers.ContinuousActions[2];

        float distToTarget = Vector3.Distance(transform.localPosition, targetTransform.localPosition);
        Vector3 toTarget = (targetTransform.localPosition - transform.localPosition).normalized;
        float lookAtTarget = Vector3.Dot(transform.forward, toTarget);
        float deltaDistance = distToTarget - _lastDistance;

        if (currentStamina <= 0f) AddReward(-0.005f);

        if (currentState == FighterState.Idle)
        {
            int mainAction = actionBuffers.DiscreteActions[0];
            int blockAction = actionBuffers.DiscreteActions[1];

            ProcessDefenseAction(blockAction, distToTarget);
            ProcessOffenseAction(mainAction, distToTarget, lookAtTarget);
        }

        ProcessTensionAndDistanceRewards(distToTarget, deltaDistance);

        if (lookAtTarget > 0.8f) AddReward(0.001f);
        else if (lookAtTarget < 0.2f) AddReward(-0.002f);

        if (Vector3.Distance(new Vector3(transform.localPosition.x, 0, transform.localPosition.z), Vector3.zero) > 15.0f)
        {
            AddReward(-0.01f);
        }

        CheckDeathAndFall();

        _lastDistance = distToTarget;
    }

    private void ProcessDefenseAction(int blockAction, float distToTarget)
    {
        if (blockAction == 1 && currentStamina > 5f)
        {
            isBlocking = true;
            _anim.SetBool("IsBlocking", true);
            forceMultiplier = 15f;
            if (distToTarget > 3.0f) AddReward(-0.002f);
        }
        else
        {
            isBlocking = false;
            _anim.SetBool("IsBlocking", false);
            forceMultiplier = 40f;
        }
    }

    private void ProcessOffenseAction(int mainAction, float distToTarget, float lookAtTarget)
    {
        if (mainAction == 1 && Time.time - _lastAttackTime > attackCooldown && !isBlocking && currentStamina >= lightAttackCost)
        {
            if (distToTarget > attackRange || lookAtTarget < 0.5f) AddReward(-0.1f);
            ChangeState(FighterState.Attacking, lightAttackDuration, 1, lightAttackCost);
            _lastAttackTime = Time.time;
        }
        else if (mainAction == 2 && Time.time - _lastHeavyAttackTime > heavyAttackCooldown && !isBlocking && currentStamina >= heavyAttackCost)
        {
            if (distToTarget > attackRange * heavyAttackRangeMultiplier || lookAtTarget < 0.5f) AddReward(-0.2f);
            ChangeState(FighterState.Attacking, heavyAttackDuration, 2, heavyAttackCost);
            _lastHeavyAttackTime = Time.time;
        }
        else if (mainAction == 3 && Time.time - _lastRollTime > rollCooldown && currentStamina >= rollCost)
        {
            if (targetAgent != null && targetAgent.CurrentState == FighterState.Attacking && distToTarget < 4.0f)
            {
                AddReward(0.5f);
                currentStamina += rollCost;
            }
            else if (distToTarget > 4.0f) AddReward(-0.05f);

            ChangeState(FighterState.Rolling, rollDuration, 0, rollCost);
            _lastRollTime = Time.time;
        }
    }

    private void ProcessTensionAndDistanceRewards(float distToTarget, float deltaDistance)
    {
        if (currentState != FighterState.Idle) return;

        if (currentStamina < 40f)
        {
            if (distToTarget < 4.0f)
            {
                if (deltaDistance > 0.001f) AddReward(0.0005f);
                else AddReward(-0.001f);
            }
        }
        else
        {
            if (distToTarget <= 1.0f) AddReward(-0.001f);
            else if (distToTarget > 3.0f)
            {
                if (deltaDistance < -0.001f) AddReward(0.001f);
                else AddReward(-0.002f);
            }
        }
    }

    private void CheckDeathAndFall()
    {
        if (myHealth.IsDead.CurrentValue)
        {
            SetReward(-2.0f);
            if (targetAgent != null) { targetAgent.SetReward(1.0f); targetAgent.EndEpisode(); }
            EndEpisode();
        }
        else if (transform.localPosition.y < -2f)
        {
            SetReward(-5.0f);
            if (targetAgent != null) targetAgent.EndEpisode();
            EndEpisode();
        }
    }

    private void ChangeState(FighterState newState, float duration, int attackType, float staminaCost)
    {
        currentState = newState;
        _stateTimer = duration;
        currentStamina -= staminaCost;
        isBlocking = false;
        _anim.SetBool("IsBlocking", false);

        _rBody.linearDamping = 0f;
        _anim.SetLayerWeight(1, 0f);
        _anim.applyRootMotion = true;

        if (newState == FighterState.Attacking)
        {
            _currentAttackType = attackType;
            _anim.SetTrigger(attackType == 1 ? "LightAttack" : "HeavyAttack");
        }
        else if (newState == FighterState.Rolling)
        {
            _anim.SetTrigger("Roll");
        }
    }

    private void FixedUpdate()
    {
        UpdateStateMachine();
        UpdateStamina();
        UpdateMovement();
    }

    private void UpdateStateMachine()
    {
        if (currentState == FighterState.Idle) return;

        _stateTimer -= Time.fixedDeltaTime;
        if (_stateTimer <= 0)
        {
            if (currentState == FighterState.Attacking || currentState == FighterState.Rolling)
            {
                currentState = FighterState.Vulnerable;
                _stateTimer = vulnerableDuration;

                _anim.SetLayerWeight(1, 1f);
                _anim.applyRootMotion = false;
                _rBody.linearDamping = 10f;
                _currentAttackType = 0;
            }
            else if (currentState == FighterState.Vulnerable)
            {
                currentState = FighterState.Idle;
            }
        }
    }

    private void UpdateStamina()
    {
        if (currentState == FighterState.Idle)
        {
            float regenRate = isBlocking ? blockStaminaRegen : staminaRegenRate;
            currentStamina = Mathf.Clamp(currentStamina + regenRate * Time.fixedDeltaTime, 0f, maxStamina);
        }
    }

    private void UpdateMovement()
    {
        if (currentState != FighterState.Idle)
        {
            _anim.SetFloat("MoveSpeed", 0f);
            return;
        }

        transform.Rotate(0, _currentTurnSignal * turnSpeed * Time.fixedDeltaTime, 0);
        Vector3 localMove = (transform.forward * _currentMoveSignal.z) + (transform.right * _currentMoveSignal.x);
        float speedPenalty = (currentStamina <= 0f) ? 0.5f : 1.0f;

        _rBody.AddForce(localMove * forceMultiplier * speedPenalty);
        _anim.SetFloat("MoveSpeed", _rBody.linearVelocity.magnitude);
    }
}