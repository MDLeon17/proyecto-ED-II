using System.Windows;
using System.Windows.Controls;

namespace UfgEnvios;

public partial class MainWindow
{
    private readonly Button _navGraphEditor = new();

    private void ConfigurarEditorGrafos()
    {
        _navGraphEditor.Content = "Editar grafos";
        _navGraphEditor.Style = (Style)FindResource("SecondaryButton");
        _navGraphEditor.Margin = new Thickness(0, 8, 0, 0);
        _navGraphEditor.ToolTip = "Agregar conexiones haciendo clic en el mapa, cambiar su sentido o eliminarlas.";
        _navGraphEditor.Click += AbrirEditorGrafos_Click;
        ((StackPanel)NavSimulation.Parent).Children.Add(_navGraphEditor);
        RegisterName("NavGraphEditor", _navGraphEditor);
    }

    private void AbrirEditorGrafos_Click(object sender, RoutedEventArgs e)
    {
        if (_runCts != null || _mapRegions == null) return;
        BtnNavigation.IsChecked = false;
        var editor = new GraphEditorWindow(_graph, _mapRegions) { Owner = this };
        editor.GraphChanged += (_, _) =>
        {
            ClearPresentation();
            RenderMaps();
            ActualizarResumenDatos();
            _consultaResultado.Text = "La red de conexiones cambió. Ejecuta la consulta nuevamente para obtener el recorrido actualizado.";
            AddLog("Red de conexiones actualizada desde Editar grafos.");
        };
        editor.ShowDialog();
    }
}
