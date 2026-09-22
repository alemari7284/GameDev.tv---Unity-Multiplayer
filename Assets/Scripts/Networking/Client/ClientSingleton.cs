using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

/// <summary>
/// MonoBehaviour "contenitore" per il lato client del bootstrap: esiste solo per
/// dare un aggancio nella scena (DontDestroyOnLoad, Inspector) a <see cref="ClientGameManager"/>,
/// che e' una classe C# pura e quindi non potrebbe vivere da sola nella scena.
/// Istanziato una sola volta da ApplicationController.
/// </summary>
public class ClientSingleton : MonoBehaviour
{
    private static ClientSingleton instance;
    public static ClientSingleton Instance
    {
        get
        {
            if (instance != null) return instance;

            instance = FindAnyObjectByType<ClientSingleton>();
            if (instance == null)
            {
                Debug.LogError("No ClientSingleton in the scene");
                return null;
            }
            return instance;
        }
    }

    public ClientGameManager gameManager { get; private set; }

    private void Start()
    {
        DontDestroyOnLoad(gameObject);
    }

    public async Task<bool> createClient()
    {
        gameManager = new ClientGameManager();
        return await gameManager.initAsync();
    }
}
