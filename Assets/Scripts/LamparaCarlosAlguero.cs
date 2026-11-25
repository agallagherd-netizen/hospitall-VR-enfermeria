using UnityEngine;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Controla la lámpara "Lampara - Carlos Alguero".
/// Pulsa ENTER para encender/apagar la luz.
/// </summary>
public class LamparaCarlosAlguero : MonoBehaviour
{
    [Header("Luz")]
    [Tooltip("Luz que se va a controlar. Si está vacía, se busca o crea un componente Light en este objeto.")]
    public Light luz;

    [Header("Tecla")]
    public KeyCode tecla = KeyCode.Return;

    void Awake()
    {
        BuscarOLanzarLuz();
    }

    void Start()
    {
        BuscarOLanzarLuz();
    }

    void BuscarOLanzarLuz()
    {
        if (luz == null)
            luz = GetComponentInChildren<Light>(true);

        if (luz == null)
            luz = GetComponent<Light>();

        if (luz == null)
        {
            GameObject goLuz = new GameObject("Luz_CarlosAlguero");
            goLuz.transform.SetParent(transform, false);
            goLuz.transform.localPosition = new Vector3(0f, -0.1f, 0f);
            goLuz.transform.localRotation = Quaternion.identity;
            goLuz.transform.localScale = Vector3.one;

            luz = goLuz.AddComponent<Light>();
            luz.type = LightType.Point;
            luz.color = new Color(1f, 0.9f, 0.7f);
            luz.intensity = 2f;
            luz.range = 5f;
            luz.shadows = LightShadows.Soft;
        }

        // Empieza apagada
        if (luz != null)
            luz.enabled = false;
    }

    void Update()
    {
        bool pulsar = false;

#if ENABLE_INPUT_SYSTEM
        Keyboard kb = Keyboard.current;
        if (kb != null && kb.enterKey.wasPressedThisFrame)
            pulsar = true;
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
        if (!pulsar && Input.GetKeyDown(tecla))
            pulsar = true;
#endif

        if (pulsar && luz != null)
            luz.enabled = !luz.enabled;
    }
}
