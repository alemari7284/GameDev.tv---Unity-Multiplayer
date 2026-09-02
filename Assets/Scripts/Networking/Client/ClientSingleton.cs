using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

/// <summary>
/// MonoBehaviour "contenitore" per il lato client del bootstrap: esiste solo per
/// dare un aggancio nella scena (DontDestroyOnLoad, Inspector) a <see cref="ClientGameManager"/>,
/// che e' una classe C# pura e quindi non potrebbe vivere da sola nella scena.
/// Istanziato una sola volta da ApplicationController (FLUSSO 63).
/// </summary>
public class ClientSingleton : MonoBehaviour
{
    // [FLUSSO 66] Pattern singleton "lazy": instance viene cercata in scena solo
    // alla prima richiesta di Instance, non nell'Awake. Attenzione: al momento
    // nessun altro script nel progetto legge ClientSingleton.Instance (ApplicationController
    // tiene invece un riferimento diretto alla variabile locale clientSingleton, FLUSSO 63);
    // questa proprieta' e' pronta per essere usata da script futuri in altre scene
    // (es. il Menu) che vogliano recuperare gameManager senza un riferimento esplicito.
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
        // [FLUSSO 67] Stesso motivo del DontDestroyOnLoad in ApplicationController
        // (FLUSSO 61): questo oggetto deve sopravvivere al cambio scena verso il Menu.
        DontDestroyOnLoad(gameObject);
    }

    // [FLUSSO 68] Crea il ClientGameManager (new, non Instantiate: e' una classe
    // C# pura, non un componente Unity) e gli delega subito l'intera procedura di
    // autenticazione tramite initAsync (FLUSSO 69). Il bool restituito risale la
    // catena fino ad ApplicationController.launchInMode (FLUSSO 63).
    public async Task<bool> createClient()
    {
        gameManager = new ClientGameManager();

        return await gameManager.initAsync();
    }



}
