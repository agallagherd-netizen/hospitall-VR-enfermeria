using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Separa del modelo "015" las piezas del frente (paneles + tiradores) para poder
/// abrirlas como cajones con la tecla S.
///
/// El OBJ del carro es una sola malla dividida por materiales, asi que los cajones
/// no son objetos independientes. Este script:
///   1. marca el modelo como legible (Read/Write),
///   2. busca "islas" (trozos de malla sueltos) planas y anchas en la cara frontal,
///   3. las saca a objetos nuevos "Cajon_1" / "Cajon_2" y las quita de la malla original,
///   4. los asigna al componente Carro015Control.
///
/// Menu manual: Tools > Carro 015 > Separar cajones
/// </summary>
[InitializeOnLoad]
public static class Carro015Cajones
{
    private const string TempDir = @"C:\Users\User\AppData\Local\Temp\opencode\";
    private const string Report  = TempDir + "carro-cajones.txt";
    private const string Flag    = TempDir + "carro-cajones.flag";
    private const string Carpeta = "Assets/Carro015";

    // Filtros para reconocer el frente del carro (unidades del modelo):
    private const float FrenteZMin   = 4f;    // la cara frontal esta en Z positivo
    private const float FrenteGrosor = 2f;    // piezas planas (paneles, tiradores)
    private const float FrenteAncho  = 5f;    // piezas anchas
    private const float CorteY       = 33.5f; // debajo / encima de esta altura

    static Carro015Cajones()
    {
        EditorApplication.update += Vigilar;
    }

    private static void Vigilar()
    {
        if (!File.Exists(Flag))
            return;

        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating)
            return;

        File.Delete(Flag);
        Separar(true);
    }

    [MenuItem("Tools/Carro 015/Separar cajones")]
    private static void DesdeMenu()
    {
        Separar(true);
    }

    private static void Separar(bool log)
    {
        StringBuilder sb = new StringBuilder();
        sb.AppendLine("=== cajones 015 " + DateTime.Now.ToString("HH:mm:ss") + " ===");

        Scene scene = SceneManager.GetActiveScene();
        GameObject carro = Buscar("015");

        if (carro == null)
        {
            sb.AppendLine("ERROR: no se encontro el objeto '015'.");
            Escribir(sb, log);
            return;
        }

        Carro015Control control = carro.GetComponent<Carro015Control>();
        if (control == null)
        {
            control = carro.AddComponent<Carro015Control>();
            sb.AppendLine("Componente Carro015Control anadido.");
        }

        if (control.cajones != null && control.cajones.Length > 0)
        {
            sb.AppendLine("Ya hay " + control.cajones.Length + " cajones asignados; no se toca nada.");
            Escribir(sb, log);
            return;
        }

        if (carro.transform.Find("Cajon_1") != null)
        {
            sb.AppendLine("Ya existe 'Cajon_1'; no se duplica. (Borra ese objeto si quieres rehacerlo).");
            Escribir(sb, log);
            return;
        }

        // ---------- 1. mallas legibles ----------
        List<MeshFilter> filtros = carro.GetComponentsInChildren<MeshFilter>(true).ToList();
        string rutaModelo = filtros.Count > 0 && filtros[0].sharedMesh != null
            ? AssetDatabase.GetAssetPath(filtros[0].sharedMesh)
            : "";

        bool noLegibles = filtros.Any(f => f.sharedMesh != null && !f.sharedMesh.isReadable);

        if (noLegibles && !string.IsNullOrEmpty(rutaModelo))
        {
            ModelImporter imp = AssetImporter.GetAtPath(rutaModelo) as ModelImporter;

            if (imp != null)
            {
                imp.isReadable = true;
                imp.SaveAndReimport();
                sb.AppendLine("Modelo marcado como legible y reimportado: " + rutaModelo);
            }
        }

        filtros = carro.GetComponentsInChildren<MeshFilter>(true).ToList();

        if (filtros.Any(f => f.sharedMesh != null && !f.sharedMesh.isReadable))
        {
            sb.AppendLine("ERROR: hay mallas que no son legibles; no se puede separar.");
            Escribir(sb, log);
            return;
        }

        // ---------- 2. desempaquetar el prefab ----------
        if (PrefabUtility.IsPartOfPrefabInstance(carro) || PrefabUtility.IsPartOfPrefabAsset(carro))
        {
            PrefabUtility.UnpackPrefabInstance(carro, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            sb.AppendLine("Prefab desempaquetado: el carro ya es editable en la escena.");
            filtros = carro.GetComponentsInChildren<MeshFilter>(true).ToList();
        }

        if (!AssetDatabase.IsValidFolder(Carpeta))
            AssetDatabase.CreateFolder("Assets", "Carro015");

        {
            Dictionary<int, GameObject> padres = new Dictionary<int, GameObject>();

            foreach (MeshFilter filter in filtros)
            {
                Mesh malla = filter.sharedMesh;
                if (malla == null)
                    continue;

                MeshRenderer renderer = filter.GetComponent<MeshRenderer>();
                if (renderer == null)
                    continue;

                if (malla.subMeshCount != 1)
                {
                    sb.AppendLine("  (aviso) '" + filter.name + "' tiene " + malla.subMeshCount + " submeshes; se usa el 0.");
                }

                Vector3[] vertices = malla.vertices;
                int[] triangulos = malla.GetTriangles(0);

                if (vertices.Length == 0 || triangulos.Length < 3)
                    continue;

                // islas = vertices conectados por triangulos
                int[] padre = new int[vertices.Length];
                for (int i = 0; i < padre.Length; i++) padre[i] = i;

                for (int i = 0; i + 2 < triangulos.Length; i += 3)
                {
                    Unir(padre, triangulos[i], triangulos[i + 1]);
                    Unir(padre, triangulos[i + 1], triangulos[i + 2]);
                }

                Dictionary<int, List<int>> porIsla = new Dictionary<int, List<int>>();

                for (int i = 0; i + 2 < triangulos.Length; i += 3)
                {
                    int raiz = Buscar(padre, triangulos[i]);

                    if (!porIsla.TryGetValue(raiz, out List<int> lista))
                    {
                        lista = new List<int>();
                        porIsla[raiz] = lista;
                    }

                    lista.Add(i);
                }

                List<int> tri1 = new List<int>(); // cajon de abajo
                List<int> tri2 = new List<int>(); // cajon de arriba
                List<int> resto = new List<int>();

                foreach (KeyValuePair<int, List<int>> isla in porIsla)
                {
                    Bounds caja = Caja(vertices, triangulos, isla.Value);
                    Vector3 tam = caja.size;
                    int grupo = 0;

                    bool esFrente = caja.min.z > FrenteZMin && tam.z < FrenteGrosor && tam.x > FrenteAncho;

                    if (esFrente)
                    {
                        if (caja.max.y <= CorteY) grupo = 1;
                        else if (caja.min.y >= CorteY) grupo = 2;
                    }

                    sb.AppendLine(string.Format(
                        "  isla {0,-28} tris={1,5}  caja=centro{2} tam{3}  -> {4}",
                        filter.name,
                        isla.Value.Count,
                        caja.center.ToString("0.##"),
                        tam.ToString("0.##"),
                        grupo == 0 ? "se queda" : "cajon " + grupo));

                    if (grupo == 1) tri1.AddRange(isla.Value);
                    else if (grupo == 2) tri2.AddRange(isla.Value);
                    else resto.AddRange(isla.Value);
                }

                if (tri1.Count == 0 && tri2.Count == 0)
                {
                    sb.AppendLine("  (sin piezas de frente en '" + filter.name + "')");
                    continue;
                }

                // malla sin los cajones
                Mesh mallaResto = UnityEngine.Object.Instantiate(malla);
                mallaResto.name = malla.name + "_resto";
                mallaResto.SetTriangles(resto.ToArray(), 0);
                mallaResto.RecalculateBounds();
                AssetDatabase.CreateAsset(mallaResto, Carpeta + "/" + Limpio(filter.name) + "_resto.asset");
                filter.sharedMesh = mallaResto;

                // mallas de los cajones
                for (int g = 1; g <= 2; g++)
                {
                    List<int> seleccion = g == 1 ? tri1 : tri2;

                    if (seleccion.Count == 0)
                        continue;

                    if (!padres.TryGetValue(g, out GameObject padreCajon))
                    {
                        padreCajon = new GameObject("Cajon_" + g);
                        padreCajon.transform.SetParent(carro.transform, false);
                        padreCajon.transform.localPosition = Vector3.zero;
                        padreCajon.transform.localRotation = Quaternion.identity;
                        padreCajon.transform.localScale = Vector3.one;
                        padres[g] = padreCajon;
                    }

                    Mesh mallaCajon = new Mesh();
                    mallaCajon.name = "Cajon" + g + "_" + malla.name;
                    CopiarVertices(malla, mallaCajon);
                    mallaCajon.SetTriangles(seleccion.ToArray(), 0);
                    mallaCajon.RecalculateBounds();
                    AssetDatabase.CreateAsset(mallaCajon, Carpeta + "/Cajon" + g + "_" + Limpio(filter.name) + ".asset");

                    GameObject trozo = new GameObject(Limpio(filter.name) + "_cajon");
                    trozo.transform.SetParent(padreCajon.transform, false);
                    trozo.transform.localPosition = Vector3.zero;
                    trozo.transform.localRotation = Quaternion.identity;
                    trozo.transform.localScale = Vector3.one;

                    trozo.AddComponent<MeshFilter>().sharedMesh = mallaCajon;
                    MeshRenderer nuevoRenderer = trozo.AddComponent<MeshRenderer>();
                    nuevoRenderer.sharedMaterials = renderer.sharedMaterials;
                    nuevoRenderer.shadowCastingMode = renderer.shadowCastingMode;
                    nuevoRenderer.receiveShadows = renderer.receiveShadows;

                    sb.AppendLine("  creado '" + padreCajon.name + "/" + trozo.name + "'  tris=" + seleccion.Count / 3);
                }
            }

            List<Transform> cajones = new List<Transform>();
            for (int g = 1; g <= 2; g++)
            {
                if (padres.TryGetValue(g, out GameObject p))
                    cajones.Add(p.transform);
            }

            if (cajones.Count == 0)
            {
                sb.AppendLine("ERROR: no se encontro ninguna pieza frontal que separar.");
            }
            else
            {
                control.cajones = cajones.ToArray();
                control.apertura = 0.07f;
                control.velocidadCajon = 0.35f;
                control.sentidoApertura = 1f;

                foreach (Transform t in cajones)
                {
                    Renderer[] rs = t.GetComponentsInChildren<Renderer>(true);
                    Bounds b = rs.Length > 0 ? rs[0].bounds : new Bounds(t.position, Vector3.zero);

                    foreach (Renderer r in rs)
                        b.Encapsulate(r.bounds);

                    sb.AppendLine("  " + t.name + ": bounds mundo centro=" + b.center.ToString("0.###")
                                  + " tam=" + b.size.ToString("0.###"));
                }

                sb.AppendLine("Cajones asignados al componente: " + cajones.Count);
            }

            EditorUtility.SetDirty(carro);
        }

        AssetDatabase.SaveAssets();

        EditorSceneManager.MarkSceneDirty(scene);
        bool guardado = EditorSceneManager.SaveScene(scene);
        sb.AppendLine("Escena guardada: " + guardado);

        Escribir(sb, log);
    }

    private static void CopiarVertices(Mesh origen, Mesh destino)
    {
        destino.vertices = origen.vertices;

        Vector3[] normales = origen.normals;
        if (normales.Length > 0) destino.normals = normales;

        Vector2[] uv = origen.uv;
        if (uv.Length > 0) destino.uv = uv;

        Vector2[] uv2 = origen.uv2;
        if (uv2.Length > 0) destino.uv2 = uv2;

        Vector4[] tangentes = origen.tangents;
        if (tangentes.Length > 0) destino.tangents = tangentes;

        Color[] colores = origen.colors;
        if (colores.Length > 0) destino.colors = colores;
    }

    private static Bounds Caja(Vector3[] vertices, int[] triangulos, List<int> inicios)
    {
        Bounds caja = new Bounds(vertices[triangulos[inicios[0]]], Vector3.zero);

        foreach (int i in inicios)
        {
            caja.Encapsulate(vertices[triangulos[i]]);
            caja.Encapsulate(vertices[triangulos[i + 1]]);
            caja.Encapsulate(vertices[triangulos[i + 2]]);
        }

        return caja;
    }

    private static int Buscar(int[] padre, int i)
    {
        while (padre[i] != i)
        {
            padre[i] = padre[padre[i]];
            i = padre[i];
        }

        return i;
    }

    private static void Unir(int[] padre, int a, int b)
    {
        int ra = Buscar(padre, a);
        int rb = Buscar(padre, b);

        if (ra != rb)
            padre[rb] = ra;
    }

    private static string Limpio(string nombre)
    {
        return new string(nombre.Select(c => char.IsLetterOrDigit(c) || c == '_' ? c : '_').ToArray());
    }

    private static GameObject Buscar(string nombre)
    {
        Scene scene = SceneManager.GetActiveScene();

        foreach (GameObject root in scene.GetRootGameObjects())
        {
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            {
                if (t.name.Trim() == nombre)
                    return t.gameObject;
            }
        }

        return null;
    }

    private static void Escribir(StringBuilder sb, bool log)
    {
        File.WriteAllText(Report, sb.ToString());

        if (log)
            Debug.Log("[Carro015Cajones] " + sb);
    }
}
