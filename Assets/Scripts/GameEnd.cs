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

    public void CueDeath()
    {
        if (over) return;
        over = true;
        msh.locked = true;
        if (lostFXs) lostFXs.Play();

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
