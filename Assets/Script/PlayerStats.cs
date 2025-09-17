using UnityEngine;
using System;
using System.Collections;
using Mirror;

public class PlayerStats : CharacterStats
{
    // === 글로벌 이벤트: 스폰/디스폰 ===
    public static event Action<PlayerStats> Spawned;
    public static event Action<PlayerStats> Despawned;

    // === UI 바인딩용 인스턴스 이벤트 ===
    public event Action<float, float>          OnHealthChanged;
    public event Action<float, float>          OnExpChanged;    // (현재EXP, 레벨업EXP)
    public event Action<int>                   OnLevelChanged;
    public event Action<float>                 OnSpeedChanged;
    public event Action<float>                 OnPowerChanged;


    // == 네트워크 동기화 값(SyncVar) + Hook ==
    [SyncVar(hook = nameof(OnExpSync))]      private float currentExp   = 0f;   // EXP
    [SyncVar(hook = nameof(OnExpToLvSync))]  private float expToLevelUp = 100f; // 필요 EXP
    [SyncVar(hook = nameof(OnLevelSync))]    private int level        = 1;    // 레벨

    [SyncVar(hook = nameof(OnAttackDamageChanged))]
    private float syncedAttackDamage = 10f; // 공격력(능력치)

    [SyncVar(hook = nameof(OnMoveSpeedChanged))]
    private float syncedMoveSpeed = 5f; // 이동속도(능력치)

    [SyncVar] private int   killCount   = 0;
    [SyncVar] private float totalDamage = 0f;
    [SyncVar] private string nickName;

    private MatchManager myMatchManager;

    // == 에디터/레퍼런스 ==
    [SerializeField] private Animator animator;


    // == 런타임 상태/튜닝 값 ==
    private bool  isDead = false;
    public  float healthPerLevel = 10f;
    public  float damagePerLevel = 5f;
    public  float speedPerLevel = 0.5f;

    private float originalSpeed;
    private float originalDamage;


    // == 공개 프로퍼티 ==
    public float CurrentExp   => currentExp;
    public float ExpToLevelUp => expToLevelUp;
    public int   Level        => level;
    public int   KillCount    { get => killCount;   set => killCount = value; }
    public float TotalDamage  { get => totalDamage; set => totalDamage = value; }
    public string NickName { get; set; }

    public override float MoveSpeed    => syncedMoveSpeed;
    public override float AttackDamage => syncedAttackDamage;


    // == 라이프사이클 ==
    public override void OnStartClient()
    {
        base.OnStartClient();

        originalSpeed = syncedMoveSpeed;
        originalDamage = syncedAttackDamage;

        Spawned?.Invoke(this);

        if (isLocalPlayer) return;
            UIManager.Instance?.RegisterPlayer(this);
    }

    public override void OnStopClient()
    {
        base.OnStopClient();

        if (myMatchManager != null)
        {
            myMatchManager.RemovePlayer(transform);
        }

        Despawned?.Invoke(this);
    }


    protected override void OnHealthSynced(float cur, float max)
    {
        OnHealthChanged?.Invoke(cur, max);
    }

    public override void OnStartServer()
    {
        base.OnStartServer();

        var matchId = GetComponent<NetworkMatch>().matchId;

        if (MatchManager.ActiveMatches.TryGetValue(matchId, out myMatchManager))
        {
            myMatchManager.AddPlayer(transform);
        }

        SetHealth(maxHealth, maxHealth);
        originalSpeed = syncedMoveSpeed;
        originalDamage = syncedAttackDamage;
        NetworkGameState.Instance?.Register(this);
    }

#if UNITY_EDITOR
    new private void OnValidate()
    {
        originalSpeed = syncedMoveSpeed;
        originalDamage = syncedAttackDamage;
    }
#endif


    // == 서버 전용: 체력/피해 처리 ==
    [Server]
    public override void SetHealth(float current, float max)
    {
        currentHealth = current;
        maxHealth = max;
        OnHealthChanged?.Invoke(currentHealth, maxHealth);
    }

    [Server]
    public override void TakeDamage(float damage, GameObject attacker = null)
    {
        currentHealth -= damage;
        OnHealthChanged?.Invoke(currentHealth, maxHealth);

        TargetUpdateHealth(connectionToClient, currentHealth, maxHealth);

        if (currentHealth <= 0)
            Die();
    }


    // == 클라이언트 RPC/타겟 RPC ==
    [TargetRpc]
    public void TargetUpdateHealth(NetworkConnection target, float current, float max)
    {
        OnHealthChanged?.Invoke(current, max);
    }

    [TargetRpc]
    private void TargetUpdateExp(NetworkConnection target, float current, float toLevelUp)
    {
        OnExpChanged?.Invoke(current, toLevelUp);
    }

    [TargetRpc]
    public void TargetTeleport(NetworkConnectionToClient target, Vector3 pos, Quaternion rot)
    {
        // 이 스크립트가 붙은 "내" 플레이어 오브젝트에서 직접 스냅
        var t = transform;

        if (t.TryGetComponent<CharacterController>(out var cc))
        {
            cc.enabled = false;
            t.SetPositionAndRotation(pos, rot);
            cc.enabled = true;
        }
        else
        {
            t.SetPositionAndRotation(pos, rot);
        }

        if (t.TryGetComponent<Rigidbody>(out var rb))
        {
            rb.angularVelocity = Vector3.zero;
            rb.position = pos;
            rb.rotation = rot;
        }
    }

    // == 로컬/공용 API ==
    public void Heal(float amount)
    {
        currentHealth = Mathf.Min(currentHealth + amount, maxHealth);
        OnHealthChanged?.Invoke(currentHealth, maxHealth);
    }

    public void AddExp(float exp)
    {
        currentExp += exp;
        OnExpChanged?.Invoke(currentExp, expToLevelUp);

        while (currentExp >= expToLevelUp)
            LevelUp();
    }

    [Server]
    public void GainExp(float amount)
    {
        currentExp += amount;

        // 서버에선 로직만, UI는 클라이언트에 따로 전달
        TargetUpdateExp(connectionToClient, currentExp, expToLevelUp);

        if (currentExp >= expToLevelUp)
        {
            LevelUp();
        }
    }

    public void ApplyItemEffect(ItemData.ItemType itemType, float amount, float duration)
    {
        switch (itemType)
        {
            case ItemData.ItemType.SpeedBoost:
                ApplySpeedBoost(amount, duration);
                UIManager.Instance.ShowItemEffectMessage("Speed Up!", duration);
                break;
            case ItemData.ItemType.DamageBoost:
                ApplyDamageBoost(amount, duration);
                UIManager.Instance.ShowItemEffectMessage("Damage Up", duration);
                break;
        }
    }

    public void ApplySpeedBoost(float amount, float duration)  => StartCoroutine(SpeedBoost(amount, duration));
    public void ApplyDamageBoost(float amount, float duration) => StartCoroutine(DamageBoost(amount, duration));


    // == 코루틴(버프) ==
    private IEnumerator SpeedBoost(float amount, float duration)
    {
        syncedMoveSpeed = originalSpeed * amount;
        OnSpeedChanged?.Invoke(syncedMoveSpeed);

        yield return new WaitForSeconds(duration);

        syncedMoveSpeed = originalSpeed;
        OnSpeedChanged?.Invoke(syncedMoveSpeed);
    }

    private IEnumerator DamageBoost(float amount, float duration)
    {
        syncedAttackDamage = originalDamage * amount;
        OnPowerChanged?.Invoke(syncedAttackDamage);

        yield return new WaitForSeconds(duration);

        syncedAttackDamage = originalDamage;
        OnPowerChanged?.Invoke(syncedAttackDamage);
    }


    // == 사망 처리 ==
    protected override void Die()
    {
        if (isDead) return;
        isDead = true;

        Debug.Log("플레이어 사망 처리");
        RpcPlayerDie();
        if (isServer) NetworkGameState.Instance?.Unregister(this);
        base.Die();
    }

    [ClientRpc]
    public void RpcPlayerDie()
    {
        if (animator == null)
            animator = GetComponent<Animator>();

        animator?.SetTrigger("die");
        if (isLocalPlayer) SpectatorManager.EnterSpectate(this);
    }


    // == SyncVarHooks ==
    private void OnExpSync(float oldV, float newV) => OnExpChanged?.Invoke(newV, expToLevelUp);
    private void OnExpToLvSync(float oldV, float newV) => OnExpChanged?.Invoke(currentExp, newV);
    private void OnLevelSync(int oldV, int newV) => OnLevelChanged?.Invoke(newV);

    void OnMoveSpeedChanged(float oldVal, float newVal)    => OnSpeedChanged?.Invoke(newVal);
    void OnAttackDamageChanged(float oldVal, float newVal) => OnPowerChanged?.Invoke(newVal);


   // == 내부로직: 레벨업 ==
    private void LevelUp()
    {
        currentExp -= expToLevelUp;
        level++;
        expToLevelUp *= 1.5f;

        maxHealth += healthPerLevel;
        syncedAttackDamage += damagePerLevel;
        syncedMoveSpeed += speedPerLevel;

        SetHealth(maxHealth, maxHealth);

        OnExpChanged?.Invoke(currentExp, expToLevelUp);
        OnLevelChanged?.Invoke(level);
        OnSpeedChanged?.Invoke(syncedMoveSpeed);
        OnPowerChanged?.Invoke(syncedAttackDamage);

        Debug.Log($"레벨 업! 현재 레벨: {level}");
    }

    public void EmitAll()
    {
        OnHealthChanged?.Invoke(currentHealth, maxHealth);
        OnExpChanged?.Invoke(currentExp, expToLevelUp);
        OnLevelChanged?.Invoke(level);
    }
}
