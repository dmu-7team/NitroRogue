using UnityEngine;
using Mirror;
using System.Collections.Generic;

public class SkillController : NetworkBehaviour
{
    [Header("��ų ����")]
    [SerializeField] private List<SkillConfig> skills;

    [Header("�ʼ� ����")]
    [SerializeField] private Camera playerCamera; // << ���⿡ ī�޶� ���� ������ ���� �߰�!

    // ��Ÿ�� ������
    private Dictionary<int, SkillConfig> skillDictionary;
    private Dictionary<int, float> skillCooldowns;

    #region �ʱ�ȭ (���� & Ŭ���̾�Ʈ)
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

            // --- ���Ⱑ �����Ǿ����ϴ� ---
            if (playerCamera == null)
            {
                Debug.LogError("[Ŭ���̾�Ʈ] Player Camera�� SkillController�� �Ҵ���� �ʾҽ��ϴ�!");
                return;
            }

            Transform camTransform = playerCamera.transform;
            // ���� ��ġ�� ī�޶� ��ġ �ٷ� ������ ��¦ �����Ͽ� ���� ���� ������ ����
            Vector3 spawnPos = camTransform.position + camTransform.forward * 0.5f;
            CmdUseSkill(skill.skillId, spawnPos, camTransform.rotation);
            // --- ������� ���� ---
        }
        else
        {
            float remaining = (skillCooldowns[skill.skillId] + skill.cooldown) - Time.time;
            Debug.Log($"{skill.skillName} ��Ÿ��: {remaining:F1}�� ����");
        }
    }

    [Command]
    private void CmdUseSkill(int skillId, Vector3 spawnPos, Quaternion spawnRot)
    {
        if (skillDictionary == null || !skillDictionary.TryGetValue(skillId, out SkillConfig skill))
        {
            Debug.LogError($"[����] ID�� {skillId}�� ��ų�� ã�� �� ���ų� ��ųʸ��� �ʱ�ȭ���� �ʾҽ��ϴ�.");
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
            Debug.LogError("[����] projectilePrefab�� SkillConfig�� �Ҵ���� �ʾҽ��ϴ�!");
            return;
        }

        GameObject bombInstance = Instantiate(skill.projectilePrefab, spawnPos, spawnRot);

        var clusterBomb = bombInstance.GetComponent<ClusterBomb>();
        if (clusterBomb == null)
        {
            Debug.LogError("[����] ��ź �����տ� ClusterBomb.cs ��ũ��Ʈ�� �����ϴ�!");
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
            Debug.LogError("[����] ��ź �����տ� Rigidbody ������Ʈ�� �����ϴ�!");
            NetworkServer.Destroy(bombInstance);
            return;
        }

        rb.linearVelocity = spawnRot * Vector3.forward * skill.launchForce;

        NetworkServer.Spawn(bombInstance);
        Debug.Log("[����] Ŭ������ ��ź ���� �� �߻� ����!");
    }
}