// Construye la pantalla de victoria por código en tiempo de ejecución.
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class MenuVictoria : MonoBehaviour
{
    // Fuente usada en todos los textos de la UI
    private Font uiFont;

    private void Start()
    {
        uiFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); 
        if (uiFont == null)
            uiFont = Resources.GetBuiltinResource<Font>("Arial.ttf"); // versiones anteriores (manejo de errores)

        BuildUI();
    }

    private void BuildUI()
    {
        // Obtener o crear el Canvas de este GameObject
        var canvas = GetComponent<Canvas>();
        if (canvas == null)
        {
            canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay; // se dibuja encima de todo
            canvas.sortingOrder = 100; // prioridad alta por si hay otros canvas en escena
        }

        // CanvasScaler: escala la UI según la resolución de referencia
        if (GetComponent<CanvasScaler>() == null)
        {
            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
        }

        // GraphicRaycaster: necesario para que los botones reciban clics
        if (GetComponent<GraphicRaycaster>() == null)
            gameObject.AddComponent<GraphicRaycaster>();

        // Fondo: screenshot del juego difuminado; color oscuro si no hay captura disponible
        if (GameManager.capturedScreenshot != null)
        {
            var bgObj = NewChild("Background", transform);
            var raw = bgObj.AddComponent<RawImage>();
            raw.texture = BlurTexture(GameManager.capturedScreenshot);
            FullScreen(raw.rectTransform);
        }
        else
        {
            var bgObj = NewChild("Background", transform);
            var img = bgObj.AddComponent<Image>();
            img.color = new Color(0.08f, 0.06f, 0.04f);
            FullScreen(img.rectTransform);
        }

        // Capa negra semitransparente para oscurecer el fondo y mejorar la legibilidad
        {
            var go = NewChild("Overlay", transform);
            var img = go.AddComponent<Image>();
            img.color = new Color(0f, 0f, 0f, 0.58f);
            FullScreen(img.rectTransform);
        }

        // Coordenadas del panel central (en espacio de anchors 0-1)
        const float pxMin = 0.28f, pyMin = 0.16f, pxMax = 0.72f, pyMax = 0.84f;
        const float borderPx = 4f;

        // Borde dorado: imagen ligeramente más grande que el panel, colocada antes en la jerarquía
        {
            var go = NewChild("PanelBorder", transform);
            var img = go.AddComponent<Image>();
            img.color = new Color(0.85f, 0.68f, 0.10f);
            var rt = img.rectTransform;
            rt.anchorMin = new Vector2(pxMin, pyMin);
            rt.anchorMax = new Vector2(pxMax, pyMax);
            rt.offsetMin = new Vector2(-borderPx, -borderPx); // sobresale borderPx píxeles
            rt.offsetMax = new Vector2(borderPx, borderPx);
        }

        // Panel central oscuro sobre el que se colocan todos los elementos de UI
        var panel = NewChild("Panel", transform);
        {
            var img = panel.AddComponent<Image>();
            img.color = new Color(0.10f, 0.07f, 0.03f, 0.90f);
            var rt = img.rectTransform;
            rt.anchorMin = new Vector2(pxMin, pyMin);
            rt.anchorMax = new Vector2(pxMax, pyMax);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        // Título principal
        AddText(panel.transform, "Title",
            "¡MISIÓN COMPLETADA!",
            56, FontStyle.Bold,
            new Color(0.95f, 0.82f, 0.18f),
            0.04f, 0.70f, 0.96f, 0.96f);

        // Subtítulo descriptivo
        AddText(panel.transform, "Subtitle",
            "El ladrón ha escapado con el botín",
            24, FontStyle.Italic,
            new Color(0.85f, 0.76f, 0.60f),
            0.06f, 0.52f, 0.94f, 0.68f);

        // Línea separadora decorativa entre texto y botones
        {
            var go = NewChild("Separator", panel.transform);
            var img = go.AddComponent<Image>();
            img.color = new Color(0.75f, 0.60f, 0.08f, 0.55f);
            Anchor(img.rectTransform, 0.10f, 0.495f, 0.90f, 0.505f);
        }

        // Botón Reiniciar: recarga la escena de juego
        CreateButton(panel.transform, "BtnReiniciar", "REINICIAR",
            0.07f, 0.08f, 0.46f, 0.42f,
            new Color(0.14f, 0.34f, 0.14f),
            new Color(0.20f, 0.50f, 0.20f),
            VolverAJugar);

        // Botón Salir: sale del modo ejecución (editor) o cierra la app (build)
        CreateButton(panel.transform, "BtnSalir", "SALIR",
            0.54f, 0.08f, 0.93f, 0.42f,
            new Color(0.38f, 0.08f, 0.08f),
            new Color(0.55f, 0.12f, 0.12f),
            SalirDelJuego);
    }

    // Acciones de los botones

    public void VolverAJugar()
    {
        SceneManager.LoadScene("Castle");
    }

    public void SalirDelJuego()
    {
        UnityEditor.EditorApplication.isPlaying = false;
    }

    // Helpers de construcción de UI

    // Crea un GameObject vacío hijo del transform indicado
    private static GameObject NewChild(string name, Transform parent)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        return go;
    }

    // Estira el RectTransform para ocupar todo el espacio de su padre
    private static void FullScreen(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    // Posiciona el RectTransform mediante coordenadas de anchor (0-1)
    private static void Anchor(RectTransform rt, float xMin, float yMin, float xMax, float yMax)
    {
        rt.anchorMin = new Vector2(xMin, yMin);
        rt.anchorMax = new Vector2(xMax, yMax);
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    // Crea un Text con los parámetros dados y lo ancla dentro de su padre
    private void AddText(Transform parent, string name, string content,
        int size, FontStyle style, Color color,
        float xMin, float yMin, float xMax, float yMax)
    {
        var go = NewChild(name, parent);
        var t = go.AddComponent<Text>();
        t.text = content;
        t.font = uiFont;
        t.fontSize = size;
        t.fontStyle = style;
        t.color = color;
        t.alignment = TextAnchor.MiddleCenter;
        Anchor(t.rectTransform, xMin, yMin, xMax, yMax);
    }

    // Crea un botón con imagen de fondo coloreada, texto y callback al hacer clic
    private void CreateButton(Transform parent, string name, string label,
        float xMin, float yMin, float xMax, float yMax,
        Color normal, Color hover,
        UnityEngine.Events.UnityAction action)
    {
        var go = NewChild(name, parent);
        var img = go.AddComponent<Image>();
        img.color = Color.white; // el ColorBlock del Button aplica el tint sobre este blanco
        Anchor(img.rectTransform, xMin, yMin, xMax, yMax);

        var btn = go.AddComponent<Button>();
        var cb = btn.colors;
        cb.normalColor      = normal;
        cb.highlightedColor = hover;
        cb.pressedColor     = normal * 0.7f;
        cb.selectedColor    = normal;
        cb.colorMultiplier  = 1f;
        btn.colors = cb;
        btn.onClick.AddListener(action);

        // Texto del botón
        var labelGo = NewChild("Label", go.transform);
        var t = labelGo.AddComponent<Text>();
        t.text = label;
        t.font = uiFont;
        t.fontSize = 26;
        t.fontStyle = FontStyle.Bold;
        t.color = Color.white;
        t.alignment = TextAnchor.MiddleCenter;
        FullScreen(t.rectTransform);
    }

    // Blur: reduce la textura a 1/8 de su tamaño
    private static Texture2D BlurTexture(Texture2D src)
    {
        int w = Mathf.Max(1, src.width / 8);
        int h = Mathf.Max(1, src.height / 8);

        var rt = RenderTexture.GetTemporary(w, h, 0, RenderTextureFormat.ARGB32);
        rt.filterMode = FilterMode.Bilinear;
        Graphics.Blit(src, rt); // copia src en el RenderTexture pequeño

        var prev = RenderTexture.active;
        RenderTexture.active = rt;

        var blurred = new Texture2D(w, h, TextureFormat.RGB24, false);
        blurred.filterMode = FilterMode.Bilinear;
        blurred.ReadPixels(new Rect(0, 0, w, h), 0, 0); // lee píxeles del RenderTexture
        blurred.Apply();

        RenderTexture.active = prev;
        RenderTexture.ReleaseTemporary(rt);

        return blurred;
    }
}
