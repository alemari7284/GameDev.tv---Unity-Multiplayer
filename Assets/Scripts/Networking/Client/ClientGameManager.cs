using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Services.Core;
using UnityEngine;
using UnityEngine.SceneManagement;

public class ClientGameManager
{
    private const string menuSceneName = "Menu";
    public async Task<bool> initAsync()
    {
        await UnityServices.InitializeAsync();

        AuthState authState = await AuthenticationWrapper.doAuth();

        if (authState == AuthState.Authenticated)
        {
            return true;
        }
        return false;
    }

    public void goToMenu()
    {
        SceneManager.LoadScene(menuSceneName);
    }
}
