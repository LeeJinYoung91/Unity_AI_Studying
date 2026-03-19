using UnityEngine;
using System;
using Cysharp.Threading.Tasks;
using System.Threading;

[RequireComponent(typeof(Rigidbody))]
public class Projectile : MonoBehaviour
{
    [Header("Flight Settings")]
    [SerializeField] private float _speed = 25f;
    [SerializeField] private float _lifeTime = 3f;

    [Header("Damage Settings")]
    [SerializeField] private float _lightDamage = 10f;
    [SerializeField] private float _heavyDamage = 25f;

    private BaseCharacterAgent _shooter;
    private Vector3 _direction;
    private Action<Projectile> _returnAction;
    private float _lockedDamage;
    private Rigidbody _rBody;
    private CancellationTokenSource _cts;
    private bool _isReleased = false;

    private void Awake()
    {
        _rBody = GetComponent<Rigidbody>();
        _rBody.useGravity = false;
        _rBody.constraints = RigidbodyConstraints.FreezeRotation;
    }

    public void Fire(BaseCharacterAgent shooter, Vector3 dir, Action<Projectile> returnAction)
    {
        _shooter = shooter;
        _direction = dir;
        _returnAction = returnAction;
        
        _isReleased = false;

        _lockedDamage = shooter.CurrentAttackType == 2 ? _heavyDamage : _lightDamage;
        transform.localScale = shooter.CurrentAttackType == 2 ? Vector3.one * 1.5f : Vector3.one * 1.0f;

        _cts?.Cancel();
        _cts = new CancellationTokenSource();
        LifetimeRoutineAsync(_cts.Token).Forget();
    }

    private void Update()
    {
        transform.position += _direction * _speed * Time.deltaTime;
    }

    public void ResetVelocity()
    {
        if (_rBody != null) _rBody.linearVelocity = Vector3.zero;
    }

    private async UniTaskVoid LifetimeRoutineAsync(CancellationToken token)
    {
        bool isCancelled = await UniTask.Delay(TimeSpan.FromSeconds(_lifeTime), ignoreTimeScale: false, cancellationToken: token).SuppressCancellationThrow();
        if (!isCancelled) ReturnToPool();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (_shooter == null || other.gameObject == _shooter.gameObject) return;

        BaseCharacterAgent hitAgent = other.GetComponentInParent<BaseCharacterAgent>();

        if (hitAgent != null && hitAgent.ViewModel.CurrentState.Value != FighterState.Dead)
        {
            bool wasBlocked = hitAgent.ReceiveAttack(_lockedDamage, transform.position);
            _shooter.ViewModel.AddReward(wasBlocked ? 0.3f : 1.0f);
            
            _cts?.Cancel();
            ReturnToPool();
        }
        else if (!other.isTrigger)
        {
            _cts?.Cancel();
            ReturnToPool();
        }
    }

    private void ReturnToPool()
    {
        if (_isReleased) return;
        
        _isReleased = true;
        _returnAction?.Invoke(this);
    }
}