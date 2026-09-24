using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Preenchimento circular (fatia radial 0-100%) para os aneis de status.
/// Desenhado via Painter2D: disco de fundo + fatia a partir do topo.
/// </summary>
public class StatRingFill : VisualElement
{
    private float fill = 1f;
    public Color trackColor = new(0.88f, 0.86f, 0.93f, 1f);
    public Color fillColor = Color.white;

    public StatRingFill()
    {
        pickingMode = PickingMode.Ignore;
        generateVisualContent += Draw;
        RegisterCallback<GeometryChangedEvent>(evt => MarkDirtyRepaint());
    }

    public void SetFill(float value, Color color)
    {
        fill = Mathf.Clamp01(value);
        fillColor = color;
        MarkDirtyRepaint();
    }

    private void Draw(MeshGenerationContext mgc)
    {
        float w = contentRect.width;
        float h = contentRect.height;
        if (w <= 0f || h <= 0f)
            return;
        var painter = mgc.painter2D;
        Vector2 center = new(w / 2f, h / 2f);
        float radius = Mathf.Min(w, h) / 2f;
        painter.fillColor = trackColor;
        painter.BeginPath();
        painter.Arc(center, radius, 0f, 360f);
        painter.Fill();
        if (fill <= 0f)
            return;

        painter.fillColor = fillColor;

        // Em 100%, o caminho setorial volta exatamente ao ponto inicial.
        // O Painter2D pode interpretar esse arco de 360 graus com a linha
        // ate o centro como um caminho degenerado (ele aparece deformado ao
        // atingir o maximo). Nesse caso, desenhamos um circulo completo.
        if (fill >= 0.9999f)
        {
            painter.BeginPath();
            painter.Arc(center, radius, 0f, 360f);
            painter.Fill();
            return;
        }

        painter.BeginPath();
        painter.MoveTo(center);
        painter.Arc(center, radius, -90f, -90f + 360f * fill);
        painter.ClosePath();
        painter.Fill();
    }
}
