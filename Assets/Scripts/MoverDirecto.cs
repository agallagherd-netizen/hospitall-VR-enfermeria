using UnityEngine;
using UnityEngine.AI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

[RequireComponent(typeof(NavMeshAgent))]
[RequireComponent(typeof(Animator))]
public class MoverDirecto : MonoBehaviour
{
    public static MoverDirecto Seleccionado;

    [Header("Movimiento")]
    public float velocidad = 2f;
    public float giro = 720f;

    [Header("Animacion")]
    public string paramMover = "Moviendose";

    private NavMeshAgent agent;
    private Animator anim;
    private Camera cam;
    private bool tieneBool;

    void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        anim = GetComponent<Animator>();
        cam = Camera.main;
    }

    void Start()
    {
        if (agent != null)
        {
            agent.speed = velocidad;
            agent.angularSpeed = giro;
            agent.acceleration = 15f;
            agent.stoppingDistance = 0f;
            agent.autoBraking = false;
            agent.updateRotation = true;
            agent.updatePosition = true;
            agent.baseOffset = 0f;
        }

        if (anim != null && anim.runtimeAnimatorController != null)
        {
            foreach (var p in anim.parameters)
            {
                if (p.type == AnimatorControllerParameterType.Bool && p.name == paramMover)
                {
                    tieneBool = true;
                    break;
                }
            }
            anim.applyRootMotion = false;
        }

        ColocarEnNavMesh();
    }

    void OnMouseDown()
    {
        Seleccionar();
    }

    public void Seleccionar()
    {
        if (Seleccionado != null && Seleccionado != this)
            Seleccionado.Deseleccionar();

        Seleccionado = this;
        Debug.Log("[MOVER] Seleccionado: " + gameObject.name + " | Usa WASD para moverlo");
    }

    public void Deseleccionar()
    {
        if (agent != null && agent.isOnNavMesh)
        {
            agent.isStopped = true;
            agent.ResetPath();
        }

        if (anim != null && tieneBool)
            anim.SetBool(paramMover, false);

        if (Seleccionado == this)
            Seleccionado = null;
    }

    void ColocarEnNavMesh()
    {
        if (agent == null) return;
        if (agent.isOnNavMesh) return;

        NavMeshHit hit;
        if (NavMesh.SamplePosition(transform.position, out hit, 25f, NavMesh.AllAreas))
        {
            transform.position = hit.position;
            agent.Warp(hit.position);
            Debug.Log("[MOVER] " + gameObject.name + " colocado sobre NavMesh");
        }
        else
        {
            Debug.LogError("[MOVER] " + gameObject.name + " NO encuentra NavMesh. Tienes que hornear el NavMesh.");
        }
    }

    void Update()
    {
        if (Seleccionado != this) return;
        if (agent == null || !agent.enabled) return;
        if (!agent.isOnNavMesh) { ColocarEnNavMesh(); return; }

        float h = 0f;
        float v = 0f;

#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null)
        {
            if (Keyboard.current.aKey.isPressed || Keyboard.current.leftArrowKey.isPressed) h -= 1f;
            if (Keyboard.current.dKey.isPressed || Keyboard.current.rightArrowKey.isPressed) h += 1f;
            if (Keyboard.current.sKey.isPressed || Keyboard.current.downArrowKey.isPressed) v -= 1f;
            if (Keyboard.current.wKey.isPressed || Keyboard.current.upArrowKey.isPressed) v += 1f;
        }
#endif

        if (Mathf.Approximately(h, 0f) && Mathf.Approximately(v, 0f))
        {
            h = Input.GetAxisRaw("Horizontal");
            v = Input.GetAxisRaw("Vertical");
        }

        Vector3 dir = new Vector3(h, 0f, v);

        if (dir.sqrMagnitude < 0.01f)
        {
            agent.isStopped = true;
            agent.velocity = Vector3.zero;
            agent.ResetPath();
            if (anim != null && tieneBool) anim.SetBool(paramMover, false);
            return;
        }

        agent.isStopped = false;

        if (cam != null)
        {
            Vector3 f = cam.transform.forward;
            Vector3 r = cam.transform.right;
            f.y = 0f; r.y = 0f;
            f.Normalize(); r.Normalize();
            dir = (f * v + r * h).normalized;
        }
        else
        {
            dir.Normalize();
        }

        agent.SetDestination(transform.position + dir * 3f);

        if (anim != null && tieneBool)
            anim.SetBool(paramMover, true);
    }
}

// Force recompile: 23-10-2026 18:47:53
