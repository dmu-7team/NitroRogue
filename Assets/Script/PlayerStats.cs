using UnityEngine;
using System;
using System.Collections;
using Mirror;

public class PlayerStats : CharacterStats
{
    public static event Action<PlayerStats> Spawned;
    public static event Action<PlayerStats> Despawned;

    public event Action<float, float> OnHealthChanged;
    public event Action<float, float> OnExpChanged;
    public event Action<int> OnLevelChanged;
    public event Action<float> OnSpeedChanged;
    public event Action<float> OnPowerChanged;

    [SyncVar(hook = nameof(OnExpSync))] private float currentExp = 0f;
    [SyncVar(hook = nameof(OnExpToLvSync))] private float expToLevelUp = 100f;
    [SyncVar(hook = nameof(OnLevelSync))] private int level = 1;

    [SyncVar(hook = nameof(OnAttackDamageChanged))] private float syncedAttackDamage = 10f;
    [SyncVar(hook = nameof(OnMoveSpeedChanged))] private float syncedMoveSpeed = 5f;

    [SyncVar] private int totalKills = 0;
    [SyncVar] public float totalDamage = 0f;

    // ★ 닉네임: SyncVar + hook + 이벤트
    [SyncVar(hook = nameof(OnNicknameChanged))] private string nickname;
    public event Action<string> OnNicknameChangedEvt;
    public string Nickname => nickname;

    [SyncVar] private string userId;

    [SyncVar] public bool isAlive = true;
    [SyncVar] public string matchIdStr;

    private MatchManager myMatchManager;

    [SerializeField] private Animator animator;

    private bool isDead = false;
    public float healthPerLevel = 10f;
    public float damagePerLevel = 5f;
    public float speedPerLevel = 0.5f;

    [SerializeField] private float originalSpeed = 5.0f;
    [SerializeField] private float originalDamage;

    [SerializeField] private float baseExpRequirement = 100f;
    [SerializeField] private float expIncrement = 25f;

    public float CurrentExp => currentExp;
    public float ExpToLevelUp => expToLevelUp;
    public int Level => level;
    public int TotalKills { get => totalKills; set => totalKills = value; }
    public float TotalDamage { get => totalDamage; set => totalDamage = value; }
    public string UserId { get => userId; set => userId = value; }

    public bool IsDead => isDead;
    public override float MoveSpeed => syncedMoveSpeed;
    public override float AttackDamage => syncedAttackDamage;

    public override void OnStartClient()
    {
        base.OnStartClient();

        syncedMoveSpeed = originalSpeed;
        syncedAttackDamage = originalDamage;

        Spawned?.Invoke(this);

        if (!isLocalPlayer)
            UIManager.Instance?.RegisterPlayer(this);
    }

    public override void OnStartAuthority()
    {
        base.OnStartAuthority();
        UIManager.Instance?.RegisterPlayer(this);
        UIManager.Instance?.EnterGameplayHUD();
        EmitAll();
    }

    public override void OnStartLocalPlayer()
    {
        base.OnStartLocalPlayer();
        StartCoroutine(DeferredBindUI());

        userId = PlayerPrefs.GetString("userId", System.Guid.NewGuid().ToString());
        // 아래 두 줄은 보조 동기화 용도 (서버가 Replace 전에 ServerSetNickname으로 최종 보장)
        nickname = PlayerPrefs.GetString("nickname", "Player");
        CmdRegisterPlayer(userId, nickname);
    }

    [Command]
    void CmdRegisterPlayer(string uid, string nick)
    {
        userId = uid;
        nickname = nick;
    }

    [Server] public void ServerSetNickname(string nick) => nickname = nick; // ★ 서버 세터

    private IEnumerator DeferredBindUI()
    {
        yield return null;
        yield return null;

        var ui = UIManager.Instance
                 ?? GameObject.FindFirstObjectByType<UIManager>(FindObjectsInactive.Include);
        if (ui != null)
        {
            ui.RegisterPlayer(this);
            ui.EnterGameplayHUD();
        }

        EmitAll();
    }

    public override void OnStopClient()
    {
        base.OnStopClient();

        if (myMatchManager != null)
            myMatchManager.RemovePlayer(transform);

        UIManager.Instance?.UnregisterPlayer(this);
        Despawned?.Invoke(this);
    }

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
    }

    [Server]
    public void ServerResetAllStats()
    {
        isDead = false;
        isAlive = true;
        totalKills = 0;
        totalDamage = 0f;

        currentExp = 0f;
        level = 1;
        expToLevelUp = 100f;

        SetHealth(maxHealth, maxHealth);

        syncedMoveSpeed = originalSpeed;
        syncedAttackDamage = originalDamage;
    }

    [Server]
    public override void SetHealth(float current, float max)
    {
        currentHealth = current;
        maxHealth = max;
    }

    [Server]
    public override void TakeDamage(float damage, GameObject attacker = null)
    {
        currentHealth -= damage;
        if (currentHealth <= 0)
            Die();
    }

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
        UIManager.Instance?.RegisterPlayer(this);
        UIManager.Instance?.EnterGameplayHUD();
        EmitAll();
    }

    public void Heal(float amount)
    {
        currentHealth = Mathf.Min(currentHealth + amount, maxHealth);
    }

    public void AddExp(float exp)
    {
        currentExp += exp;
        while (currentExp >= expToLevelUp) LevelUp();
    }

    [Server]
    public void GainExp(float amount)
    {
        currentExp += amount;
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
        yield return new WaitForSeconds(duration);
        syncedMoveSpeed = originalSpeed;
    }

    private IEnumerator DamageBoost(float amount, float duration)
    {
        var tmp = syncedAttackDamage;
        syncedAttackDamage *= amount;
        yield return new WaitForSeconds(duration);
        syncedAttackDamage = originalDamage;
    }

    protected override void Die()
    {
        if (isDead) return;
        isDead = true;

        Debug.Log("플레이어 사망 처리");
        RpcPlayerDie();

        if (isServer)
        {
            if (isAlive) isAlive = false;

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

    protected override void OnHealthSynced(float cur, float max) => OnHealthChanged?.Invoke(cur, max);
    private void OnExpSync(float oldV, float newV) => OnExpChanged?.Invoke(newV, expToLevelUp);
    private void OnExpToLvSync(float oldV, float newV) => OnExpChanged?.Invoke(currentExp, newV);
    private void OnLevelSync(int oldV, int newV) => OnLevelChanged?.Invoke(newV);
    private void OnMoveSpeedChanged(float o, float n) => OnSpeedChanged?.Invoke(n);
    private void OnAttackDamageChanged(float o, float n) => OnPowerChanged?.Invoke(n);

    private void LevelUp()
    {
        currentExp -= expToLevelUp;
        level++;
        if (level == 2) expToLevelUp = baseExpRequirement;
        else expToLevelUp = baseExpRequirement + (level - 2) * expIncrement;

        maxHealth += healthPerLevel;
        syncedAttackDamage += damagePerLevel;
        syncedMoveSpeed += speedPerLevel;

        SetHealth(maxHealth, maxHealth);
        EmitAll();

        Debug.Log($"레벨 업! 현재 레벨: {level}");
    }

    public void EmitAll()
    {
        OnHealthChanged?.Invoke(currentHealth, maxHealth);
        OnExpChanged?.Invoke(currentExp, expToLevelUp);
        OnLevelChanged?.Invoke(level);
        OnSpeedChanged?.Invoke(syncedMoveSpeed);
        OnPowerChanged?.Invoke(syncedAttackDamage);
    }

    [TargetRpc] public void TargetShowVictory(NetworkConnectionToClient conn) => UIManager.Instance?.ShowVictoryPanel();
    [TargetRpc] public void TargetShowDefeat(NetworkConnectionToClient conn) => UIManager.Instance?.ShowDefeatPanel();

    [Command]
    public void CmdNotifyMapLoaded()
    {
        var match = GetComponent<NetworkMatch>()?.matchId;
        if (match != null && MatchManager.ActiveMatches.TryGetValue(match.Value, out var manager))
            manager.OnClientMapLoaded(connectionToClient);
    }

    // ★ 닉네임 hook
    void OnNicknameChanged(string oldV, string newV)
    {
        OnNicknameChangedEvt?.Invoke(newV);
    }
}
