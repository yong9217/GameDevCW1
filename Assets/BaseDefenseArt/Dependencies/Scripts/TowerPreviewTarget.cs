using System.Collections.Generic;
using UnityEngine;

public sealed class TowerPreviewTarget : MonoBehaviour
{
    public static readonly List<TowerPreviewTarget> All = new List<TowerPreviewTarget>();
    public int lane;
    public float maxHealth = 180;
    public float health;
    public Vector3 origin;
    public float phase;
    public bool moving = true;
    public int generation, deaths, hits;
    public float totalDamage, slowedUntil;
    [Range(.05f,1f)] public float slowMultiplier = .5f;
    public float CurrentSpeedMultiplier => Time.time < slowedUntil ? Mathf.Clamp(slowMultiplier,.05f,1f) : 1f;
    public Vector3 AimPoint => transform.position + Vector3.up * .65f;
    public bool Alive => health > 0;
    float respawnAt, movementTime;
    Renderer[] visuals;
    Animator animator;
    void OnEnable() { if (!All.Contains(this)) All.Add(this); }
    void OnDisable() { All.Remove(this); }
    void Start()
    {
        health = maxHealth; origin = transform.position;
        movementTime=Time.time;
        visuals = GetComponentsInChildren<Renderer>();
        animator = GetComponentInChildren<Animator>();
        if (animator) animator.Play("Walk");
    }
    void Update()
    {
        if (!Alive)
        {
            if (Time.time >= respawnAt) { health = maxHealth; slowedUntil=0; generation++; foreach (var r in visuals) r.enabled = true; }
            return;
        }
        bool walk = moving;
        float speed=CurrentSpeedMultiplier;
        if (animator) animator.speed = walk ? speed : 0;
        if (walk)
        {
            movementTime+=Time.deltaTime*speed;
            float x = Mathf.Sin(movementTime * .65f + phase) * 1.8f;
            Vector3 p = origin + Vector3.right * x;
            Vector3 d = p - transform.position;
            if (d.sqrMagnitude > .00001f) transform.rotation = Quaternion.LookRotation(d);
            transform.position = p;
        }
    }
    public void Damage(float amount, float slowDuration = 0)
    {
        if (!Alive) return;
        hits++; totalDamage += Mathf.Min(health, amount); health = Mathf.Max(0, health - amount);
        if(slowDuration>0)slowedUntil = Mathf.Max(slowedUntil, Time.time + slowDuration);
        if (!Alive) { deaths++; respawnAt = Time.time + 1.2f; foreach (var r in visuals) r.enabled = false; }
    }
    public void ResetTarget()
    {
        health=maxHealth;hits=deaths=0;totalDamage=0;slowedUntil=0;movementTime=0;generation++;
        transform.position=origin;foreach(var r in visuals)r.enabled=true;
    }
}
