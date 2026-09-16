using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Services.Authentication;
using Unity.Services.Core;
using UnityEngine;

/// <summary>
/// Wrapper static (non serve un'istanza: e' un servizio unico, globale a tutto il
/// processo) attorno a Unity Authentication Service. Espone una piccola macchina a
/// stati (<see cref="AuthState"/>, FLUSSO 77) per gestire in modo sicuro chiamate
/// concorrenti a doAuth (es. piu' script che vogliono "assicurarsi" di essere
/// autenticati prima di procedere).
/// </summary>
// [FLUSSO 71] Essendo la classe "static", authState non appartiene a nessuna
// istanza: e' un singolo valore condiviso per l'intero processo, che sopravvive
// ai cambi scena esattamente come gli oggetti DontDestroyOnLoad (FLUSSO 61/67).
public static class AuthenticationWrapper
{
    public static AuthState authState { get; private set; }

    // [FLUSSO 72] Prima guardia: se ci si e' gia' autenticati in precedenza (es. un
    // secondo giocatore locale, o una chiamata doppia), si ritorna subito senza
    // ripetere l'handshake con il servizio.
    public static async Task<AuthState> doAuth(int maxTries = 5)
    {
        if (authState == AuthState.Authenticated) return authState;

        // [FLUSSO 73] Seconda guardia: se un'altra chiamata a doAuth e' GIA' in corso
        // (authState e' stato impostato ad Authenticating da qualche altro chiamante),
        // questa chiamata non ne avvia una seconda in parallelo: si limita ad aspettare
        // passivamente l'esito tramite authenticating() (FLUSSO 75) e a restituirlo.
        if (authState == AuthState.Authenticating)
        {
            Debug.LogWarning("Already authenticating!");
            await authenticating();
            return authState;
        }

        // [FLUSSO 74] FIX: qui prima c'era un ciclo duplicato (quasi identico a
        // SignInAnonimouslyAsync, FLUSSO 76) che pero' non impostava mai
        // "authState = AuthState.Authenticating;" prima del while: alla primissima
        // chiamata authState valeva ancora NonAuthenticated, la condizione del while
        // era quindi FALSA fin da subito e il login non partiva mai. Invece di
        // duplicare la logica una seconda volta qui, ora si delega direttamente al
        // metodo completo SignInAnonimouslyAsync (FLUSSO 76), che imposta
        // Authenticating correttamente PRIMA del ciclo e gestisce anche le eccezioni.
        await SignInAnonimouslyAsync(maxTries);
        return authState;
    }

    // [FLUSSO 75] Helper di attesa passiva: usato solo dal ramo FLUSSO 73. Fa
    // polling ogni 200ms finche' lo stato non esce da Authenticating/NonAuthenticated
    // (cioe' finche' non diventa Authenticated, Error o Timeout), invece di avviare
    // un secondo tentativo di login concorrente con quello gia' in corso altrove.
    private static async Task<AuthState> authenticating()
    {
        while (authState == AuthState.Authenticating || authState == AuthState.NonAuthenticated)
        {
            await Task.Delay(200);
        }
        return authState;
    }

    // [FLUSSO 76] Vera logica di login, ora richiamata da doAuth (FLUSSO 74):
    // imposta subito AuthState.Authenticating (cosa che PRIMA mancava in doAuth),
    // poi ritenta il login fino a maxRetries volte, gestendo le eccezioni
    // specifiche di Authentication/RequestFailed impostando AuthState.Error, e
    // segnando AuthState.Timeout se i tentativi si esauriscono senza successo.
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


    // [FLUSSO 77] I 5 stati possibili della macchina a stati. NonAuthenticated e'
    // il default C# per un enum (valore 0), quindi e' anche il valore iniziale di
    // authState prima di qualunque chiamata a doAuth.
    public enum AuthState
    {
        NonAuthenticated,
        Authenticating,
        Authenticated,
        Error,
        Timeout
    }
}