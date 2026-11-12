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

    void Start() => StartCoroutine(RunWaves());

    IEnumerator RunWaves()
    {
        yield return new WaitForSeconds(firstWaveDelay);
        int wave = 10;
        while (flow.IsRunning)
        {
            wave++;
            StartCoroutine(SpawnWave(wave));

            //Put some msg about next wave in... OR we keep permanently in timer

            yield return new WaitForSeconds(secondsBetweenWaves);
        }
    }

    // Each wave is a little bigger and comes from a wider arc than the one before.

    //Hangles each wave spawn, using ONLY wave number. Have to scale accordingly
    IEnumerator SpawnWave(int wave)
    {
        float centre = Random.Range(0f, 360f); //Choose a random direction

        float arc = Mathf.Min(360f, 60f + wave * 30f); //Choose some random deviation from direction, scaling linearly with wave

        int bosses, normals, fasts, heavies;

        if(wave > 9)
        {
            normals = Mathf.RoundToInt(wave*Mathf.Log(wave)/4f + 6.6f);
            fasts = Mathf.RoundToInt(wave*Mathf.Log(wave)/4f + 4.4f);
            heavies = Mathf.RoundToInt(wave*Mathf.Log(wave)/10f + 1f);
            bosses = Mathf.FloorToInt((wave-2)/4 - 1);

            foreach (GameObject tower in bank.towerToCost.Keys.ToArray())
            {
                bank.towerToCost[tower] += (2f*wave - 1f)/100;

                bank.AssignCatalog(tower.name, Mathf.Round(bank.towerToCost[tower]));

                Debug.Log(bank.PrintCatalog());

                /*
                Working:
                f(w) = w^2/100 + org
                f(10) = org
                f(w-1) = w^2/100 + (1-2w)/100 + org
                f(w-1) = f(x) + (1-2w)/100
                f(x) = f(w-1) + (2w-1)/100
                */

                //This way, I dont need to store seperate values for each tower and they can all scale with just 1 func
            }
        } else
        {
            normals = 2 + wave; //Number of normals to spawn scale linearly with wave

            fasts = wave >= 2 ? wave : 0; //Number of fasts scale linearly with wave STARTING from wave 2

            heavies = wave >= 3 ? wave / 3 : 0; //Number of heavies scale linearly with wave starting from wave 3

            if(wave == 5)
            {
                bosses = 1;
            } else if(wave == 8)
            {
                bosses = 1;
            } else
            {
                bosses = 0;
            }
        }

        for (int i = 0; i < normals && flow.IsRunning; i++) { flow.Spawn(GameFlow.EnemyKind.Normal, centre, arc); yield return new WaitForSeconds(1.2f); }
        for (int i = 0; i < heavies && flow.IsRunning; i++) { flow.Spawn(GameFlow.EnemyKind.Heavy, centre, arc); yield return new WaitForSeconds(0f); }
        for (int i = 0; i < fasts && flow.IsRunning; i++) { flow.Spawn(GameFlow.EnemyKind.Fast, centre, arc); yield return new WaitForSeconds(.5f); }
        for (int i = 0; i < bosses && flow.IsRunning; i++) { flow.Spawn(GameFlow.EnemyKind.Boss, centre, arc); yield return new WaitForSeconds(3f); }
    }
}
