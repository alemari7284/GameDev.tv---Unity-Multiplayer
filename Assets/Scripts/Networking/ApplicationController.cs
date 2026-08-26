using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

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

        }
        else
        {
            ClientSingleton clientSingleton = Instantiate(clientPrefab);
            bool authenticated = await clientSingleton.createClient();
            HostSingleton hostSingleton = Instantiate(hostPrefab);
            hostSingleton.createHost();

            if (authenticated)
            {
                clientSingleton.gameManager.goToMenu();
            }
        }
    }
}
