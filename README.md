# UFG ENVIOS

Simulador de entregas entre los **24 municipios de Quetzaltenango**, hecho en C# con WPF y .NET 8 para Estructuras de Datos II.

## Ejecutar

Abre `UfgEnvios.sln` en Visual Studio con la carga de trabajo de desarrollo de escritorio .NET y presiona **F5**. También puedes usar:

```powershell
dotnet run --project UfgEnvios.csproj
```

Para verificar la navegación, el registro de pedidos, los 24 contornos y la animación simultánea, además de generar vistas previas de ambas pantallas:

```powershell
dotnet run --project Tests/MapRegionCheck.csproj -c Release -- "imagen ejemplo.jpg" "artifacts/PreviewComparativa.png"
```

Una [vista previa de la comparativa](Assets/PreviewComparativa.png) muestra el resultado con una ruta BFS y otra DFS.

## Uso

1. Abre el **menú hamburguesa** de la esquina superior izquierda y elige **Simular envío** o **Registrar en tabla hash**. Cada sección tiene su propio formulario; cambiar de sección conserva los datos.
2. En **Registrar en tabla hash**, escribe el cliente y selecciona el origen y el destino. Pulsa **Registrar en tabla hash** para guardar el pedido con una guía única y verlo en la tabla. Los pedidos se mantienen en memoria mientras la aplicación está abierta.
3. Selecciona un pedido de la tabla y pulsa **Simular envío seleccionado** para cargar su guía y municipios en el simulador. También puedes elegirlo en la lista del simulador o simular una ruta libre.
4. En **Simular envío**, elige origen y destino en el panel izquierdo o haz clic en dos municipios del mapa. La red completa permanece visible; los contornos de origen y destino se tiñen en azul y violeta y los demás municipios se atenúan.
5. Selecciona BFS, DFS o **Comparar en paralelo**. La comparativa coloca dos mapas lado a lado, uno para cada algoritmo.
6. Pulsa **Trazar y despachar**. El paquete avanza y la ruta se va dibujando por tramos. En comparativa, ambos vehículos arrancan a la vez y cada mapa muestra su progreso. Si cargaste un pedido registrado, al terminar se actualiza a **Entregado** en la tabla.
7. Consulta escalas, kilómetros estimados y el registro de paradas. **Limpiar recorrido** prepara una nueva simulación.

Cada municipio es un vértice del grafo. Las conexiones terrestres bidireccionales son aristas. BFS busca la menor cantidad de aristas, mientras DFS sigue una rama hasta el fondo. En comparativa, ambas rutas se ven por separado y la diferencia en tramos aparece en el análisis.

La imagen `imagen ejemplo.jpg` que ya estaba en el proyecto sirve de referencia geográfica. Los marcadores numerados del 1 al 24 coinciden con los municipios de esa imagen; al pasar el cursor se muestra el nombre completo. La aplicación detecta sus límites claros y los tiñe según la selección. El logo vectorial está en `Assets/UfgEnviosLogo.svg` y se reproduce en la barra lateral. **Las conexiones, kilómetros y tiempos son datos simulados**: no son instrucciones de navegación, rutas viales verificadas ni tiempos de entrega reales. BFS tampoco optimiza necesariamente la suma de kilómetros.

La lista de 24 municipios se contrastó con la [Mapoteca de Quetzaltenango del Sistema Nacional de Información Social](https://snis.mides.gob.gt/mapoteca/quetzaltenango/). Para la nueva interfaz se tomaron como referencia patrones de mapa, puntos de paso y seguimiento visibles en el [concepto de aplicación logística de Ramotion](https://www.ramotion.com/logistics-app-ui-ux-design-concept/).
