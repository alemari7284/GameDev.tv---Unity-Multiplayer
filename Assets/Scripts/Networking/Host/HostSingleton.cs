using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

/// <summary>
/// Controparte di <see cref="ClientSingleton"/> (FLUSSO 66-68) lato host: stesso
/// identico pattern, ma per <see cref="HostGameManager"/>. Istanziato SEMPRE da
/// ApplicationController (FLUSSO 64), anche per un'istanza che al momento e' solo
/// un client, cosi' e' gia' pronto se questa istanza dovesse ospitare una partita.
/// </summary>
public class HostSingleton : MonoBehaviour
{
    // [FLUSSO 78] Stesso pattern singleton "lazy" di ClientSingleton (FLUSSO 66):
    // stessa nota, non ancora letto da nessun altro script del progetto.
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

    // [FLUSSO 82] Proprieta' pubblica (prima era un campo privato, illeggibile da fuori
    // questa classe): serve a MainMenu.StartHost (FLUSSO 83) per raggiungere
    // HostGameManager.StartHostAsync (FLUSSO 85-89) dall'esterno tramite
    // HostSingleton.Instance.GameManager. Si allinea al pattern gia' usato da
    // ClientSingleton.gameManager (FLUSSO 66/68), che pero' era gia' pubblica fin
    // dall'inizio proprio in previsione di un uso futuro dal Menu. Il "private set"
    // impedisce comunque a chiunque, fuori da questa classe, di sostituire l'istanza:
    // solo createHost() (sotto) puo' assegnarla.
    public HostGameManager GameManager { get; private set; }

    // Start is called before the first frame update
    private void Start()
    {
        // [FLUSSO 79] Stesso motivo del DontDestroyOnLoad in ClientSingleton (FLUSSO 67).
        DontDestroyOnLoad(gameObject);
    }

    // [FLUSSO 80] A differenza di ClientSingleton.createClient (FLUSSO 68), qui non
    // c'e' ancora nessuna logica asincrona da avviare: HostGameManager e' per ora
    // una classe vuota (FLUSSO 81), quindi createHost si limita a istanziarla.
    public void createHost()
    {
        GameManager = new HostGameManager();
    }



}
