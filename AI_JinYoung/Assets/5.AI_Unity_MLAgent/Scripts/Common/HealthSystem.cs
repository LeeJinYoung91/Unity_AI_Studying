using UnityEngine;
using R3;

public class HealthSystem : MonoBehaviour
{
    [Header("Health Settings")]
    [SerializeField] private float _maxHP = 100f;

    private readonly ReactiveProperty<float> _currentHP = new ReactiveProperty<float>();
    private readonly ReactiveProperty<bool> _isDead = new ReactiveProperty<bool>(false);

    private readonly Subject<float> _onDamageTaken = new Subject<float>();
    private readonly Subject<Unit> _onDeath = new Subject<Unit>();

    public float MaxHP => _maxHP;
    public ReadOnlyReactiveProperty<float> CurrentHP => _currentHP;
    public ReadOnlyReactiveProperty<bool> IsDead => _isDead;
    public Observable<float> OnDamageTaken => _onDamageTaken;
    public Observable<Unit> OnDeath => _onDeath;

    private void Awake()
    {
        ResetHP();
    }

    public void ResetHP()
    {
        _currentHP.Value = _maxHP;
        _isDead.Value = false;
    }

    public void TakeDamage(float damageAmount)
    {
        if (_isDead.Value) return;

        _currentHP.Value -= damageAmount;
        _onDamageTaken.OnNext(damageAmount);

        if (_currentHP.Value <= 0f)
        {
            _currentHP.Value = 0f;
            _isDead.Value = true;
            _onDeath.OnNext(Unit.Default);
        }
    }

    private void OnDestroy()
    {
        _currentHP.Dispose();
        _isDead.Dispose();
        _onDamageTaken.Dispose();
        _onDeath.Dispose();
    }
}