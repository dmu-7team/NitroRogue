//
// 이 스크립트는 VFX 데모용 발사체 움직임 예제입니다.
// 실제 게임에서는 최적화, 피격 판정, 풀링 등을 추가 구현해야 합니다.
//

#pragma warning disable 0168 // variable declared but not used.
#pragma warning disable 0219 // variable assigned but not used.
#pragma warning disable 0414 // private field assigned but not used.

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ProjectileMoveScript : MonoBehaviour {

    public bool rotate = false; // 발사체 자체를 회전시킬지 여부
    public float rotateAmount = 45; // 초당 회전 속도
    public float bounceForce = 10; // 튕길 때 반동 힘
    public float speed; // 이동 속도
    public GameObject muzzlePrefab; // 발사 시 생성할 총구 이펙트
    public GameObject hitPrefab; // 충돌 시 생성할 이펙트
    public List<GameObject> trails; // 이동 중 따라다니는 궤적 이펙트들

    private bool collided; // 한 번만 충돌 처리되도록 제어
    private Rigidbody rb; // Rigidbody 캐싱
    private RotateToMouseScript rotateToMouse; // 목표를 바라보게 하는 외부 스크립트
    private GameObject target; // 회전할 목표 대상

    void Start () {
        rb = GetComponent <Rigidbody> ();

        // 발사 시 총구 이펙트 생성
        if (muzzlePrefab != null) {
			var muzzleVFX = Instantiate (muzzlePrefab, transform.position, Quaternion.identity);
			muzzleVFX.transform.forward = gameObject.transform.forward;

            // 파티클이 있으면 지속시간 후 삭제
            var ps = muzzleVFX.GetComponent<ParticleSystem>();
			if (ps != null)
				Destroy (muzzleVFX, ps.main.duration);
			else {
				var psChild = muzzleVFX.transform.GetChild(0).GetComponent<ParticleSystem>();
				Destroy (muzzleVFX, psChild.main.duration);
			}
		}
	}

	void FixedUpdate () {
        // 타겟이 있으면 그 방향을 바라봄
        if (target != null)
            rotateToMouse.RotateToMouse (gameObject, target.transform.position);

        // 자전 회전
        if (rotate)
            transform.Rotate(0, 0, rotateAmount, Space.Self);

        // 앞으로 이동
        if (speed != 0 && rb != null)
			rb.position += (transform.forward) * (speed * Time.deltaTime);   
    }

	void OnCollisionEnter (Collision co) {
            if (co.gameObject.tag != "Bullet" && !collided)
            {
                collided = true;

                // 궤적 파티클 분리 후 삭제
                if (trails.Count > 0)
                {
                    for (int i = 0; i < trails.Count; i++)
                    {
                        trails[i].transform.parent = null;
                        var ps = trails[i].GetComponent<ParticleSystem>();
                        if (ps != null)
                        {
                            ps.Stop();
                            Destroy(ps.gameObject, ps.main.duration + ps.main.startLifetime.constantMax);
                        }
                    }
                }

                // 속도 멈추고 물리 비활성화
                speed = 0;
                GetComponent<Rigidbody>().isKinematic = true;

                // 충돌 지점에 히트 이펙트 생성
                ContactPoint contact = co.contacts[0];
                Quaternion rot = Quaternion.FromToRotation(Vector3.up, contact.normal);
                Vector3 pos = contact.point;

                if (hitPrefab != null)
                {
                    var hitVFX = Instantiate(hitPrefab, pos, rot) as GameObject;

                    var ps = hitVFX.GetComponent<ParticleSystem>();
                    if (ps == null)
                    {
                        var psChild = hitVFX.transform.GetChild(0).GetComponent<ParticleSystem>();
                        Destroy(hitVFX, psChild.main.duration);
                    }
                    else
                        Destroy(hitVFX, ps.main.duration);
                }

                // 이 스크립트만 제거하고 발사체는 유지
                StartCoroutine(DestroyParticle(0f));
        }
	}

    // 일정 시간 후 발사체 제거. 스케일을 줄여가며 사라지게 연출 가능
    public IEnumerator DestroyParticle (float waitTime) {

		if (transform.childCount > 0 && waitTime != 0) {
			List<Transform> tList = new List<Transform> ();

			foreach (Transform t in transform.GetChild(0).transform) {
				tList.Add (t);
			}

			while (transform.GetChild(0).localScale.x > 0) {
				yield return new WaitForSeconds (0.01f);
				transform.GetChild(0).localScale -= new Vector3 (0.1f, 0.1f, 0.1f);
				for (int i = 0; i < tList.Count; i++) {
					tList[i].localScale -= new Vector3 (0.1f, 0.1f, 0.1f);
				}
			}
		}
		
		yield return new WaitForSeconds (waitTime);
		Destroy (gameObject);
	}

    // 외부에서 타겟을 지정할 때 사용하는 함수 (유도탄 등)
    public void SetTarget (GameObject trg, RotateToMouseScript rotateTo)
    {
        target = trg;
        rotateToMouse = rotateTo;
    }
}
