using UnityEngine;
using UnityEngine.AI;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

[DisallowMultipleComponent]
[RequireComponent(typeof(Animator))]
[RequireComponent(typeof(NavMeshAgent))]
public class MoverHumanoideWASD : MonoBehaviour
{
    [Header("Movimiento")]
    public float velocidad = 1.8f;
    public float velocidadGiro = 720f;
    public float aceleracion = 12f;

    [Header("Animación")]
    public string nombreParametroMover = "Moviendose";
    public string nombreTriggerMover = "Caminar";

    [Header("Cámara")]
    public bool usarDireccionCamara = true;

    [Header("Debug")]
    public bool mostrarLogs = true;

    private Animator _animator;
    private NavMeshAgent _agent;
    private Camera _camara;
    private bool _tieneBool;
    private bool _tieneTrigger;

    void Awake()
    {
        _animator = GetComponent<Animator>();
        _agent = GetComponent<NavMeshAgent>();
        _camara = Camera.main;
    }

    void Start()
    {
        if (_agent != null)
        {
            _agent.speed = velocidad;
            _agent.angularSpeed = velocidadGiro;
            _agent.acceleration = aceleracion;
            _agent.stoppingDistance = 0f;
            _agent.autoBraking = false;
            _agent.updateRotation = true;
            _agent.baseOffset = 0f;
        }
        ComprobarParametros();
        ColocarSobreNavMesh();
    }

    void ComprobarParametros()
    {
        if (_animator == null || _animator.runtimeAnimatorController == null) return;
        foreach (var p in _animator.parameters)
        {
            if (p.type == AnimatorControllerParameterType.Bool && p.name == nombreParametroMover) _tieneBool = true;
            if (p.type == AnimatorControllerParameterType.Trigger && p.name == nombreTriggerMover) _tieneTrigger = true;
        }
        if (mostrarLogs) Debug.Log("[" + gameObject.name + "] Bool '" + nombreParametroMover + "': " + _tieneBool + " | Trigger '" + nombreTriggerMover + "': " + _tieneTrigger);
    }

    void ColocarSobreNavMesh()
    {
        if (_agent == null) return;
        if (_agent.isOnNavMesh) return;
        if (mostrarLogs) Debug.LogWarning("[" + gameObject.name + "] Buscando NavMesh...");
        NavMeshHit hit;
        if (NavMesh.SamplePosition(transform.position, out hit, 15f, NavMesh.AllAreas))
        {
            transform.position = hit.position;
            _agent.Warp(hit.position);
            if (mostrarLogs) Debug.Log("[" + gameObject.name + "] Colocado sobre NavMesh.");
        }
        else if (mostrarLogs) Debug.LogError("[" + gameObject.name + "] NO HAY NAVMESH CERCANO.");
    }

    void Update()
    {
        if (_agent == null) return;
        if (!_agent.isOnNavMesh) { ColocarSobreNavMesh(); return; }

        float horizontal = 0f;
        float vertical = 0f;

#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null)
        {
            if (Keyboard.current.aKey.isPressed || Keyboard.current.leftArrowKey.isPressed) horizontal -= 1f;
            if (Keyboard.current.dKey.isPressed || Keyboard.current.rightArrowKey.isPressed) horizontal += 1f;
            if (Keyboard.current.sKey.isPressed || Keyboard.current.downArrowKey.isPressed) vertical -= 1f;
            if (Keyboard.current.wKey.isPressed || Keyboard.current.upArrowKey.isPressed) vertical += 1f;
        }
#endif

        if (Mathf.Approximately(horizontal, 0f) && Mathf.Approximately(vertical, 0f))
        {
            horizontal = Input.GetAxisRaw("Horizontal");
            vertical = Input.GetAxisRaw("Vertical");
        }

        Vector3 direccion = new Vector3(horizontal, 0f, vertical);
        if (direccion.sqrMagnitude < 0.01f)
        {
            _agent.isStopped = true;
            _agent.velocity = Vector3.zero;
            SetAnimacion(false);
            return;
        }

        _agent.isStopped = false;
        if (usarDireccionCamara && _camara != null)
        {
            Vector3 forward = _camara.transform.forward;
            Vector3 right = _camara.transform.right;
            forward.y = 0f;
            right.y = 0f;
            forward.Normalize();
            right.Normalize();
            direccion = (forward * vertical + right * horizontal).normalized;
        }
        else
        {
            direccion.Normalize();
        }

        Vector3 destino = transform.position + direccion * 1.5f;
        _agent.SetDestination(destino);
        SetAnimacion(true);
    }

    void SetAnimacion(bool moviendose)
    {
        if (_animator == null) return;
        if (_tieneBool)
        {
            _animator.SetBool(nombreParametroMover, moviendose);
            return;
        }
        if (moviendose && _tieneTrigger) _animator.SetTrigger(nombreTriggerMover);
    }
}
