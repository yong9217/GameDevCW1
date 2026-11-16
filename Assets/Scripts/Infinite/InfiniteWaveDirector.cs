using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// PLACEHOLDER wave design for Infinite mode, only so the mode can be played and tested now.
// Replace the body of RunWaves() (or this whole component) with the real design. Everything it needs is on InfiniteFlow:
//   flow.Spawn(kind, angleDegrees, arcDegrees)   one enemy on the ring around the castle
//   flow.SpawnAnywhere(kind)                      one enemy from a random direction
//   flow.SurvivedSeconds   flow.IsRunning   flow.EnemiesAlive
[RequireComponent(typeof(InfiniteFlow))]
public class InfiniteWaveDirector : MonoBehaviour
{
    [Tooltip("Seconds before the first wave, so the player can build a first tower.")]
    [Min(0f)] public float firstWaveDelay = 15f;
    [Tooltip("Seconds from the start of one wave to the start of the next.")]
    [Min(1f)] public float secondsBetweenWaves = 25f;
    [Tooltip("Begin wave scaling from the stated wave")]
    [Min(0)]public int waveScaleFrom = 10;

    [Header("Spawn delays")]
    [SerializeField] public Dictionary<GameFlow.EnemyKind,float> spawnDelays = new Dictionary<GameFlow.EnemyKind, float>();

    [Header("References")]
    public PlayerEconomy bank;
    public Debugger debug;

    InfiniteFlow flow;

    void Awake()
    {
        flow = GetComponent<InfiniteFlow>();

        debug.Add("l", () =>
        {
            Debug.Log(bank.PrintCatalog());
        }, "DebugCat");
    }

    // Waves started so far (0 before the first wave).
    public int WavesStarted { get; private set; }
    // Seconds (game time) until the next wave starts; shown on the HUD under the survival timer.
    public float NextWaveIn { get; private set; }

    void Start() => StartCoroutine(RunWaves());

    // Counts down in game time (Time.deltaTime), so pauses and tutorial papers stop the countdown too.
    IEnumerator RunWaves()
    {
        NextWaveIn = firstWaveDelay;
        while (flow.IsRunning)
        {
            NextWaveIn -= Time.deltaTime;
            if (NextWaveIn <= 0f)
            {
                WavesStarted++;
                StartCoroutine(SpawnWave(WavesStarted));
                NextWaveIn += secondsBetweenWaves;
            }
            yield return null;
        }
    }

    // Each wave is a little bigger and comes from a wider arc than the one before.

    //Hangles each wave spawn, using ONLY wave number. Have to scale accordingly
    IEnumerator SpawnWave(int wave)
    {
        float centre = Random.Range(0f, 360f); //Choose a random direction

        float arc = Mathf.Min(360f, 60f + wave * 30f); //Choose some random deviation from direction, scaling linearly with wave

        int bosses, normals, fasts, heavies;

        float dmgCoeff = 1f;

        if(wave > waveScaleFrom - 1) //Wave 10 and beyond, gives the 1st 9 waves to setup
        {
            //The intention is to grow *slightly* faster than linear to guarantee an end
            //Has O(xlog(x))
            normals = Mathf.RoundToInt(wave*Mathf.Log(wave)/4f + 6.6f);     //Look at the funcs
            fasts = Mathf.RoundToInt(wave*Mathf.Log(wave)/4f + 4.4f);
            heavies = Mathf.RoundToInt(wave*Mathf.Log(wave)/10f + 1f);
            //This can increase linearly cuz its boss
            bosses = Mathf.FloorToInt((wave-2)/4 - 1);                      //Increase by 1 every 4 waves

            dmgCoeff = 4f*Mathf.Log(wave)-4f*Mathf.Log(waveScaleFrom)+1f;   //Increases enemy damage

            //The intention is to grow faster than linear
            //Has O(x^2)
            foreach (GameObject tower in bank.towerToCost.Keys.ToArray())
            {
                //Change towerToCost instead of Catalog directly to avoid decimal points while keeping int values
                bank.towerToCost[tower] += (2f*wave - 1f)/100;

                /*
                Working:
                f(w) = w^2/100 + org
                f(10) = org
                f(w-1) = w^2/100 + (1-2w)/100 + org
                f(w-1) = f(x) + (1-2w)/100
                f(x) = f(w-1) + (2w-1)/100
                */

                //This way, I dont need to store seperate values for each tower and they can all scale with just 1 func

                bank.AssignCatalog(tower.name, Mathf.Round(bank.towerToCost[tower]));
            }
        } else //Behaviour for the first 9 waves
        {
            normals = 2 + wave; //Number of normals to spawn scale linearly with wave

            fasts = wave >= 2 ? wave : 0; //Number of fasts scale linearly with wave STARTING from wave 2

            heavies = wave >= 3 ? wave / 3 : 0; //Number of heavies scale linearly with wave starting from wave 3

            if(wave == 5)
            {
                bosses = 1;
            } else
            {
                bosses = 0;
            }
        }

        //Actually spawn the stuff
        for (int i = 0; i < normals && flow.IsRunning; i++) { flow.Spawn(GameFlow.EnemyKind.Normal, centre, arc).GetComponent<EnemyBrain>().attackDamage *=  dmgCoeff; yield return new WaitForSeconds(spawnDelays[GameFlow.EnemyKind.Normal]); }
        for (int i = 0; i < heavies && flow.IsRunning; i++) { flow.Spawn(GameFlow.EnemyKind.Heavy, centre, arc).GetComponent<EnemyBrain>().attackDamage *=  dmgCoeff; yield return new WaitForSeconds(spawnDelays[GameFlow.EnemyKind.Heavy]); }
        for (int i = 0; i < fasts && flow.IsRunning; i++) { flow.Spawn(GameFlow.EnemyKind.Fast, centre, arc).GetComponent<EnemyBrain>().attackDamage *=  dmgCoeff; yield return new WaitForSeconds(spawnDelays[GameFlow.EnemyKind.Fast]); }
        for (int i = 0; i < bosses && flow.IsRunning; i++) { flow.Spawn(GameFlow.EnemyKind.Boss, centre, arc).GetComponent<EnemyBrain>().attackDamage *=  dmgCoeff; yield return new WaitForSeconds(spawnDelays[GameFlow.EnemyKind.Boss]); }
    }
}
