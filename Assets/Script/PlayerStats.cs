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
    public event Action<float, float> OnHealthChanged;
    public event Action<float, float> OnExpChanged;    // (현재EXP, 레벨업EXP)
    public event Action<int> OnLevelChanged;
    public event Action<float> OnSpeedChanged;
    public event Action<float> OnPowerChanged;

    // == 네트워크 동기화 값(SyncVar) + Hook ==
    [SyncVar(hook = nameof(OnExpSync))] private float currentExp = 0f;     // EXP
    [SyncVar(hook = nameof(OnExpToLvSync))] private float expToLevelUp = 100f;   // 필요 EXP
    [SyncVar(hook = nameof(OnLevelSync))] private int level = 1;      // 레벨

    [SyncVar(hook = nameof(OnAttackDamageChanged))] private float syncedAttackDamage = 10f;
    [SyncVar(hook = nameof(OnMoveSpeedChanged))] private float syncedMoveSpeed = 5f;

    [SyncVar] private int killCount = 0;
    [SyncVar] private float totalDamage = 0f;

    // 🔸 닉네임 SyncVar로 통일
    [SyncVar(hook = nameof(OnNickSync))] private string nickName;

    [SyncVar] public bool isAlive = true;   // 서버 생존 추적용
    [SyncVar] public string matchIdStr;         // 서버 매치 식별용 (GUID 문자열)

    private MatchManager myMatchManager;

    // == 에디터/레퍼런스 ==
    [SerializeField] private Animator animator;

    // == 런타임 상태/튜닝 값 ==
    private bool isDead = false;
    public float healthPerLevel = 10f;
    public float damagePerLevel = 5f;
    public float speedPerLevel = 0.5f;

    [SerializeField] private float originalSpeed = 5.0f;
    [SerializeField] private float originalDamage;

    [SerializeField] private float baseExpRequirement = 100f;   // 첫 번째 레벨업 필요 EXP
    [SerializeField] private float expIncrement = 25f;         // 레벨마다 증가하는 값

    // == 공개 프로퍼티 ==
    public float CurrentExp => currentExp;
    public float ExpToLevelUp => expToLevelUp;
    public int Level => level;
    public int KillCount { get => killCount; set => killCount = value; }
    public float TotalDamage { get => totalDamage; set => totalDamage = value; }

    public string NickName
    {
        get => nickName;
        [Server]
        set => nickName = value; // SyncVar에 직접 설정
    }

    public bool IsDead => isDead;
    public override float MoveSpeed => syncedMoveSpeed;
    public override float AttackDamage => syncedAttackDamage;

    // == 라이프사이클 ==
    public override void OnStartClient()
    {
        base.OnStartClient();

        syncedMoveSpeed = originalSpeed;
        syncedAttackDamage = originalDamage;

        Debug.Log("스폰로그 보내기");
        Spawned?.Invoke(this);

        // 원격 플레이어 표시/리스트용 등록이 필요하면 유지
        if (!isLocalPlayer)
            UIManager.Instance?.RegisterPlayer(this);
    }

    // 로컬 권한 획득 시점: HUD를 내 캐릭터로 바인딩
    public override void OnStartAuthority()
    {
        base.OnStartAuthority();
        // 관전 종료 호출은 네 SpectatorManager에 'Exit'류 API가 없으니 생략
        // HUD를 게임 HUD로 전환 + UI가 내 스탯을 구독하도록 Register 호출
        UIManager.Instance?.RegisterPlayer(this);
        UIManager.Instance?.EnterGameplayHUD();
        EmitAll(); // 현재 수치 즉시 반영
    }
    public override void OnStartLocalPlayer()
    {
        base.OnStartLocalPlayer();
        StartCoroutine(DeferredBindUI());
    }

    private IEnumerator DeferredBindUI()
    {
        // UI 오브젝트/Canvas 생성 대기
        yield return null;
        yield return null;

        var ui = UIManager.Instance
                 ?? GameObject.FindFirstObjectByType<UIManager>(FindObjectsInactive.Include);
        if (ui != null)
        {
            ui.RegisterPlayer(this);
            ui.EnterGameplayHUD();
        }

        // 서버 값 스냅샷 동기화(아래 C 패치 포함 시 생략 가능)
        EmitAll();
    }


    public override void OnStopClient()
    {
        base.OnStopClient();

        if (myMatchManager != null)
            myMatchManager.RemovePlayer(transform);

        // HUD 연결 해제
        UIManager.Instance?.UnregisterPlayer(this);

        Despawned?.Invoke(this);
    }




    // == 서버 전용: 초기화/등록 ==
    [Server]
    public override void OnStartServer()
    {
        base.OnStartServer();

        ServerResetAllStats();

        var matchId = GetComponent<NetworkMatch>().matchId;
        matchIdStr = matchId.ToString();

        if (MatchManager.ActiveMatches.TryGetValue(matchId, out myMatchManager))
            myMatchManager.AddPlayer(transform);
        if (connectionToClient != null)
            TargetSyncAll(connectionToClient, currentHealth, maxHealth, currentExp, expToLevelUp, level, syncedMoveSpeed, syncedAttackDamage);
        NetworkGameState.Instance?.Register(this);
    }
    [TargetRpc]
    private void TargetSyncAll(NetworkConnectionToClient conn,
    float curHp, float maxHp, float exp, float toLv, int lv, float spd, float dmg)
    {
        currentHealth = curHp;
        maxHealth = maxHp;
        EmitAll();
        //OnHealthChanged?.Invoke(currentHealth, maxHealth);

        //OnExpChanged?.Invoke(exp, toLv);
        //OnLevelChanged?.Invoke(lv);
        //OnSpeedChanged?.Invoke(spd);
        //OnPowerChanged?.Invoke(dmg);
    }
    // 서버 전용: 완전 초기화(스폰 직후 항상 호출)
    [Server]
    public void ServerResetAllStats()
    {
        isDead = false;
        isAlive = true;
        killCount = 0;
        totalDamage = 0f;

        currentExp = 0f;
        level = 1;
        expToLevelUp = 100f;

        SetHealth(maxHealth, maxHealth);

        originalSpeed = syncedMoveSpeed;
        originalDamage = syncedAttackDamage;
    }

    // == 서버 전용: 체력/피해 처리 ==
    [Server]
    public override void SetHealth(float current, float max)
    {
        currentHealth = current;
        maxHealth = max;
        //OnHealthChanged?.Invoke(currentHealth, maxHealth);
    }

    [Server]
    public override void TakeDamage(float damage, GameObject attacker = null)
    {
        currentHealth -= damage;
        //OnHealthChanged?.Invoke(currentHealth, maxHealth);

        //if (connectionToClient != null)
        //    TargetUpdateHealth(connectionToClient, currentHealth, maxHealth);

        if (currentHealth <= 0)
            Die();
    }

    // == Target/Client RPC ==
    //[TargetRpc]
    //public void TargetUpdateHealth(NetworkConnection target, float current, float max)
    //    => OnHealthChanged?.Invoke(current, max);

    //[TargetRpc]
    //private void TargetUpdateExp(NetworkConnection target, float current, float toLevelUp)
    //    => OnExpChanged?.Invoke(current, toLevelUp);

    [TargetRpc]
    public void TargetTeleport(NetworkConnectionToClient target, Vector3 pos, Quaternion rot)
    {
        var t = transform;

        if (t.TryGetComponent<CharacterController>(out var cc))
        {
            cc.enabled = false;
            t.SetPositionAndRotation(pos, rot);
            cc.enabled = true;
        }
        else t.SetPositionAndRotation(pos, rot);

        if (t.TryGetComponent<Rigidbody>(out var rb))
        {
            rb.angularVelocity = Vector3.zero;
            rb.position = pos; rb.rotation = rot;
        }
    }

    [TargetRpc]
    public void TargetBindHUD(NetworkConnectionToClient conn)
    {
        // 관전 종료 API 없음 → HUD만 전환
        UIManager.Instance?.RegisterPlayer(this);
        UIManager.Instance?.EnterGameplayHUD();
        EmitAll();
    }


    // == 로컬/공용 API ==
    public void Heal(float amount)
    {
        currentHealth = Mathf.Min(currentHealth + amount, maxHealth);
        //OnHealthChanged?.Invoke(currentHealth, maxHealth);
    }

    public void AddExp(float exp)
    {
        currentExp += exp;
        //OnExpChanged?.Invoke(currentExp, expToLevelUp);
        while (currentExp >= expToLevelUp) LevelUp();
    }

    [Server]
    public void GainExp(float amount)
    {
        currentExp += amount;
        //if (connectionToClient != null)
        //    TargetUpdateExp(connectionToClient, currentExp, expToLevelUp);

        if (currentExp >= expToLevelUp)
            LevelUp();
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

    public void ApplySpeedBoost(float amount, float duration) => StartCoroutine(SpeedBoost(amount, duration));
    public void ApplyDamageBoost(float amount, float duration) => StartCoroutine(DamageBoost(amount, duration));

    private IEnumerator SpeedBoost(float amount, float duration)
    {
        var tmp = syncedMoveSpeed;
        syncedMoveSpeed *= amount;
        //OnSpeedChanged?.Invoke(syncedMoveSpeed);
        yield return new WaitForSeconds(duration);
        syncedMoveSpeed = originalSpeed;
        //OnSpeedChanged?.Invoke(syncedMoveSpeed);
    }

    private IEnumerator DamageBoost(float amount, float duration)
    {
        var tmp = syncedAttackDamage;
        syncedAttackDamage *= amount;
        //OnPowerChanged?.Invoke(syncedAttackDamage);
        yield return new WaitForSeconds(duration);
        syncedAttackDamage = originalDamage;
        //OnPowerChanged?.Invoke(syncedAttackDamage);
    }

    // == 사망 처리 ==
    protected override void Die()
    {
        if (isDead) return;         // 중복 방지
        isDead = true;

        Debug.Log("플레이어 사망 처리");
        RpcPlayerDie();

        if (isServer)
        {
            if (isAlive) isAlive = false;   // 한 번만 off

            var nm = (CustomNetworkManager_Server)NetworkManager.singleton;
            nm.ServerNotifyPlayerDead(matchIdStr);

            NetworkGameState.Instance?.Unregister(this);
        }

        base.Die();
    }

    [ClientRpc]
    public void RpcPlayerDie()
    {
        if (animator == null) animator = GetComponent<Animator>();
        animator?.SetTrigger("die");

        if (isLocalPlayer)
        {
            SpectatorManager.EnterSpectate(this);
            UIManager.Instance?.EnterSpectatorHUD();
            Debug.Log("[UI] EnterSpectatorHUD 호출됨 (로컬 플레이어)");
        }
    }

    // == SyncVar Hooks ==
    protected override void OnHealthSynced(float cur, float max) => OnHealthChanged?.Invoke(cur, max);
    private void OnExpSync(float oldV, float newV) => OnExpChanged?.Invoke(newV, expToLevelUp);
    private void OnExpToLvSync(float oldV, float newV) => OnExpChanged?.Invoke(currentExp, newV);
    private void OnLevelSync(int oldV, int newV) => OnLevelChanged?.Invoke(newV);
    private void OnMoveSpeedChanged(float o, float n) => OnSpeedChanged?.Invoke(n);
    private void OnAttackDamageChanged(float o, float n) => OnPowerChanged?.Invoke(n);

    private void OnNickSync(string oldName, string newName)
    {
        // UIManager에 RefreshNameTag가 없으므로 여기서는 생략
        // (필요하면 이름표 컴포넌트를 직접 찾아 갱신하세요)
    }

    // == 내부 로직: 레벨업 ==
    private void LevelUp()
    {
        currentExp -= expToLevelUp;
        level++;
        if (level == 2)
        {
            expToLevelUp = baseExpRequirement;
        }
        else
        {
            expToLevelUp = baseExpRequirement + (level - 2) * expIncrement;
        }

        maxHealth += healthPerLevel;
        syncedAttackDamage += damagePerLevel;
        syncedMoveSpeed += speedPerLevel;

        SetHealth(maxHealth, maxHealth);
        EmitAll();
        //OnExpChanged?.Invoke(currentExp, expToLevelUp);
        //OnLevelChanged?.Invoke(level);
        //OnSpeedChanged?.Invoke(syncedMoveSpeed);
        //OnPowerChanged?.Invoke(syncedAttackDamage);

        Debug.Log($"레벨 업! 현재 레벨: {level}");
    }

    // == UI 즉시 반영 ==
    public void EmitAll()
    {
        OnHealthChanged?.Invoke(currentHealth, maxHealth);
        OnExpChanged?.Invoke(currentExp, expToLevelUp);
        OnLevelChanged?.Invoke(level);
        OnSpeedChanged?.Invoke(syncedMoveSpeed);
        OnPowerChanged?.Invoke(syncedAttackDamage);
    }

    // (현재 구조 유지용) TargetRpc 기반 승/패
    [TargetRpc]
    public void TargetShowVictory(NetworkConnectionToClient conn)
        => UIManager.Instance?.ShowVictoryPanel();

    [TargetRpc]
    public void TargetShowDefeat(NetworkConnectionToClient conn)
        => UIManager.Instance?.ShowDefeatPanel();

    [Command]
    public void CmdNotifyMapLoaded()
    {
        var match = GetComponent<NetworkMatch>()?.matchId;
        if (match != null && MatchManager.ActiveMatches.TryGetValue(match.Value, out var manager))
        {
            manager.OnClientMapLoaded(connectionToClient);
        }
    }
}