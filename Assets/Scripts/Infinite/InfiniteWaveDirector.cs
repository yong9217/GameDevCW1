using System.Collections;
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

    InfiniteFlow flow;

    void Awake() => flow = GetComponent<InfiniteFlow>();

    void Start() => StartCoroutine(RunWaves());

    IEnumerator RunWaves()
    {
        yield return new WaitForSeconds(firstWaveDelay);
        int wave = 0;
        while (flow.IsRunning)
        {
            wave++;
            StartCoroutine(SpawnWave(wave));
            yield return new WaitForSeconds(secondsBetweenWaves);
        }
    }

    // Each wave is a little bigger and comes from a wider arc than the one before.
    IEnumerator SpawnWave(int wave)
    {
        float centre = Random.Range(0f, 360f);
        float arc = Mathf.Min(360f, 60f + wave * 30f);
        int normals = 2 + wave;
        int fasts = wave >= 2 ? wave : 0;
        int heavies = wave >= 3 ? wave / 3 : 0;
        bool boss = wave % 8 == 0;

        for (int i = 0; i < normals && flow.IsRunning; i++) { flow.Spawn(GameFlow.EnemyKind.Normal, centre, arc); yield return new WaitForSeconds(1.2f); }
        for (int i = 0; i < heavies && flow.IsRunning; i++) { flow.Spawn(GameFlow.EnemyKind.Heavy, centre, arc); yield return new WaitForSeconds(2f); }
        for (int i = 0; i < fasts && flow.IsRunning; i++) { flow.Spawn(GameFlow.EnemyKind.Fast, centre, arc); yield return new WaitForSeconds(.5f); }
        if (boss && flow.IsRunning) flow.Spawn(GameFlow.EnemyKind.Boss, centre, arc);
    }
}
