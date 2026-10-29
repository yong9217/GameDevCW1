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
        root.RegisterCallback<MouseOverEvent>(e =>
        {
            Debug.Log("Hovered!");
            msh.active = false;
        });
        root.RegisterCallback<MouseOutEvent>(e =>
        {
            Debug.Log("Left!");
            msh.Activate();
        });

        List<Button> buts = root.Query<Button>().ToList();

        //For some reason each button gets overwritten

        //Bcuz i is referenced instead of valued for some reason????

        for (int i = 0; i < buts.Count; i++)
        {
            int k = i;

            buts[i].clicked += () =>
            {
                Debug.Log($"Clicked button {buts[k].name}");

                if(prefabsToButton.TryGetValue(k, out GameObject prefab))
                {
                    msh.towerPrefab = prefab;
                } else
                {
                    Debug.LogWarning($"No prefab assigned to button {buts[k].name}");
                }
            };
        }
    }
}
