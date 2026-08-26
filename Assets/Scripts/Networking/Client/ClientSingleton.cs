using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

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

    // Start is called before the first frame update
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
