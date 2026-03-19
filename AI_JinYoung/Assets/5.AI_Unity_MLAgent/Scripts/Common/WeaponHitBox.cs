using UnityEngine;
using R3;

[RequireComponent(typeof(Collider))]
public class WeaponHitBox : MonoBehaviour
{
    [Header("Agent Reference")]
    [SerializeField] private BaseCharacterAgent _myAgent;

    [Header("Damage Settings")]
    [SerializeField] private float _lightDamage = 10f;
    [SerializeField] private float _heavyDamage = 25f;

    [Header("Reward Settings")]
    [SerializeField] private float _baseHitReward = 1.0f;
    [SerializeField] private float _counterHitReward = 2.0f;
    [SerializeField] private float _perfectDodgeCounterReward = 4.0f;
    [SerializeField] private float _vulnerablePunishReward = 2.5f;
    [SerializeField] private float _blockHitReward = 0.3f;

    [Header("Timers")]
    [SerializeField] private float _perfectDodgeTimeWindow = 3.0f;

    private bool _hasHitThisAttack = false;
    private const string PLAYER_TAG = "Player";
    private void Start()
    {
        if (_myAgent != null && _myAgent.ViewModel != null)
        {
            _myAgent.ViewModel.CurrentState
                .Subscribe(state =>
                {
                    if (state != FighterState.Attacking)
                    {
                        _hasHitThisAttack = false;
                    }
                })
                .RegisterTo(destroyCancellationToken);
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (_myAgent == null || _myAgent.ViewModel == null) return;

        if (_myAgent.ViewModel.CurrentState.Value != FighterState.Attacking || _hasHitThisAttack || !other.CompareTag(PLAYER_TAG))
            return;

        BaseCharacterAgent hitAgent = other.GetComponentInParent<BaseCharacterAgent>();
        if (hitAgent == null || hitAgent == _myAgent || hitAgent.ViewModel == null) return;

        FighterState hitState = hitAgent.ViewModel.CurrentState.Value;
        
        if (hitState == FighterState.Rolling || hitState == FighterState.Dead) return;

        float finalDamage = (_myAgent.CurrentAttackType == 2) ? _heavyDamage : _lightDamage;
        float rewardToGive = _baseHitReward;

        if (hitState == FighterState.Attacking)
        {
            rewardToGive = _counterHitReward;
            if (Time.time - _myAgent.LastRollTime < _perfectDodgeTimeWindow)
            {
                rewardToGive = _perfectDodgeCounterReward;
            }
        }
        else if (hitState == FighterState.Vulnerable)
        {
            rewardToGive = _vulnerablePunishReward;
        }

        // 데미지 전달
        bool wasBlocked = hitAgent.ReceiveAttack(finalDamage, _myAgent.transform.position);

        // 💡 Rule 7: 보상 전달도 Agent를 직접 찌르지 않고 ViewModel(뇌)의 RewardStream을 통해 발송!
        if (wasBlocked)
        {
            _myAgent.ViewModel.AddReward(_blockHitReward);
        }
        else
        {
            _myAgent.ViewModel.AddReward(rewardToGive); 
        }
        
        _hasHitThisAttack = true;
    }
}