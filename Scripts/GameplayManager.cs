using UnityEngine;

public class GameplayManager : MonoBehaviour
{
    public SpeechEngine speak;
    public WaveHandler wave;
    int nextWave = 0;

    void Start()
    {
        wave.OnWaveEnd(WaveChecks);
        Next();

        nextWave++;
    }

    void WaveChecks()
    {
        nextWave++;
        if(nextWave == 2)
        {
            //Need some event / check to run Next()
        }
    }

    public void Next()
    {
        wave.StartWave(nextWave);
        speak.NextSpeech();
    }
}
