using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Hace que el personaje camine una distancia concreta (2 metros por defecto) usando
/// un <see cref="NavMeshAgent"/> y se detenga al llegar, activando el trigger del
/// Animator mientras avanza.
///
/// El AnimatorController debe tener un trigger con el mismo nombre que
/// <see cref="nombreTriggerCaminar"/> para que la animación de caminar se dispare.
/// </summary>
[RequireComponent(typeof(NavMeshAgent))]
[RequireComponent(typeof(Animator))]
[AddComponentMenu("Enfermeria/Caminar por distancia")]
public class CaminarDosMetros : MonoBehaviour
{
    [Header("Desplazamiento")]
    [Tooltip("Distancia en metros que debe recorrer el personaje hacia adelante.")]
    [SerializeField] private float distanciaMetros = 2f;

    [Tooltip("Empieza a caminar automáticamente al cargar la escena.")]
    [SerializeField] private bool caminarAlIniciar = true;

    [Tooltip("Repite el trayecto en ida y vuelta de forma indefinida.")]
    [SerializeField] private bool repetirEnBucle = false;

    [Tooltip("Segundos de espera entre trayecto y trayecto cuando se repite en bucle.")]
    [SerializeField] private float esperaEntreTrayectos = 1f;

    [Header("Animación")]
    [Tooltip("Nombre del parámetro bool del Animator que indica que el personaje está caminando. " +
             "Si existe, tiene prioridad sobre el trigger.")]
    [SerializeField] private string nombreParametroCaminando = "Moviendose";

    [Tooltip("Trigger de reserva para AnimatorControllers antiguos que no tengan el bool.")]
    [SerializeField] private string nombreTriggerCaminar = "Caminar";

    [Header("Destino")]
    [Tooltip("Usa un punto concreto como destino en lugar de calcular la distancia hacia adelante.")]
    [SerializeField] private bool usarDestinoManual = false;

    [Tooltip("Punto de destino usado cuando 'Usar destino manual' está activo.")]
    [SerializeField] private Vector3 destinoManual = Vector3.zero;

    [Header("Depuración")]
    [Tooltip("Dibuja en la vista de escena el origen y el destino del trayecto.")]
    [SerializeField] private bool dibujarGizmos = true;

    // Radios que se prueban, de más corto a más largo, para buscar el punto de NavMesh
    // más cercano a la posición teórica de destino.
    private static readonly float[] RadiosMuestreo = { 0.05f, 0.1f, 0.25f, 0.5f, 1f, 2f };

    private NavMeshAgent agente;
    private Animator animator;

    private int hashTrigger = -1;
    private bool existeTrigger;

    private int hashBool = -1;
    private bool existeBool;

    private Vector3 origen;
    private Vector3 destino;
    private bool pendienteDeInicio = true;
    private bool trayectoActivo;
    private bool regresando;
    private float inicioTrayecto;

    private void Awake()
    {
        agente = GetComponent<NavMeshAgent>();
        animator = GetComponent<Animator>();
        CachearParametros();
    }

    /// <summary>
    /// True mientras este script está desplazando al personaje. El control manual lo usa
    /// para saber cuándo puede tomar el mando sin pelearse con la caminata automática.
    /// </summary>
    public bool EstaCaminando => trayectoActivo;

    private void OnValidate()
    {
        distanciaMetros = Mathf.Max(0f, distanciaMetros);
        esperaEntreTrayectos = Mathf.Max(0f, esperaEntreTrayectos);
    }

    private void Update()
    {
        if (agente == null)
        {
            return;
        }

        // Espera a que exista NavMesh bajo los pies antes de arrancar.
        if (pendienteDeInicio)
        {
            if (caminarAlIniciar && agente.isOnNavMesh)
            {
                LanzarSiguienteTrayecto();
            }

            return;
        }

        if (!trayectoActivo || agente.pathPending)
        {
            return;
        }

        // remainingDistance es infinito si todavía no hay ruta utilizable.
        if (agente.remainingDistance <= agente.stoppingDistance + 0.02f)
        {
            FinalizarTrayecto();
        }
    }

    /// <summary>
    /// Arranca (o reinicia) el trayecto hacia el siguiente punto de la ruta.
    /// </summary>
    public void LanzarSiguienteTrayecto()
    {
        pendienteDeInicio = false;

        if (!AnclarAlNavMesh())
        {
            Debug.LogError(
                $"[CaminarDosMetros] «{name}» no está sobre el NavMesh y no se puede caminar. " +
                "Comprueba que la superficie NavMesh de la escena esté generada.", this);
            trayectoActivo = false;
            return;
        }

        Vector3 objetivo;

        if (usarDestinoManual)
        {
            // Destino fijado a mano: manda sobre la distancia y sobre la ida y vuelta.
            objetivo = destinoManual;
        }
        else if (regresando)
        {
            // Vuelta al punto de partida.
            objetivo = origen;
        }
        else
        {
            // Primer trayecto (o nueva ida): aquí se fija el origen.
            origen = transform.position;
            objetivo = origen + transform.forward * distanciaMetros;
        }

        objetivo.y = usarDestinoManual ? objetivo.y : origen.y;

        if (!BuscarPuntoDeNavMesh(objetivo, out destino))
        {
            Debug.LogWarning(
                $"[CaminarDosMetros] No se encontró NavMesh cerca de {objetivo}. " +
                "Amplía el piso o vuelve a generar el NavMesh.", this);
            trayectoActivo = false;
            ActivarAnimacionCaminando(false);
            return;
        }

        trayectoActivo = true;
        inicioTrayecto = Time.time;

        agente.isStopped = false;

        if (agente.SetDestination(destino))
        {
            // Solo se da la vuelta al sentido de viaje cuando el trayecto arranca de verdad.
            if (!usarDestinoManual)
            {
                regresando = !regresando;
            }

            ActivarAnimacionCaminando(true);
        }
        else
        {
            trayectoActivo = false;
            Debug.LogWarning(
                $"[CaminarDosMetros] No se pudo ordenar el destino {destino}.", this);
        }
    }

    private void FinalizarTrayecto()
    {
        trayectoActivo = false;

        // Se fija la posición exacta para que el trayecto mida los 2 m completos
        // y no queden los centímetros que deja la distancia de parada del agente.
        if (agente.isOnNavMesh)
        {
            agente.Warp(destino);
        }

        agente.isStopped = true;
        ActivarAnimacionCaminando(false);

        // Distancia en planta: es la que se ha pedido, sin que la altura del NavMesh la desvíe.
        float distancia = Mathf.Sqrt(
            (destino.x - origen.x) * (destino.x - origen.x) +
            (destino.z - origen.z) * (destino.z - origen.z));

        string objetivo = usarDestinoManual ? "destino manual" : $"objetivo {distanciaMetros:0.00} m";

        Debug.Log(
            $"[CaminarDosMetros] «{name}» recorrió {distancia:0.00} m " +
            $"({objetivo}) en {Time.time - inicioTrayecto:0.0} s.", this);

        if (repetirEnBucle)
        {
            Invoke(nameof(LanzarSiguienteTrayecto), esperaEntreTrayectos);
        }
    }

    /// <summary>
    /// Coloca el objeto sobre el NavMesh si no lo está (por ejemplo, tras moverlo a mano).
    /// </summary>
    private bool AnclarAlNavMesh()
    {
        if (agente.isOnNavMesh)
        {
            return true;
        }

        return NavMesh.SamplePosition(transform.position, out NavMeshHit hit, 2f, NavMesh.AllAreas)
            && agente.Warp(hit.position);
    }

    /// <summary>
    /// Busca el punto de NavMesh más cercano al destino ideal, probando radios crecientes.
    /// Así el punto final queda lo más pegado posible a la distancia pedida.
    /// </summary>
    private static bool BuscarPuntoDeNavMesh(Vector3 objetivo, out Vector3 resultado)
    {
        resultado = objetivo;

        bool encontrado = false;
        float mejorError = float.MaxValue;

        foreach (float radio in RadiosMuestreo)
        {
            if (!NavMesh.SamplePosition(objetivo, out NavMeshHit hit, radio, NavMesh.AllAreas))
            {
                continue;
            }

            // Distancia en planta: la diferencia de altura no debe penalizar el ajuste.
            float error = Mathf.Sqrt(
                (hit.position.x - objetivo.x) * (hit.position.x - objetivo.x) +
                (hit.position.z - objetivo.z) * (hit.position.z - objetivo.z));

            if (error < mejorError)
            {
                mejorError = error;
                resultado = hit.position;
                encontrado = true;
            }

            if (mejorError <= 0.005f)
            {
                break;
            }
        }

        return encontrado;
    }

    /// <summary>
    /// Detiene el trayecto en curso (y cualquier repetición pendiente) dejando al personaje
    /// quieto en Idle. Lo usa el control manual para tomar el mando.
    /// </summary>
    public void DetenerCaminata()
    {
        CancelInvoke(nameof(LanzarSiguienteTrayecto));

        trayectoActivo = false;

        // Evita que el arranque automático vuelva a lanzar otro trayecto.
        pendienteDeInicio = false;

        if (agente != null && agente.isOnNavMesh)
        {
            agente.ResetPath();
            agente.isStopped = true;
        }

        ActivarAnimacionCaminando(false);
    }

    private void CachearParametros()
    {
        existeBool = false;
        existeTrigger = false;

        if (animator == null || animator.runtimeAnimatorController == null)
        {
            return;
        }

        hashBool = Animator.StringToHash(nombreParametroCaminando);
        hashTrigger = Animator.StringToHash(nombreTriggerCaminar);

        foreach (AnimatorControllerParameter parametro in animator.parameters)
        {
            if (parametro.nameHash == hashBool)
            {
                existeBool = parametro.type == AnimatorControllerParameterType.Bool;
            }
            else if (parametro.nameHash == hashTrigger)
            {
                existeTrigger = parametro.type == AnimatorControllerParameterType.Trigger;
            }
        }

        if (!existeBool && !existeTrigger)
        {
            Debug.LogWarning(
                $"[CaminarDosMetros] El Animator no tiene el parámetro «{nombreParametroCaminando}» " +
                $"ni el trigger «{nombreTriggerCaminar}». El personaje se moverá sin animación de caminar.", this);
        }
    }

    private void ActivarAnimacionCaminando(bool caminando)
    {
        if (animator == null)
        {
            return;
        }

        if (existeBool)
        {
            animator.SetBool(hashBool, caminando);
            return;
        }

        if (!existeTrigger)
        {
            return;
        }

        animator.ResetTrigger(hashTrigger);

        if (caminando)
        {
            animator.SetTrigger(hashTrigger);
        }
    }

    private void OnDrawGizmosSelected()
    {
        if (!dibujarGizmos || !Application.isPlaying)
        {
            return;
        }

        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(destino, 0.12f);
        Gizmos.DrawLine(origen, destino);

        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(origen, 0.12f);
    }
}