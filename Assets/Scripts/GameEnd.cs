using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

public class GameEnd : MonoBehaviour
{
    // Old defeat screen; only used when no defeat EndScreenUI is assigned.
    public LostStylist lostUI;
    public ParticleSystem lostFXs;
    public MouseStateHandler msh;

    public PanelRenderer winUI;
    public List<ParticleSystem> winFXs;

    [Header("Parchment end screens")]
    public EndScreenUI defeatScreen;
    public EndScreenUI victoryScreen;

    bool over;

    // True once the castle has fallen. The pause menu checks it so Esc cannot un-freeze a lost game.
    public static bool IsDefeated { get; private set; }

    void Awake() => IsDefeated = false;

    public void CueDeath()
    {
        if (over) return;
        over = true;
        IsDefeated = true;
        msh.locked = true;
        // Freeze the whole game behind the defeat screen. Try again / Main menu set the time scale back to 1.
        Time.timeScale = 0f;
        if (lostFXs)
        {
            var fx = lostFXs.main;
            fx.useUnscaledTime = true; // keep the defeat effect animating while the game is frozen
            lostFXs.Play();
        }

        if (defeatScreen) defeatScreen.Show();
        else if (lostUI) lostUI.stylist.Show();
    }

    public void CueWin()
    {
        if (over) return;
        over = true;
        msh.locked = true;
        Debug.Log("All waves beaten. You win!");

        foreach (ParticleSystem ps in winFXs)
        {
            if (ps) ps.Play();
        }

        if (victoryScreen) victoryScreen.Show();
    }
}
