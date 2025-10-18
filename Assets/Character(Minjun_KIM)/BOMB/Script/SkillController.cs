using UnityEngine;
using Mirror;
using System.Collections.Generic;

public class SkillController : NetworkBehaviour
{
    [Header("스킬 슬롯")]
    [SerializeField] private List<SkillConfig> skills;

    [Header("필수 연결")]
    [SerializeField] private Camera playerCamera; // << 여기에 카메라를 직접 연결할 변수 추가!

    // 런타임 데이터
    private Dictionary<int, SkillConfig> skillDictionary;
    private Dictionary<int, float> skillCooldowns;

    #region 초기화 (서버 & 클라이언트)
    public override void OnStartServer()
    {
        base.OnStartServer();
        InitializeSkillDictionary();
    }

    public override void OnStartClient()
    {
        base.OnStartClient();
        InitializeSkillDictionary();
        InitializeCooldowns();
    }

    private void InitializeSkillDictionary()
    {
        skillDictionary = new Dictionary<int, SkillConfig>();
        foreach (var skill in skills)
        {
            if (skill != null) { skillDictionary[skill.skillId] = skill; }
        }
    }

    private void InitializeCooldowns()
    {
        skillCooldowns = new Dictionary<int, float>();
        foreach (var skill in skills)
        {
            if (skill != null) { skillCooldowns[skill.skillId] = Time.time - skill.cooldown; }
        }
    }
    #endregion

    void Update()
    {
        if (!isLocalPlayer) return;

        if (Input.GetKeyDown(KeyCode.X)) { TryUseSkill(0); }
        if (Input.GetKeyDown(KeyCode.Y)) { TryUseSkill(1); }
    }

    private void TryUseSkill(int index)
    {
        if (index < 0 || index >= skills.Count || skills[index] == null) return;

        SkillConfig skill = skills[index];

        if (Time.time >= skillCooldowns[skill.skillId] + skill.cooldown)
        {
            skillCooldowns[skill.skillId] = Time.time;

            // --- 여기가 수정되었습니다 ---
            if (playerCamera == null)
            {
                Debug.LogError("[클라이언트] Player Camera가 SkillController에 할당되지 않았습니다!");
                return;
            }

            Transform camTransform = playerCamera.transform;
            // 생성 위치를 카메라 위치 바로 앞으로 살짝 조정하여 벽에 끼는 현상을 방지
            Vector3 spawnPos = camTransform.position + camTransform.forward * 0.5f;
            CmdUseSkill(skill.skillId, spawnPos, camTransform.rotation);
            // --- 여기까지 수정 ---
        }
        else
        {
            float remaining = (skillCooldowns[skill.skillId] + skill.cooldown) - Time.time;
            Debug.Log($"{skill.skillName} 쿨타임: {remaining:F1}초 남음");
        }
    }

    [Command]
    private void CmdUseSkill(int skillId, Vector3 spawnPos, Quaternion spawnRot)
    {
        if (skillDictionary == null || !skillDictionary.TryGetValue(skillId, out SkillConfig skill))
        {
            Debug.LogError($"[서버] ID가 {skillId}인 스킬을 찾을 수 없거나 딕셔너리가 초기화되지 않았습니다.");
            return;
        }

        switch (skill.type)
        {
            case SkillType.Projectile:
                HandleProjectileSkill(skill, spawnPos, spawnRot);
                break;
        }
    }

    [Server]
    private void HandleProjectileSkill(SkillConfig skill, Vector3 spawnPos, Quaternion spawnRot)
    {
        if (skill.projectilePrefab == null)
        {
            Debug.LogError("[서버] projectilePrefab이 SkillConfig에 할당되지 않았습니다!");
            return;
        }

        GameObject bombInstance = Instantiate(skill.projectilePrefab, spawnPos, spawnRot);

        var clusterBomb = bombInstance.GetComponent<ClusterBomb>();
        if (clusterBomb == null)
        {
            Debug.LogError("[서버] 폭탄 프리팹에 ClusterBomb.cs 스크립트가 없습니다!");
            NetworkServer.Destroy(bombInstance);
            return;
        }

        clusterBomb.owner = this.gameObject;
        clusterBomb.childPrefab = skill.childProjectilePrefab;
        clusterBomb.childCount = skill.childProjectileCount;
        clusterBomb.childForce = skill.childLaunchForce;

        var rb = bombInstance.GetComponent<Rigidbody>();
        if (rb == null)
        {
            Debug.LogError("[서버] 폭탄 프리팹에 Rigidbody 컴포넌트가 없습니다!");
            NetworkServer.Destroy(bombInstance);
            return;
        }

        rb.linearVelocity = spawnRot * Vector3.forward * skill.launchForce;

        NetworkServer.Spawn(bombInstance);
        Debug.Log("[서버] 클러스터 폭탄 생성 및 발사 성공!");
    }
}