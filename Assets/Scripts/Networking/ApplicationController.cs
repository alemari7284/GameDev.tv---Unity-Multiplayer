using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

/// <summary>
/// Vero e proprio punto di ingresso della rete: vive nella scena "NetBootstrap",
/// caricata per prima all'avvio del gioco, PRIMA di qualunque scena con gameplay
/// o UI (es. ConnectionButtons, §2 nella guida). Il suo unico compito e' capire
/// se questa istanza e' un dedicated server o un giocatore, e nel secondo caso
/// autenticarsi presso Unity Gaming Services prima di lasciar entrare nel menu.
/// </summary>

public class ApplicationController : MonoBehaviour
{
    [SerializeField] private ClientSingleton clientPrefab;
    [SerializeField] private HostSingleton hostPrefab;
    // Start is called before the first frame update
    private async Task Start()
    {
        DontDestroyOnLoad(gameObject);
        bool isDedicatedServer = SystemInfo.graphicsDeviceType ==
            UnityEngine.Rendering.GraphicsDeviceType.Null; //a ded. server has no graphics
        await launchInMode(isDedicatedServer);
    }

    private async Task launchInMode(bool isDedicatedServer)
    {
        if (isDedicatedServer)
        {
            // Ramo dedicated server: ancora uno stub vuoto ("sticazzi per ora").
            // Un dedicated server headless non deve autenticarsi come giocatore ne'
            // mostrare un menu: qui andra' in futuro l'avvio diretto della sessione
            // di rete lato server
        }
        else
        {
            HostSingleton hostSingleton = Instantiate(hostPrefab);
            hostSingleton.createHost();

            ClientSingleton clientSingleton = Instantiate(clientPrefab);
            bool authenticated = await clientSingleton.createClient();

            if (authenticated)
            {
                clientSingleton.gameManager.goToMenu();
            }
        }
    }
}
