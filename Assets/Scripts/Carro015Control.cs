using UnityEngine;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Controla el carro de enfermería "015" con el teclado:
///
///   W  -> avanzar hacia delante
///   S  -> retroceder
///   A  -> desplazarse hacia la derecha
///   D  -> desplazarse hacia la izquierda
///   E  -> abrir / cerrar los cajones
///
/// El movimiento se hace relativo a la cámara (como si empujases el carro),
/// así que "adelante" es siempre hacia donde mira el casco / la cámara.
/// Los cajones son una lista de Transform que se deslizan hacia delante.
/// </summary>
public class Carro015Control : MonoBehaviour
{
    [Header("Movimiento")]
    [Tooltip("Metros por segundo que se mueve el carro.")]
    public float velocidad = 1.0f;

    [Tooltip("Si esta marcado, el carro se mueve segun hacia donde mira la camara.")]
    public bool usarDireccionCamara = true;

    [Tooltip("Con esto marcado: A mueve a la derecha y D a la izquierda (como pediste). " +
             "Desmarcalo para el comportamiento clasico (A izquierda, D derecha).")]
    public bool invertirIzquierdaDerecha = true;

    [Header("Cajones")]
    [Tooltip("Piezas que se deslizan al abrir. Si esta vacio, la S no hace nada.")]
    public Transform[] cajones = new Transform[0];

    [Tooltip("Cuanto sale cada cajon, en metros.")]
    public float apertura = 0.08f;

    [Tooltip("Velocidad de apertura/cierre en metros por segundo.")]
    public float velocidadCajon = 0.35f;

    [Tooltip("Deslizar el cajon en el eje Z local del carro. Si sale hacia atras, ponlo en -1.")]
    public float sentidoApertura = 1f;

    [Header("Estado")]
    [SerializeField] private bool cajonesAbiertos;

    /// <summary>True si los cajones estan abiertos.</summary>
    public bool CajonesAbiertos
    {
        get { return cajonesAbiertos; }
    }

    private Camera _camara;
    private Vector3[] _posicionCerrada;
    private bool _eAntes;

    void Awake()
    {
        _camara = BuscarCamara();
        GuardarPosicionesCerradas();
    }

    void Start()
    {
        _camara = BuscarCamara();
    }

    void GuardarPosicionesCerradas()
    {
        if (cajones == null)
        {
            _posicionCerrada = new Vector3[0];
            return;
        }

        _posicionCerrada = new Vector3[cajones.Length];

        for (int i = 0; i < cajones.Length; i++)
        {
            if (cajones[i] != null)
                _posicionCerrada[i] = cajones[i].localPosition;
        }
    }

    /// <summary>Vuelve a tomar la posicion actual de los cajones como "cerrado".</summary>
    public void RecordarPosicionCerrada()
    {
        GuardarPosicionesCerradas();
    }

    Camera BuscarCamara()
    {
        Camera[] todas = Camera.allCameras;
        foreach (Camera c in todas)
        {
            if (c != null && c.name == "CenterEyeAnchor")
                return c;
        }

        return Camera.main;
    }

    void Update()
    {
        float adelante = 0f;
        float lado = 0f;          // +1 derecha, -1 izquierda
        bool alternarCajones = false;

#if ENABLE_INPUT_SYSTEM
        Keyboard kb = Keyboard.current;
        if (kb != null)
        {
            if (kb.wKey.isPressed) adelante += 1f;
            if (kb.sKey.isPressed) adelante -= 1f;

            if (kb.aKey.isPressed) lado += 1f;
            if (kb.dKey.isPressed) lado -= 1f;

            if (kb.eKey.wasPressedThisFrame)
                alternarCajones = true;
        }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
        if (Input.GetKey(KeyCode.W)) adelante += 1f;
        if (Input.GetKey(KeyCode.S)) adelante -= 1f;
        if (Input.GetKey(KeyCode.A)) lado += 1f;
        if (Input.GetKey(KeyCode.D)) lado -= 1f;
        if (Input.GetKeyDown(KeyCode.E)) alternarCajones = true;
#endif

        if (alternarCajones)
            AbrirCerrarCajones();

        if (!invertirIzquierdaDerecha)
            lado = -lado;

        Mover(adelante, lado);
        AnimarCajones();
    }

    void Mover(float adelante, float lado)
    {
        if (Mathf.Approximately(adelante, 0f) && Mathf.Approximately(lado, 0f))
            return;

        Vector3 frente = Vector3.forward;
        Vector3 derecha = Vector3.right;

        if (usarDireccionCamara)
        {
            if (_camara == null)
                _camara = BuscarCamara();

            if (_camara != null)
            {
                frente = _camara.transform.forward;
                derecha = _camara.transform.right;
            }
        }

        frente.y = 0f;
        derecha.y = 0f;

        if (frente.sqrMagnitude < 0.0001f) frente = Vector3.forward;
        if (derecha.sqrMagnitude < 0.0001f) derecha = Vector3.right;

        frente.Normalize();
        derecha.Normalize();

        Vector3 direccion = frente * adelante + derecha * lado;

        if (direccion.sqrMagnitude < 0.0001f)
            return;

        transform.position += direccion.normalized * velocidad * Time.deltaTime;
    }

    /// <summary>Abre los cajones si estan cerrados y los cierra si estan abiertos.</summary>
    public void AbrirCerrarCajones()
    {
        cajonesAbiertos = !cajonesAbiertos;
    }

    /// <summary>Fuerza el estado de los cajones.</summary>
    public void PonerCajones(bool abiertos)
    {
        cajonesAbiertos = abiertos;
    }

    void AnimarCajones()
    {
        if (cajones == null || cajones.Length == 0)
            return;

        if (_posicionCerrada == null || _posicionCerrada.Length != cajones.Length)
            GuardarPosicionesCerradas();

        // Hacia donde esta la parte de delante del carro. Ojo: usamos
        // localToWorldMatrix porque TransformDirection ignora la escala y este
        // carro tiene la escala Z en negativo (el frente mira al otro lado).
        Vector3 frente = transform.localToWorldMatrix.MultiplyVector(Vector3.forward);
        frente.y = 0f;

        if (frente.sqrMagnitude < 0.0001f)
            frente = Vector3.forward;

        frente.Normalize();

        // El carro puede tener la escala en centimetros, asi que pasamos tanto el
        // recorrido como el paso (metros) al espacio local de los cajones.
        for (int i = 0; i < cajones.Length; i++)
        {
            Transform cajon = cajones[i];
            if (cajon == null || cajon.parent == null)
                continue;

            Transform padre = cajon.parent;
            float pasoLocal = padre.InverseTransformVector(frente * (velocidadCajon * Time.deltaTime)).magnitude;

            Vector3 objetivo = _posicionCerrada[i];

            if (cajonesAbiertos)
                objetivo += padre.InverseTransformVector(frente * (apertura * sentidoApertura));

            cajon.localPosition = Vector3.MoveTowards(cajon.localPosition, objetivo, pasoLocal);
        }
    }
}
