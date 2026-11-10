using System.Collections;
using UnityEngine;

// Existing Built-in tower visuals, connected to production enemy health.
public sealed class TowerPreviewEffects : MonoBehaviour
{
    static Texture2D spark;
    static Texture2D SparkTexture()
    {
        if(spark)return spark;
        spark=new Texture2D(32,32,TextureFormat.RGBA32,false);
        for(int y=0;y<32;y++)for(int x=0;x<32;x++)
        {float a=Mathf.Clamp01(1-Vector2.Distance(new Vector2(x,y),new Vector2(15.5f,15.5f))/15.5f);spark.SetPixel(x,y,new Color(1,1,1,a*a));}
        spark.Apply();return spark;
    }
    public static Material Material(Color color)
    {
        var m = new Material(Shader.Find("Sprites/Default")); m.color = color; return m;
    }
    public static GameObject Primitive(PrimitiveType type, string label, Vector3 scale, Color color)
    {
        var g = GameObject.CreatePrimitive(type); g.name = label; g.transform.localScale = scale;
        var c = g.GetComponent<Collider>(); if (c) Destroy(c);
        g.GetComponent<Renderer>().sharedMaterial = Material(color); return g;
    }
    public static void Burst(Vector3 p, Color color, float size, int count = 14)
    {
        var g = new GameObject("Preview hit sparks"); g.transform.position = p;
        g.AddComponent<TowerPreviewEffects>();
        var ps = g.AddComponent<ParticleSystem>();
        ps.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = ps.main; main.loop = false; main.duration = .35f; main.startLifetime = .32f;
        main.startSpeed = size * 3; main.startSize = size * .14f; main.startColor = color; main.simulationSpace = ParticleSystemSimulationSpace.World;
        var emission = ps.emission; emission.rateOverTime = 0; emission.SetBursts(new[] { new ParticleSystem.Burst(0, (short)count) });
        var shape = ps.shape; shape.shapeType = ParticleSystemShapeType.Sphere; shape.radius = size * .08f;
        var material=Material(Color.white);material.mainTexture=SparkTexture();
        ps.GetComponent<ParticleSystemRenderer>().sharedMaterial = material;
        var sizeLife=ps.sizeOverLifetime;sizeLife.enabled=true;sizeLife.size=new ParticleSystem.MinMaxCurve(1,AnimationCurve.Linear(0,1,1,0));
        ps.Play(); Destroy(g, 1.2f);
    }
    public static void Bolt(Vector3 start, Vector3 end, Color color)
    {
        var g = new GameObject("Preview lightning (rebuilt)");
        var fx = g.AddComponent<TowerPreviewEffects>(); fx.StartCoroutine(fx.Lightning(start, end, color));
    }
    IEnumerator Lightning(Vector3 a, Vector3 b, Color color)
    {
        var line = gameObject.AddComponent<LineRenderer>(); line.sharedMaterial = Material(color);
        line.positionCount = 13; line.startWidth = .075f; line.endWidth = .035f; line.numCapVertices = 3;
        float t = 0;
        while (t < .32f)
        {
            for (int i = 0; i < 13; i++)
            {
                var p = Vector3.Lerp(a, b, i / 12f);
                if (i != 0 && i != 12) p += Random.insideUnitSphere * .19f;
                line.SetPosition(i, p);
            }
            line.widthMultiplier = 1 - t / .4f; t += Time.deltaTime; yield return null;
        }
        Destroy(gameObject);
    }
    public static void Shot(Transform muzzle, EnemyHealth target, bool cannon, float damage, TowerPreviewWeapon owner)
    {
        var g = new GameObject(cannon ? "Preview cannonball flight" : "Preview arrow flight");
        var fx = g.AddComponent<TowerPreviewEffects>(); fx.StartCoroutine(fx.Flight(muzzle.position, target, cannon, damage, owner));
    }
    IEnumerator Flight(Vector3 start, EnemyHealth target, bool cannon, float damage, TowerPreviewWeapon owner)
    {
        if (cannon)
        {
            var ball = Primitive(PrimitiveType.Sphere, "Cannonball", Vector3.one * .22f, new Color(.17f,.19f,.23f)); ball.transform.SetParent(transform, false);
        }
        else
        {
            var shaft = Primitive(PrimitiveType.Cylinder, "Arrow shaft", new Vector3(.045f,.38f,.045f), new Color(.55f,.32f,.13f));
            shaft.transform.SetParent(transform, false); shaft.transform.localRotation = Quaternion.Euler(90,0,0);
            var tip = Primitive(PrimitiveType.Cube, "Arrow tip", new Vector3(.09f,.055f,.15f), new Color(.85f,.88f,.92f));
            tip.transform.SetParent(transform, false); tip.transform.localPosition = Vector3.forward * .4f;
            var fin = Primitive(PrimitiveType.Cube, "Arrow fletching", new Vector3(.16f,.035f,.15f), new Color(.9f,.86f,.6f));
            fin.transform.SetParent(transform, false); fin.transform.localPosition = Vector3.back * .28f;
        }
        transform.position = start;
        float duration = cannon ? .7f : .36f, t = 0;
        while (t < 1 && target && target.isActiveAndEnabled && target.IsAlive)
        {
            t = Mathf.Min(1,t + Time.deltaTime / duration);
            Vector3 p = Vector3.Lerp(start, target.AimPoint,t) + Vector3.up * (cannon ? Mathf.Sin(t*Mathf.PI)*1.5f : 0);
            Vector3 d = p - transform.position;
            if (d.sqrMagnitude > .000001f) transform.rotation = Quaternion.LookRotation(d);
            transform.position = p; yield return null;
        }
        if (target && target.isActiveAndEnabled && target.IsAlive)
        {
            target.Damage(damage);
            if (owner) owner.hits++;
            Burst(target.AimPoint, cannon ? new Color(1,.45f,.12f) : new Color(1,.85f,.4f), cannon ? .8f : .35f, cannon ? 28 : 9);
        }
        Destroy(gameObject);
    }
    void OnDestroy()
    {
        foreach(var r in GetComponentsInChildren<Renderer>()) if(r.sharedMaterial)Destroy(r.sharedMaterial);
    }
}
