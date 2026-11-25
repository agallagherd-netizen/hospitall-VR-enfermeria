using UnityEngine;
using UnityEngine.AI;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

[RequireComponent(typeof(NavMeshAgent))]
[RequireComponent(typeof(Animator))]
public class EnfermeraWASD : MonoBehaviour
{
    [Header("Movimiento")]
    public float velocidad = 1.8f;
    public float velocidadGiro = 720f;

    [Header("Animación")]
    public string parametroMover = "Moviendose";

    [Header("Cámara")]
    public bool usarDireccionCamara = true;

    private NavMeshAgent _agent;
    private Animator _animator;
    private Camera _camara;
    private bool _tieneBool;

    void Awake()
    {
        _agent = GetComponent<NavMeshAgent>();
        _animator = GetComponent<Animator>();
        _camara = Camera.main;
    }

    void Start()
    {
        if (_agent != null)
        {
            _agent.speed = velocidad;
            _agent.angularSpeed = velocidadGiro;
            _agent.acceleration = 12f;
            _agent.stoppingDistance = 0f;
            _agent.autoBraking = false;
            _agent.updateRotation = true;
            _agent.baseOffset = 0f;
        }

        if (_animator != null && _animator.runtimeAnimatorController != null)
        {
            foreach (var p in _animator.parameters)
            {
                if (p.type == AnimatorControllerParameterType.Bool && p.name == parametroMover)
                {
                    _tieneBool = true;
                    break;
                }
            }
            _animator.applyRootMotion = false;
        }

        // Asegurar que está sobre NavMesh
        ColocarEnNavMesh();
    }

    void ColocarEnNavMesh()
    {
        if (_agent == null || _agent.isOnNavMesh) return;

        NavMeshHit hit;
        if (NavMesh.SamplePosition(transform.position, out hit, 25f, NavMesh.AllAreas))
        {
            transform.position = hit.position;
            _agent.Warp(hit.position);
        }
    }

    void Update()
    {
        if (_agent == null || !_agent.enabled) return;
        if (!_agent.isOnNavMesh) { ColocarEnNavMesh(); return; }

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
            _agent.isStopped = true;
            _agent.velocity = Vector3.zero;
            _agent.ResetPath();
            if (_animator != null && _tieneBool) _animator.SetBool(parametroMover, false);
            return;
        }

        _agent.isStopped = false;

        if (usarDireccionCamara && _camara != null)
        {
            Vector3 f = _camara.transform.forward;
            Vector3 r = _camara.transform.right;
            f.y = 0f; r.y = 0f;
            f.Normalize(); r.Normalize();
            dir = (f * v + r * h).normalized;
        }
        else
        {
            dir.Normalize();
        }

        _agent.SetDestination(transform.position + dir * 2f);

        if (_animator != null && _tieneBool)
            _animator.SetBool(parametroMover, true);
    }
}
