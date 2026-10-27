using System.Linq;
using UnityEngine;

public sealed class TowerPreviewWeapon : MonoBehaviour
{
    public enum WeaponKind { Crossbow, Cannon, Crystal }
    public WeaponKind kind;
    public int lane;
    public Transform yaw, muzzle, fixedBase, recoilPart;
    public float range = 9, damage = 25, interval = .7f;
    public float slowDuration = 1.2f;
    public bool attacking = true;
    public int shots, hits, chainHits;
    public float maxYawChange, maxBaseMovement, maxRecoil;
    public TowerPreviewTarget currentTarget;
    public Vector3 lastMuzzle, lastAim;
    float fireAt, recoil;
    Vector3 recoilRest, baseStart;
    Quaternion yawStart;
    void Start()
    {
        if(kind==WeaponKind.Crystal)
        {
            var crystal=GetComponentsInChildren<Transform>(true).FirstOrDefault(t=>t.name=="Crystal");
            if(crystal&&!crystal.GetComponent<TowerPreviewCrystalSpin>())crystal.gameObject.AddComponent<TowerPreviewCrystalSpin>();
        }
        if (yaw) yawStart = yaw.rotation;
        if (fixedBase) baseStart = fixedBase.position;
        if (recoilPart) recoilRest = recoilPart.localPosition;
        fireAt = Time.time + .8f + lane * .25f;
    }
    public void ResetWeapon()
    {
        shots=hits=chainHits=0;maxYawChange=maxBaseMovement=maxRecoil=0;recoil=0;currentTarget=null;fireAt=Time.time+.8f+lane*.25f;
        if(yaw)yaw.rotation=yawStart;if(recoilPart)recoilPart.localPosition=recoilRest;
    }
    void Update()
    {
        if (fixedBase) maxBaseMovement = Mathf.Max(maxBaseMovement, Vector3.Distance(baseStart,fixedBase.position));
        if (recoilPart)
        {
            recoil = Mathf.MoveTowards(recoil,0,Time.deltaTime*.9f);
            // Recoil in the independent gimbal's local backward axis.
            recoilPart.localPosition = recoilRest + Vector3.back * recoil;
            maxRecoil = Mathf.Max(maxRecoil,recoil);
        }
        currentTarget = TowerPreviewTarget.All.Where(t => t && t.Alive && t.lane == lane && Vector3.Distance(transform.position,t.transform.position) <= range)
            .OrderBy(t => Vector3.Distance(transform.position,t.transform.position)).FirstOrDefault();
        if (!currentTarget) return;
        Vector3 d = currentTarget.AimPoint - transform.position; d.y = 0;
        if (yaw && d.sqrMagnitude > .001f)
        {
            yaw.rotation = Quaternion.RotateTowards(yaw.rotation,Quaternion.LookRotation(d),160*Time.deltaTime);
            maxYawChange = Mathf.Max(maxYawChange,Quaternion.Angle(yawStart,yaw.rotation));
        }
        if (!attacking || Time.time < fireAt) return;
        if (yaw && Vector3.Angle(yaw.forward,d)>3) return;
        fireAt = Time.time + interval; shots++; lastMuzzle = muzzle.position; lastAim = currentTarget.AimPoint;
        if (kind == WeaponKind.Crystal)
        {
            var color = new Color(.3f,.8f,1);
            TowerPreviewEffects.Bolt(muzzle.position,currentTarget.AimPoint,color);
            TowerPreviewEffects.Burst(currentTarget.AimPoint,color,.5f);
            var origin = currentTarget.AimPoint;
            var others = TowerPreviewTarget.All.Where(t=>t && t != currentTarget && t.Alive && t.lane==lane && Vector3.Distance(t.AimPoint,origin)<3.5f).Take(2).ToArray();
            currentTarget.Damage(damage,slowDuration); hits++;
            foreach(var other in others)
            {
                TowerPreviewEffects.Bolt(origin,other.AimPoint,color); TowerPreviewEffects.Burst(other.AimPoint,color,.35f);
                other.Damage(damage,slowDuration); hits++; chainHits++;
            }
        }
        else
        {
            TowerPreviewEffects.Shot(muzzle,currentTarget,kind==WeaponKind.Cannon,damage,this);
            TowerPreviewEffects.Burst(muzzle.position,kind==WeaponKind.Cannon?new Color(1,.55f,.15f):new Color(1,.85f,.5f),kind==WeaponKind.Cannon?.45f:.16f,kind==WeaponKind.Cannon?18:5);
            if (recoilPart) recoil = kind==WeaponKind.Cannon?.16f:.065f;
        }
    }
}
