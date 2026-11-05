# UFG ENVIOS

Simulador de envíos entre los **24 municipios de Quetzaltenango**, desarrollado en **C#, WPF y .NET 8** para Estructura de Datos II. Registra remitente y destinatario, busca paquetes por guía, autocompleta municipios y compara recorridos con rutas óptimas por distancia, costo o tiempo.

## Ejecutar

Requiere Windows y el SDK de .NET 8 o uno compatible. En Visual Studio, instala la carga de trabajo de desarrollo de escritorio .NET, abre `UfgEnvios.sln` y presiona F5. Desde PowerShell, en la raíz del repositorio:

```powershell
dotnet run --project UfgEnvios.csproj
```

Al iniciar se carga `Data/Samples/logistica.json`, copiado automáticamente a la carpeta del ejecutable. No depende del directorio desde el que se abra la aplicación.

## Funciones

- **Registro:** alta y eliminación de paquetes con guía única, remitente, destinatario y municipios de origen/destino.
- **Envío:** selecciona un paquete registrado y pulsa Simular envío. El botón **⋮** permite elegir BFS, DFS, comparación paralela o Dijkstra; para Dijkstra se elige Distancia, Costo o Tiempo.
- **Historial:** conserva las simulaciones completadas durante la sesión, con ambas personas y el método usado.
- **Consultas y datos:** busca una guía, escribe un prefijo como `san` o `gén`, consulta BFS/DFS/Dijkstra o verifica los grupos y la disponibilidad dirigida de una ruta. La búsqueda de guía selecciona el paquete para simularlo en Envío.
- **Editar grafos:** abre una ventana desde el menú ☰. Haz clic en dos municipios para agregar una conexión unidireccional o bidireccional y su distancia simulada. Selecciona una línea o una fila para cambiar el sentido o eliminarla; los recorridos y la conectividad se actualizan inmediatamente.
- **Reemplazar datos desde JSON:** carga otro conjunto completo. Primero valida los datos y los contornos; si falla, conserva la sesión actual. Si tiene éxito, reemplaza los envíos y vacía el historial.

Los registros, los cambios de estado y las conexiones editadas se mantienen **en memoria**. Al cerrar y abrir, se vuelve a cargar el JSON; no se guarda automáticamente sobre el archivo.

## Estructuras integradas

| Estructura | Uso y justificación |
|---|---|
| Tabla hash propia | Buscar, insertar y eliminar paquetes por guía; encadenamiento y redimensionamiento. |
| Grafo con listas de adyacencia propias | Municipios y rutas dirigidas o bidireccionales; BFS, DFS y Dijkstra. |
| Union-Find propio | Componentes de conectividad débil y descarte de rutas entre grupos distintos. |
| Trie propio | Autocompletar nombres desde un prefijo, ignorando tildes y mayúsculas. |
| Lista dinámica, cola, conjunto y tabla de dispersión propios | Almacenamiento, BFS, visitas, índices, contornos, animación e historial. |

No se usan `List<T>`, `Dictionary<TKey,TValue>`, `HashSet<T>`, `Queue<T>` ni `ObservableCollection<T>` como estructuras de la aplicación. Se usan arreglos, interfaces de enlace de WPF, LINQ y las colecciones internas de controles y geometrías de WPF.

## Datos y consultas de prueba

- [Datos de ejemplo](Data/Samples/logistica.json): **30 envíos**, 24 municipios y **44 relaciones**. Son 39 bidireccionales y 5 de un sentido, almacenadas como 83 arcos.
- [Consultas ejecutables](Data/Samples/consultas.json): **40 consultas**, cinco para cada opción: hash, prefijo, BFS, DFS, Dijkstra por distancia/costo/tiempo y conectividad.
- [Guía de pruebas y demostración](docs/PRUEBAS.md): entradas, resultados esperados y comandos.

Los 30 registros principales son los **envíos**; los 24 municipios coinciden con el mapa existente. Las distancias (km), los costos (Q) y los tiempos (h) son simulados. Las flechas señalan rutas de un sentido; pasar el cursor sobre una conexión muestra sus pesos. No son instrucciones de navegación real.

## Verificar

```powershell
dotnet build UfgEnvios.sln -c Release
dotnet run --project Tests/MapRegionCheck.csproj -c Release -- "imagen ejemplo.jpg" "artifacts/Verificacion.png"
```

La suite comprueba estructuras, datos inválidos, las 40 consultas, rutas dirigidas, pesos cero, municipios aislados y los óptimos de Dijkstra para los 576 pares de municipios y los tres pesos, usando Floyd-Warshall como referencia independiente. También prueba el editor de grafos (clics, altas, cambios de sentido y bajas), el registro, los contornos, la búsqueda visible, el Trie y las animaciones BFS/DFS y Dijkstra. Genera capturas en `artifacts/`.

## Documentación

- [Manual técnico](docs/MANUAL_TECNICO.md): diseño, clases, integración, complejidades y decisiones.
- [Manual de usuario](docs/MANUAL_USUARIO.md): pasos de cada pantalla.
- [Pruebas](docs/PRUEBAS.md): consultas y demostración.
- [Aportes del equipo](docs/APORTES.md): responsabilidades comunicadas por el equipo.
- [Guion de exposición](docs/PRESENTACION.md): demostración de aproximadamente seis minutos.

Repositorio configurado: [MDLeon17/proyecto-ED-II](https://github.com/MDLeon17/proyecto-ED-II), con visibilidad pública verificada el 4 de noviembre de 2026. Los cambios locales deben publicarse por los integrantes con sus commits reales antes de entregar.
