using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class WaveHandler : MonoBehaviour
{
    public UIResponder uir;

    Dictionary<int,List<Spawner>> waves = new Dictionary<int, List<Spawner>>();

    int curWave = -1;
    public int maxWaves;

    public Debugger debug;
    public int executeWave;

    void Awake()
    {
        debug.Add("2", () =>
        {
            startWave(executeWave);
        }, "ManualStart");
    }

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
        endWave();

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
    //Returns the wave ended OR -1 
    public int endWaveOnly()
    {
        if (waves.TryGetValue(curWave, out List<Spawner> ss))
        {
            if (ss.Count > 0)
            {
                Debug.LogWarning($"Wave {curWave} still has active spawners! Force ending...");

                forceEndWave(curWave);

                int ended = curWave;
                curWave = -1;
                return ended;
            } else
            {
                Debug.Log($"Ending wave {curWave}");

                int ended = curWave;
                curWave = -1;
                return ended;
            }
        }

        return -1;
    }

    public void Remove(Spawner s)
    {
        if(waves.TryGetValue(s.waveNum, out List<Spawner> ls))
        {
            ls.Remove(s);

            if(ls.Count <= 0)
            {
                Debug.Log("No more active spawners. Ending wave");
                endWave();
            }
        }
    }

    public void endWave()
    {
        int waveEnded = endWaveOnly();

        if(waveEnded < 0)
        {
            Debug.LogWarning($"Could not find wave {curWave} to end!");
        } else
        {
            UpdateHUD(waveEnded);
        }
    }

    public void UpdateHUD(int waveEnded)
    {
        uir.UpdateWaveMeter(waveEnded/maxWaves);
    }
}
