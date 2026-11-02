using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class WaveHandler : MonoBehaviour
{
    Dictionary<int,List<Spawner>> waves = new Dictionary<int, List<Spawner>>();

    int curWave = -1;

    public void Register(Spawner s)
    {
        if(s.waveNum < 0)
        {
            Debug.LogError("Unable to have negative wave numbers! Rejecting spawner");
        } else
        {
            if(!waves.TryAdd(s.waveNum, new List<Spawner> { s }))
            {
                waves[s.waveNum].Add(s);
            }
        }
    }

    public void startWave(int n)
    {
        int ended = endWave();
        if(ended >= 0)
        {
            Debug.Log($"Ending wave {ended}");
        }

        if(waves.TryGetValue(n, out List<Spawner> ss))
        {
            foreach (Spawner sp in ss)
            {
                sp.Activate();
            }
        } else
        {
            Debug.LogError($"No wave indexed {n}");
        }
    }

    //Only checks that the wave exists & resets the current wave
    public bool forceEndWave(int n)
    {
        if(waves.TryGetValue(n, out List<Spawner> ss))
        {
            foreach (Spawner sp in ss)
            {
                sp.End();
            }

            curWave = -1;

            return true;
        }

        return false;
    }

    //Checks that the current wave exists and is actually active
    //Returns the wave ended
    public int endWave()
    {
        if (waves.TryGetValue(curWave, out List<Spawner> ss))
        {
            if (ss.First().isActive())
            {
                forceEndWave(curWave);

                int ended = curWave;

                curWave = -1;

                return ended;
            } else
            {
                Debug.LogWarning($"Wave {curWave} was started but spawners weren't activated! Resetting current wave");

                curWave = -1;
            }
        }

        return -1;
    }
}
