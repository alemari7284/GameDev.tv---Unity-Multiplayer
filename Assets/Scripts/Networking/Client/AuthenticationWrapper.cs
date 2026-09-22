using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Services.Authentication;
using Unity.Services.Core;
using UnityEngine;

/// <summary>
/// Wrapper static (non serve un'istanza: e' un servizio unico, globale a tutto il
/// processo) attorno a Unity Authentication Service. Espone una piccola macchina a
/// stati (<see cref="AuthState"/>) per gestire in modo sicuro chiamate
/// concorrenti a doAuth (es. piu' script che vogliono "assicurarsi" di essere
/// autenticati prima di procedere).
/// </summary>

public static class AuthenticationWrapper
{
    public static AuthState authState { get; private set; }

    public static async Task<AuthState> doAuth(int maxTries = 5)
    {
        if (authState == AuthState.Authenticated) return authState;

        if (authState == AuthState.Authenticating)
        {
            Debug.LogWarning("Already authenticating!");
            await authenticating();
            return authState;
        }

        await SignInAnonimouslyAsync(maxTries);
        return authState;
    }

    private static async Task<AuthState> authenticating()
    {
        while (authState == AuthState.Authenticating || authState == AuthState.NonAuthenticated)
        {
            await Task.Delay(200);
        }
        return authState;
    }

    private static async Task SignInAnonimouslyAsync(int maxRetries)
    {
        authState = AuthState.Authenticating;

        int retries = 0;

        while (authState == AuthState.Authenticating && retries < maxRetries)
        {
            try
            {
                await AuthenticationService.Instance.SignInAnonymouslyAsync();
                if (AuthenticationService.Instance.IsSignedIn && AuthenticationService.Instance.IsAuthorized)
                {
                    authState = AuthState.Authenticated;
                    break;
                }
            }
            catch (AuthenticationException ex)
            {
                Debug.LogError(ex);
                authState = AuthState.Error;
            }
            catch (RequestFailedException ex)
            {
                Debug.LogError(ex);
                authState = AuthState.Error;
            }
            retries++;
            await Task.Delay(1000);
        }

        if (authState != AuthState.Authenticated)
        {
            Debug.LogWarning($"Player wasn't signed in succesfully after {retries} retries");
            authState = AuthState.Timeout;
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
}