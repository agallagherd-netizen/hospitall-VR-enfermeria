using UnityEngine;
using UnityEngine.AI;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Desplaza al personaje hacia la derecha mientras se mantiene pulsada la flecha derecha.
///
/// Usa el <see cref="NavMeshAgent"/> para el movimiento y el <see cref="Animator"/> para
/// reproducir el ciclo de caminar. Al soltar la tecla el personaje se detiene y vuelve a Idle.
///
/// El AnimatorController debe tener un parámetro bool «Moviendose» (o, si no existe, un
/// trigger «Caminar») para que la animación acompañe al desplazamiento.
/// </summary>
[RequireComponent(typeof(NavMeshAgent))]
[RequireComponent(typeof(Animator))]
[AddComponentMenu("Enfermeria/Control con flecha derecha")]
public class ControlFlechaDerecha : MonoBehaviour
{
    [Header("Desplazamiento")]
    [Tooltip("Distancia máxima que se recorre en una pulsación. El NavMesh la recorta en el borde del piso.")]
    [SerializeField] private float distanciaMaxima = 30f;

    [Tooltip("Metros que se retrocede desde el borde del NavMesh para no quedarse clavado en el límite.")]
    [SerializeField] private float margenBordeNavMesh = 0.45f;

    [Tooltip("Si está activo, «derecha» es la derecha de la pantalla; si no, el eje +X del mundo.")]
    [SerializeField] private bool usarDireccionDeCamara = true;

    [Header("Animación")]
    [Tooltip("Parámetro bool del Animator que se pone a true mientras el personaje camina.")]
    [SerializeField] private string nombreParametroCaminando = "Moviendose";

    [Tooltip("Trigger de reserva para AnimatorControllers antiguos que no tengan el bool.")]
    [SerializeField] private string nombreTriggerCaminar = "Caminar";

    [Header("Compatibilidad")]
    [Tooltip("Detiene la caminata automática de 2 m en cuanto el jugador usa la flecha.")]
    [SerializeField] private bool tomarControlDeLaCaminataAutomatica = true;

    private NavMeshAgent agente;
    private Animator animator;
    private CaminarDosMetros caminataAutomatica;

    private int hashBool;
    private bool existeBool;
    private int hashTrigger;
    private bool existeTrigger;

    private bool manteniendoPulsada;
    private bool avisadoSinNavMesh;

    private void Awake()
    {
        agente = GetComponent<NavMeshAgent>();
        animator = GetComponent<Animator>();
        caminataAutomatica = GetComponent<CaminarDosMetros>();
        CachearParametros();
    }

    private void OnDisable()
    {
        if (manteniendoPulsada)
        {
            Soltar();
        }
    }

    private void Update()
    {
        if (agente == null || animator == null)
        {
            return;
        }

        bool pulsada = FlechaDerechaPulsada();

        if (pulsada == manteniendoPulsada)
        {
            return;
        }

        if (pulsada)
        {
            Empezar();
        }
        else
        {
            Soltar();
        }
    }

    // =====================================================================
    //  Movimiento
    // =====================================================================

    private void Empezar()
    {
        // Si el personaje venía de la caminata automática de 2 m, el jugador toma el mando.
        if (tomarControlDeLaCaminataAutomatica && caminataAutomatica != null)
        {
            caminataAutomatica.DetenerCaminata();
        }

        if (!agente.isOnNavMesh && !AnclarAlNavMesh())
        {
            AvisarUnaVez("El personaje no está sobre el NavMesh; no se puede mover.");
            return;
        }

        Vector3 direccion = DireccionDerecha();

        if (!CalcularDestino(direccion, out Vector3 destino))
        {
            AvisarUnaVez("No se encontró un destino válido sobre el NavMesh hacia la derecha.");
            return;
        }

        // Sin hueco para avanzar: no tiene sentido arrancar la animación.
        if ((destino - transform.position).sqrMagnitude < 0.04f)
        {
            AvisarUnaVez("El personaje ya está en el borde del NavMesh.");
            return;
        }

        manteniendoPulsada = true;
        avisadoSinNavMesh = false;

        agente.isStopped = false;
        agente.updateRotation = true;

        if (!agente.SetDestination(destino))
        {
            manteniendoPulsada = false;
            AvisarUnaVez($"No se pudo ordenar el destino {destino}.");
            return;
        }

        AplicarAnimacion(true);
    }

    private void Soltar()
    {
        manteniendoPulsada = false;

        if (agente != null && agente.isOnNavMesh)
        {
            agente.ResetPath();
            agente.isStopped = true;
        }

        AplicarAnimacion(false);
    }

    /// <summary>
    /// Derecha de la pantalla proyectada sobre el suelo. Si no hay cámara utilizable,
    /// cae al eje +X del mundo para que el control siga funcionando.
    /// </summary>
    private Vector3 DireccionDerecha()
    {
        if (usarDireccionDeCamara)
        {
            Camera camara = Camera.main;

            if (camara != null)
            {
                Vector3 derecha = camara.transform.right;
                derecha.y = 0f;

                if (derecha.sqrMagnitude > 0.0001f)
                {
                    return derecha.normalized;
                }
            }
        }

        return Vector3.right;
    }

    /// <summary>
    /// Destino lejos hacia la derecha, recortado al borde del NavMesh.
    /// </summary>
    private bool CalcularDestino(Vector3 direccion, out Vector3 destino)
    {
        Vector3 origen = transform.position;
        Vector3 lejano = origen + direccion * distanciaMaxima;

        if (NavMesh.Raycast(origen, lejano, out NavMeshHit borde, NavMesh.AllAreas))
        {
            float distanciaAlBorde = Vector3.Distance(origen, borde.position);

            lejano = distanciaAlBorde > margenBordeNavMesh * 2f
                ? borde.position - direccion * margenBordeNavMesh
                : borde.position;
        }

        if (NavMesh.SamplePosition(lejano, out NavMeshHit punto, 1.5f, NavMesh.AllAreas))
        {
            destino = punto.position;
            return true;
        }

        destino = origen;
        return false;
    }

    private bool AnclarAlNavMesh()
    {
        return NavMesh.SamplePosition(transform.position, out NavMeshHit punto, 2f, NavMesh.AllAreas)
            && agente.Warp(punto.position);
    }

    private void AvisarUnaVez(string mensaje)
    {
        if (avisadoSinNavMesh)
        {
            return;
        }

        avisadoSinNavMesh = true;
        Debug.LogWarning($"[ControlFlechaDerecha] {mensaje}", this);
    }

    // =====================================================================
    //  Entrada y animación
    // =====================================================================

    private static bool FlechaDerechaPulsada()
    {
#if ENABLE_INPUT_SYSTEM
        return Keyboard.current != null && Keyboard.current.rightArrowKey.isPressed;
#elif ENABLE_LEGACY_INPUT_MANAGER
        return Input.GetKey(KeyCode.RightArrow);
#else
        return false;
#endif
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
                $"[ControlFlechaDerecha] El Animator no tiene el parámetro «{nombreParametroCaminando}» " +
                $"ni el trigger «{nombreTriggerCaminar}». El personaje se moverá sin animación.", this);
        }
    }

    private void AplicarAnimacion(bool caminando)
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
}
