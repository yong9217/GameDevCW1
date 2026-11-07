using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class WaveHandler : MonoBehaviour
{
    public UIResponder uir;

    Dictionary<int,Wave> waves = new Dictionary<int, Wave>();

    int curWave = -1;
    public int maxWaves;

    public Debugger debug;
    public int executeWave;

    void Awake()
    {
        debug.Add("2", () =>
        {
            Debug.Log(waves.ContainsKey(executeWave));
            DebugDict();
        }, "ManualStart");
    }

    public void DebugDict()
    {
        foreach (Wave w in waves.Values)
        {
            Debug.Log(w);
        }
    }

    public void Register(Spawner s)
    {
        if(s.waveNum < 0)
        {
            Debug.LogError("Unable to have negative wave numbers! Rejecting spawner");
        } else
        {
            if(!waves.TryAdd(s.waveNum, new Wave(s)))
            {
                waves[s.waveNum].spawners.Add(s);
            }
        }
    }

    public void StartWave(int n)
    {
        EndWave();

        if(waves.TryGetValue(n, out Wave ss))
        {
            foreach (Spawner sp in ss.spawners)
            {
                sp.Activate();
            }

            curWave = n;
        } else
        {
            Debug.LogError($"No wave indexed {n} to start");
        }
    }

    //Only checks that the wave exists & resets the current wave
    public bool ForceEndWave(int n)
    {
        if(waves.TryGetValue(n, out Wave ss))
        {
            foreach (Spawner sp in ss.spawners)
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
    public int EndWaveOnly()
    {
        if (waves.TryGetValue(curWave, out Wave ss))
        {
            int ended;

            if (ss.spawners.Count > 0)
            {
                Debug.LogWarning($"Wave {curWave} still has active spawners! Force ending...");

                ForceEndWave(curWave);

                ended = curWave;
                curWave = -1;
            } else
            {
                Debug.Log($"Ending wave {curWave}");

                ended = curWave;
                curWave = -1;
            }

            Debug.Log($"Invoking on end for wave {ended}");
            
            ss.Invoke();

            return ended;
        } else
        {
            Debug.LogError($"Could not find wave {curWave} in waves");
        }

        return -1;
    }

    public void Remove(Spawner s)
    {
        if(waves.TryGetValue(s.waveNum, out Wave ls))
        {
            ls.spawners.Remove(s);

            if(ls.spawners.Count <= 0)
            {
                Debug.Log("No more active spawners. Ending wave");
                EndWave();
            }
        }
    }

    public void EndWave()
    {
        int waveEnded = EndWaveOnly();

        if(waveEnded < 0)
        {
            Debug.LogWarning($"Could not find wave {curWave} to end!");
        } else
        {
            UpdateHUD(waveEnded);
        }
    }

    public void OnWaveEnd(int waveNum, Action ac)
    {
        if(waves.TryGetValue(waveNum, out Wave wave))
        {
            wave.OnWaveEnd(ac);
        } else
        {
            Debug.LogError($"Unable to find wave {waveNum}");
        }
    }

    public void ResetWaveEndAction(int waveNum)
    {
        OnWaveEnd(waveNum, () => {});
    }

    void UpdateHUD(int waveEnded)
    {
        uir.UpdateWaveMeter((maxWaves - waveEnded)/maxWaves);
    }
    
}

class Wave
{
    Action onWaveEnd;
    public List<Spawner> spawners;

    public Wave(Spawner s)
    {
        onWaveEnd = () => {};
        spawners = new List<Spawner>{s};
    }

    public Wave(Spawner s, Action ac) : this(s)
    {
        if(ac == null)
        {
            Debug.LogWarning("Cannot initialize wave with null OnWaveEnd action!");
        } else
        {
            onWaveEnd = ac;
        }
    }

    public void OnWaveEnd(Action ac)
    {
        onWaveEnd = ac;
    }

    public void Invoke()
    {
        onWaveEnd.Invoke();
    }

    public override string ToString()
    {
        string ot = $"spawners.count = {spawners.Count} [";

        foreach (Spawner sp in spawners)
        {
            ot += $"{sp}, ";
        }

        ot += "]\n";

        return ot;
    }
}