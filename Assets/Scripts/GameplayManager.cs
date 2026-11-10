using UnityEngine;

public class GameplayManager : MonoBehaviour
{
    public SpeechEngine speak;
    public WaveHandler wave;
    public MouseStateHandler msh;
    public GameEnd end;
    int nextWave = 0;

    bool towerTut = true;

    void Awake()
    {
        ////KEEP THIS IN CHRONOLOGICAL ORDER

        //Tower placing tutorial

        //Play dialogue[0]

        //Wave 0 start (started in Start())

        //Play tower placement tutorial dialogue[1]
        //Pause game for tower placing tutorial
        speak.textBoxes[0].OnSpeechEnd(() =>
        {
            Time.timeScale = 0f; //BE CAREFUL! ReccurTimer won't increment during this time

            //Unlock Arrow

            //Flash Arrow

            //Flash left-click

            //Flash shadow image

            speak.NextSpeech();
        });

        //*Places tower*

        //Resume game once tower placing tutorial ends

        //Play dialogue[2] on placing tutorial tower **Probably should be on enemy die instead
        msh.OnPlace(() =>
        {
            // Only the "place an arrow tower" prompt (dialogue[1], so the next one is [2]) waits for this.
            // A tower built earlier must not skip ahead, or dialogue[0]'s end never runs and the waves stop.
            if (towerTut && nextWave == 1 && speak.NextSpeechKey() == 2)
            {
                speak.NextSpeech();
                Time.timeScale = 1f;
                towerTut = false;
            }
        });

        //Play dialogue[3]
        speak.textBoxes[2].OnSpeechEnd(() =>
        {
            speak.NextSpeech();
        });

        //Wave 1 start
        //Play dialogue[4]
        wave.OnWaveEnd(1,() =>
        {
            Next();
        });
        speak.textBoxes[3].OnSpeechEnd(() =>
        {
            Next();
        });

        //*beats wave 1*

        //Wave 2 start (Wave 2 started by wave[1].OnWaveEnd())
        //Dialogue[5] (Dialogue[5] started by wave[1].OnWaveEnd())
        wave.OnWaveEnd(2,Next);
        speak.textBoxes[5].OnSpeechEnd(() =>
        {
            speak.NextSpeech();

            //Unlock Cannon

            //Flash Cannon
        });

        //Dialogue[6]

        //*beats wave 2*

        //Wave 3 start
        //Dialogue[7]
        wave.OnWaveEnd(3,Next);
        speak.textBoxes[7].OnSpeechEnd(() =>
        {
            speak.NextSpeech();

            //Unlock Magic

            //Flash Magic
        });

        //*beats wave 3*

        //Wave 4 start
        //Dialogue[8]

        //*beats wave 4*

        //Base invokes cueEnd()
    }

    void Start()
    {
        //Wave 0 start
        //Dialogue[0] start
        speak.OnGUILoad(Next);

        //Beating the last wave wins the game (all spawners have registered by now)
        if (!end) end = FindAnyObjectByType<GameEnd>();
        int last = wave.LastWave;
        if (last >= 0 && end)
        {
            wave.OnWaveEnd(last, end.CueWin);
        }
    }

    public void Next()
    {
        wave.StartWave(nextWave);
        speak.NextSpeech();

        nextWave++;
    }
}