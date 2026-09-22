using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

/// <summary>
/// Controparte di <see cref="ClientSingleton"/> lato host: stesso
/// identico pattern, ma per <see cref="HostGameManager"/>. Istanziato SEMPRE da
/// ApplicationController, anche per un'istanza che al momento e' solo
/// un client, cosi' e' gia' pronto se questa istanza dovesse ospitare una partita.
/// </summary>
public class HostSingleton : MonoBehaviour
{
    private static HostSingleton instance;
    public static HostSingleton Instance
    {
        get
        {
            if (instance != null) return instance;

            instance = FindAnyObjectByType<HostSingleton>();
            if (instance == null)
            {
                Debug.LogError("No HostSingleton in the scene");
                return null;
            }
            return instance;
        }
    }

    public HostGameManager GameManager { get; private set; }

    // Start is called before the first frame update
    private void Start()
    {
        DontDestroyOnLoad(gameObject);
    }

    public void createHost()
    {
        GameManager = new HostGameManager();
    }



}
