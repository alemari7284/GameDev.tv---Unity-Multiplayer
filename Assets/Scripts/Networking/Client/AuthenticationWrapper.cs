using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Services.Authentication;
using UnityEngine;

public static class AuthenticationWrapper
{
    public static AuthState authState { get; private set; }

    public static async Task<AuthState> doAuth(int maxTries = 5)
    {
        if (authState == AuthState.Authenticated) return authState;

        authState = AuthState.Authenticating;

        int tries = 0;
        while (authState == AuthState.Authenticating && tries < maxTries)
        {
            await AuthenticationService.Instance.SignInAnonymouslyAsync();
            if (AuthenticationService.Instance.IsSignedIn && AuthenticationService.Instance.IsAuthorized)
            {
                authState = AuthState.Authenticated;
                break;
            }
            tries++;
            await Task.Delay(1000);
        }
        return authState;

    }

}

public enum AuthState
{
    NonAuthenticated,
    Authenticating,
    Authenticated,
    Error,
    Timeout
}