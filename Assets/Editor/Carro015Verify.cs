using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

/// <summary>
/// Prueba automatica del carro 015 en Play: mete eventos de teclado de verdad
/// (S, W, A, D) y apunta en carro-verificar.txt que pasa con el carro.
///
/// Se dispara creando carro-verificar.flag.
/// El archivo carro-verificar.marca indica que hay una prueba en marcha
/// (al entrar en Play se recarga el dominio y se pierden las variables).
/// </summary>
[InitializeOnLoad]
public static class Carro015Verify
{
    private const string TempDir = @"C:\Users\User\AppData\Local\Temp\opencode\";
    private const string Report  = TempDir + "carro-verificar.txt";
    private const string Flag    = TempDir + "carro-verificar.flag";
    private const string Marca   = TempDir + "carro-verificar.marca";

    private static bool _armado;
    private static float _t0;
    private static int _fase;
    private static Carro015Control _control;
    private static Vector3 _posAnterior;

    static Carro015Verify()
    {
        EditorApplication.update += Vigilar;
    }

    private static void Vigilar()
    {
        if (File.Exists(Flag))
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating)
                return;

            File.Delete(Flag);
            File.WriteAllText(Marca, DateTime.Now.ToString("HH:mm:ss"));
            EditorApplication.isPlaying = true;
            return;
        }

        if (!EditorApplication.isPlaying || !File.Exists(Marca))
            return;

        if (!_armado)
        {
            _armado = true;
            _t0 = Time.realtimeSinceStartup;
            _fase = 0;
            _control = null;
            File.Delete(Report);
            Application.runInBackground = true; // para que el juego corra aunque Unity no tenga el foco
            Registrar("=== prueba carro 015 " + DateTime.Now.ToString("HH:mm:ss") + " ===");
            Registrar("Keyboard.current = " + (Keyboard.current == null ? "NULL" : Keyboard.current.name)
                      + "  raton = " + (Mouse.current == null ? "NULL" : Mouse.current.name));
        }

        float t = Time.realtimeSinceStartup - _t0;

        if (_control == null)
        {
            _control = UnityEngine.Object.FindAnyObjectByType<Carro015Control>();

            if (_control == null)
            {
                if (t > 20f)
                    Cerrar("No se encontro Carro015Control");

                return;
            }

            Registrar("control en '" + _control.gameObject.name + "': cajones="
                      + (_control.cajones == null ? 0 : _control.cajones.Length)
                      + "  pos=" + _control.transform.position.ToString("0.###"));
            _posAnterior = _control.transform.position;
        }

        switch (_fase)
        {
            case 0:
                if (t < 1.5f) break;
                Registrar("t=" + t.ToString("0.0") + "  pos inicial=" + _control.transform.position.ToString("0.###")
                          + "  cajonesAbiertos=" + _control.CajonesAbiertos);
                _fase = 1;
                break;

            case 1:
                if (t < 3f) break;
                Pulsar(Key.S);
                Registrar("t=" + t.ToString("0.0") + "  PULSO S (abrir)");
                _fase = 2;
                break;

            case 2:
                if (t < 3.4f) break;
                Soltar();
                _fase = 3;
                break;

            case 3:
                if (t < 6f) break;
                Registrar("t=" + t.ToString("0.0") + "  abiertos=" + _control.CajonesAbiertos
                          + "  cajon0 local=" + Local(_control, 0) + "  cajon1 local=" + Local(_control, 1));
                Pulsar(Key.W);
                Registrar("t=" + t.ToString("0.0") + "  PULSO W (adelante)");
                _fase = 4;
                break;

            case 4:
                if (t < 8f) break;
                Soltar();
                Registrar("t=" + t.ToString("0.0") + "  tras W: pos=" + _control.transform.position.ToString("0.###")
                          + "  desplazamiento=" + (_control.transform.position - _posAnterior).ToString("0.###")
                          + "  modulo=" + (_control.transform.position - _posAnterior).magnitude.ToString("0.###"));
                _posAnterior = _control.transform.position;
                _fase = 5;
                break;

            case 5:
                if (t < 8.4f) break;
                Pulsar(Key.A);
                Registrar("t=" + t.ToString("0.0") + "  PULSO A (derecha)");
                _fase = 6;
                break;

            case 6:
                if (t < 10.4f) break;
                Soltar();
                Registrar("t=" + t.ToString("0.0") + "  tras A: desplazamiento="
                          + (_control.transform.position - _posAnterior).ToString("0.###"));
                _posAnterior = _control.transform.position;
                _fase = 7;
                break;

            case 7:
                if (t < 10.8f) break;
                Pulsar(Key.D);
                Registrar("t=" + t.ToString("0.0") + "  PULSO D (izquierda)");
                _fase = 8;
                break;

            case 8:
                if (t < 12.8f) break;
                Soltar();
                Registrar("t=" + t.ToString("0.0") + "  tras D: desplazamiento="
                          + (_control.transform.position - _posAnterior).ToString("0.###"));
                _posAnterior = _control.transform.position;
                _fase = 9;
                break;

            case 9:
                if (t < 13.2f) break;
                Pulsar(Key.S);
                Registrar("t=" + t.ToString("0.0") + "  PULSO S (cerrar)");
                _fase = 10;
                break;

            case 10:
                if (t < 13.6f) break;
                Soltar();
                _fase = 11;
                break;

            case 11:
                if (t < 16.5f) break;
                Registrar("t=" + t.ToString("0.0") + "  abiertos=" + _control.CajonesAbiertos
                          + "  cajon0 local=" + Local(_control, 0) + "  cajon1 local=" + Local(_control, 1));
                Cerrar("fin");
                _fase = 12;
                break;
        }
    }

    private static string Local(Carro015Control control, int indice)
    {
        if (control.cajones == null || indice >= control.cajones.Length || control.cajones[indice] == null)
            return "-";

        return control.cajones[indice].localPosition.ToString("0.###");
    }

    private static void Pulsar(Key tecla)
    {
        if (Keyboard.current == null) return;
        InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState(tecla));
        InputSystem.Update();
    }

    private static void Soltar()
    {
        if (Keyboard.current == null) return;
        InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState());
        InputSystem.Update();
    }

    private static void Cerrar(string motivo)
    {
        Registrar("--- " + motivo + " ---");
        _armado = false;

        if (File.Exists(Marca))
            File.Delete(Marca);

        EditorApplication.isPlaying = false;
    }

    private static void Registrar(string linea)
    {
        File.AppendAllText(Report, linea + Environment.NewLine);
        Debug.Log("[Carro015Test] " + linea);
    }
}
