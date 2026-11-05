using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

public class GameEnd : MonoBehaviour
{
    public LostStylist lostUI;
    public ParticleSystem lostFXs;
    public MouseStateHandler msh;

    public PanelRenderer winUI;
    public List<ParticleSystem> winFXs;

    public void CueDeath()
    {
        msh.active = false;
        lostFXs.Play();

        lostUI.stylist.Show();
    }

    public void CueWin()
    {
        foreach (ParticleSystem ps in winFXs)
        {
            ps.Play();
        }

        // winUI.Show();
    }
}
