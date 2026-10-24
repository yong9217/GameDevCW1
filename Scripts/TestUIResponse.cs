using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

public class TestUIResponse : MonoBehaviour
{
    PanelRenderer pr;
    [SerializeField] public Dictionary<int,GameObject> prefabsToButton;
    public MouseStateHandler msh;

    void Awake()
    {
        pr = GetComponent<PanelRenderer>();
        pr.RegisterUIReloadCallback(OnUIReload);
    }

    void OnUIReload(PanelRenderer pr, VisualElement root, int ver)
    {
        List<Button> buts = root.Query<Button>().ToList();

        //For some reason each button gets overwritten

        for (int i = 0; i < buts.Count; i++)
        {
            buts[i].clicked += () =>
            {
                Debug.Log($"Clicked button {i}");

                if(prefabsToButton.TryGetValue(i, out GameObject prefab))
                {
                    msh.towerPrefab = prefab;
                } else
                {
                    Debug.LogWarning($"No prefab assigned to button {i}");
                }
            };
        }
    }
}
