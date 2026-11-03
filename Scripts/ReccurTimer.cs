using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class ReccurTimer : MonoBehaviour
{
    //All time units in seconds
    float curTicks = 0f;
    float resetThreshold = 0f;

    //Number of cycles to keep track of total time elapsed
    int nCycles = 0;

    //Global pause timer
    public bool paused;

    //Currently executing tasks
    SortedList<(float,float), TimeNode> tasks = new SortedList<(float,float), TimeNode>();
    //Paused tasks
    Dictionary<string, PausedTimeTask> pausedTasks = new Dictionary<string, PausedTimeTask>();

    //Initialize the cycle threshold
    public void Init(float t)
    {
        if(t <= 0)
        {
            Debug.LogError("Unable to have a 0 or negative reset threshold");
        } else
        {
            resetThreshold = t;
        }
    }

    //Call to begin a periodic task
    /*
    every - Execute every ___ seconds
    ac - task to execute (MUST return void)
    name - Name of task. For pausing and resuming
    after - Start execution after some delay

    !IMPORTANT!
    Since name uniqueness is not enforced on Add, thus Pause, Resume and End may unintentionally end the wrong task
    */
    public void OnTimeInvoke(float every, Action ac, string name = "default", float after = 0f, int nTimes = -1)
    {
        if(!tasks.TryAdd((every,after), new TimeNode(ac, name, after, nTimes)))
        {
            tasks[(every,after)].tasks.Add(new TimeTask(ac));
        }
    }

    public void InvokeOnce(float wait, Action ac, string name = "defualt")
    {
        OnTimeInvoke(resetThreshold, ac, name, wait);
    }

    //Call to pause the named task

    //To pause a task, remove its task from TimeNode and keep it with other paused tasks
    public string Pause(string name)
    {
        //Output
        string key = null; //Task name to resume

        //Look for the specified task
        bool found = false; //Just used to break outer loop
        (float,float) nodeKey = (-1,-1); //Used to check for TimeNode removal later
        
        foreach (KeyValuePair<(float,float), TimeNode> tn in tasks)
        {
            if (!found)
            {
                foreach (TimeTask tk in tn.Value.tasks)
                {
                    //Found task
                    if(tk.name == name)
                    {
                        PausedTimeTask pt = tn.Value.Pause(tk.name, tn.Key.Item1, tn.Value.nextCall-curTicks); //Will also remove from TimeNode (tn.Value)

                        nodeKey = tn.Key; //Keep TimeNode to check later

                        //Ensure no duplicate names
                        key = pt.name;
                        int n = 0;
                        bool dupe = false;
                        while (pausedTasks.ContainsKey(key)) //IF there is duplicate
                        {
                            //Do the file thing of ...(1) ...(2) until an unused int is found
                            dupe = true;
                            n++;
                            key = pt.name + n;
                        }
                        pt.name = key; //update the name if required

                        if (dupe)
                        {
                            Debug.Log($"Duplicate task exists. Using {key} as name");
                        }
                        
                        //Add to paused tasks
                        pausedTasks.Add(key, pt);
                        found = true;
                        break;
                    }
                }
            } else
            {
                break;
            }
        }

        //Remove TimeNode if no longer has any tasks in it ONLY if task was paused
        if (found)
        {
            if(tasks[nodeKey].tasks.Count <= 0)
            {
                tasks.Remove(nodeKey);
            }
        }

        return key;
    }

    //Resume a paused task

    //Just build* a new TimeNode instead of trying to slot it back in
    //*Most of the time its built, there is the offchance a TimeNode with the exact period and offset exists
    public void Resume(string name)
    {
        if(pausedTasks.TryGetValue(name, out PausedTimeTask pt))
        {
            OnTimeInvoke(pt.period, pt.task, pt.name, pt.localOffset + curTicks); //localOffset + curticks to resume from where we left off
            pausedTasks.Remove(name);
        } else
        {
            Debug.LogError($"Could not find paused task named {name}");
        }
    }

    //End a task
    public void EndTask(string name)
    {
        string tkey;
        //Already paused
        if (pausedTasks.ContainsKey(name))
        {
            tkey = name;
        } else
        {
            //Pause first then remove from pause list
            tkey = Pause(name);

            if(tkey == null)
            {
                Debug.LogError($"Could not find task called {name}");
                return;
            }
        }

        pausedTasks.Remove(tkey);

        Debug.Log($"Ended repeating task {name}");
    }

    // Update is called once per frame
    void Update()
    {
        if (!paused)
        {
            float dTime = Time.deltaTime;
            curTicks += dTime;

            //Format curTick AND all nextCalls to match reset values
            if(curTicks >= resetThreshold)
            {
                curTicks -= resetThreshold * Mathf.Floor(curTicks/resetThreshold);

                foreach (KeyValuePair<(float,float), TimeNode> kv in tasks)
                {
                    kv.Value.Reset(resetThreshold);
                }

                nCycles++;

                Debug.Log("Reset!");
            }

            //Execute TimeNodes if needed
            foreach ((float,float) k in tasks.Keys.ToList())
            {
                TimeNode node = tasks[k];

                if(curTicks > node.nextCall) //Need to execute?
                {
                    node.nextCall += k.Item1; //Increment to next call
                    foreach (TimeTask t in node.tasks)
                    {
                        if(t.remTimes > 0)
                        {
                            t.Execute();
                        }
                    }
                }
            }
        }
    }

    public override string ToString()
    {
        string ot = $"Timer ({(paused ? "||" : "|>")}) at {curTicks} / {resetThreshold} secs [{nCycles} cycles :: Total elapsed: {nCycles*resetThreshold + curTicks} secs]\n";

        foreach ((float,float) k in tasks.Keys.ToList())
        {
            ot += $"Every {k.Item1} secs offset by {k.Item2} secs:\n";
            ot += tasks[k];
            ot += "\n";
        }

        return ot;
    }
}

//Represents a group of tasks that execute at the same time
class TimeNode
{
    public List<TimeTask> tasks;
    public float nextCall;
    //float period     Comes from Dictionary key in ReccurTimer
    //float offset

    public TimeNode(Action ac, string name = "default", float offset = 0f, int nTimes = -1)
    {
        tasks = new List<TimeTask>{new TimeTask(ac,name,nTimes)};
        nextCall = offset;
    }

    //Ensure next call matches current time slice
    public void Reset(float resT)
    {
        nextCall -= resT;
    }

    //Removes the indexed task and makes a PausedTimeTask

    //Hence period and offset comes from ReccurTimer
    PausedTimeTask Pause(int ix, float every, float offset)
    {
        if(ix >= 0 && ix < tasks.Count)
        {
            PausedTimeTask paused = TimeTask.Pause(tasks[ix], every, offset);
            tasks.RemoveAt(ix);
            return paused;
        } else
        {
            if(ix == -1)
            {
                Debug.LogError("Could not find the named task!");
            } else
            {
                Debug.LogError("Index out of range! No task to pause");
            }
            return null;
        }
    }

    //Removes the specified task and makes a PausedTimeTask
    public PausedTimeTask Pause(string task, float every, float offset)
    {
        return Pause(tasks.FindIndex(tt => tt.name == task), every, offset);
    }

    public override string ToString()
    {
        string ot = $"TimeNode {tasks.Count} tasks | nextCall: {nextCall}\n";

        foreach (TimeTask item in tasks)
        {
            ot += item;
            ot += "\n";
        }

        return ot;
    }
}

//A task to execute periodically
class TimeTask
{
    public Action task;
    public string name;
    public int remTimes;
    
    public TimeTask(Action ac, string n = "default", int t = -1)
    {
        task = ac;
        name = n;
        remTimes = t;
    }

    public void Execute()
    {
        task.Invoke();
        remTimes--;
    }

    public override string ToString()
    {
        return $"Task {name} |> ({remTimes} calls left)";
    }

    //Pause the task
    public static PausedTimeTask Pause(TimeTask tt, float every, float off)
    {
        return new PausedTimeTask(tt, every, off);
    }
}

//Stores information about a paused periodic task
class PausedTimeTask : TimeTask
{
    public float localOffset;
    public float period;

    public PausedTimeTask(Action ac, float p, string n = "default", float off = 0f) : base(ac, n)
    {
        period = p;
        localOffset = off;
    }

    public PausedTimeTask(TimeTask tt, float p, float off = 0f) : this(tt.task, p, tt.name, off){}

    public override string ToString()
    {
        return $"Task {name} || [Rem: {remTimes}, every: {period}, localOffset: {localOffset}]";
    }
}