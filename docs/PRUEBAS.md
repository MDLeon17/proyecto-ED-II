# Datos, consultas y pruebas

## Archivos entregados

`Data/Samples/logistica.json` contiene 24 municipios, 30 envíos y 44 relaciones. Los envíos son los registros principales del escenario. Se conservan los 24 municipios del mapa, sin agregar municipios ficticios para alcanzar el mínimo de registros.

Cada relación bidireccional se expande a dos arcos. El ejemplo incluye 39 de esas relaciones y 5 de un solo sentido: 83 arcos en memoria. Las cinco dirigidas son SAL→OLI, SIJ→FRA, SIG→ESP, MAR→PAL y FLO→COA. Aunque no exista el arco inverso directo, puede existir un camino alternativo por otros municipios.

`Data/Samples/consultas.json` contiene 40 consultas: cinco para cada una de las ocho opciones. La suite las lee y ejecuta; no es solo un listado ilustrativo.

## Formato de datos

El archivo tiene tres arreglos obligatorios: `nodes`, `routes`, `orders`. Fragmentos de ejemplo:

```json
{
  "nodes": [
    {
      "id": "QUE", "code": "QUE", "name": "Quetzaltenango",
      "mapNumber": 1, "x": 619, "y": 420,
      "regionX": 622, "regionY": 433,
      "type": "AirHub", "region": "Quetzaltenango", "capacity": "Simulada"
    }
  ],
  "routes": [],
  "orders": []
}
```

Un registro de `routes`:

```json
{
  "fromId": "QUE", "toId": "SAL", "distanceKm": 11,
  "cost": 23.1, "estimatedHours": 0.43,
  "bidirectional": true, "mode": "Ground"
}
```

Un registro de `orders`:

```json
{
  "guia": "UFG-DEMO-001",
  "remitente": "Remitente de prueba 01",
  "destinatario": "Destinatario de prueba 01",
  "origenId": "QUE", "destinoId": "CAB"
}
```

Estos fragmentos explican el formato; para una red completa utiliza el archivo entregado. Los IDs deben existir antes de referenciarlos en rutas o envíos. Los pesos son obligatorios, finitos y no negativos; se aceptan ceros. Se rechazan guías, IDs, números de mapa y arcos repetidos. Origen y destino de un envío deben ser distintos. Las semillas deben identificar regiones diferentes y válidas de la imagen.

## Cinco consultas por opción

| Opción | Entradas en orden | Resultados esperados |
|---|---|---|
| Hash | `UFG-DEMO-001`, `UFG-DEMO-010`, `UFG-DEMO-030`, `NO-EXISTE`, `UFG-DEMO-031` | Existe, existe, existe, no existe, no existe. |
| Prefijo | `san`, `co`, `gén`, `sal`, `zzz` | 6, 3, 1, 1 y 0 sugerencias. |
| BFS | QUE→COA, CAB→CAN, GEN→SAL, QUE→QUE, QUE→NO | Ruta, ruta, ruta, camino de un nodo, sin ruta por ID desconocido. Cada ruta exitosa minimiza tramos. |
| DFS | QUE→COA, CAB→CAN, GEN→SAL, QUE→QUE, QUE→NO | Ruta válida, ruta válida, ruta válida, camino de un nodo, sin ruta. No promete mínimos. |
| Dijkstra: distancia | QUE→COA, CAB→CAN, GEN→SAL, QUE→QUE, QUE→NO | Ruta de distancia mínima en las primeras tres; cero para el mismo municipio; sin ruta para ID desconocido. |
| Dijkstra: costo | QUE→COA, CAB→CAN, GEN→SAL, QUE→QUE, QUE→NO | Ruta de costo mínimo en las primeras tres; cero para el mismo municipio; sin ruta para ID desconocido. |
| Dijkstra: tiempo | QUE→COA, CAB→CAN, GEN→SAL, QUE→QUE, QUE→NO | Ruta de tiempo mínimo en las primeras tres; cero para el mismo municipio; sin ruta para ID desconocido. |
| Conectividad | QUE→COA, CAB→CAN, GEN→SAL, QUE→QUE, QUE→NO | Mismo grupo en las primeras cuatro; falso para el ID desconocido. |

`NO` es intencionalmente inexistente: se prueba desde el código, porque la interfaz permite seleccionar únicamente municipios válidos. En las consultas del archivo, `expected` expresa existencia, cantidad de sugerencias o pertenencia al grupo. Los óptimos y la consistencia de los totales se verifican además con las pruebas independientes.

### Prefijos concretos

- `san`: San Carlos Sija, San Francisco La Unión, San Juan Ostuncalco, San Martín Sacatepéquez, San Mateo y San Miguel Sigüilá.
- `co`: Coatepeque, Colomba Costa Cuca y Concepción Chiquirichapa.
- `gén` y `GEN`: Génova.
- `sal`: Salcajá.
- `zzz`: ningún resultado.

## Casos independientes de los datos de ejemplo

La suite crea un grafo A, B, C, D, E, inicialmente con E aislado:

| Arco | Distancia | Costo | Tiempo |
|---|---:|---:|---:|
| A→D | 20 | 20 | 1 |
| A→B | 1 | 10 | 4 |
| B→D | 1 | 10 | 4 |
| A→C | 5 | 1 | 3 |
| C→D | 5 | 1 | 3 |

Resultados A→D: BFS toma un tramo; Dijkstra por distancia toma A→B→D (2); por costo toma A→C→D (2); por tiempo toma A→D (1). D→A no tiene ruta aunque ambos estén en el mismo grupo débil. E pertenece a otro grupo. Después se agrega D→E con pesos cero y se verifica que se actualizan los componentes y existe camino desde A.

Además se prueban colisiones deliberadas de hash con `Aa` y `BB`, eliminación, 2,000 inserciones con redimensionamiento, claves con hash negativo en la tabla genérica, cola vacía reutilizada y operaciones de lista dinámica. La carga se prueba con JSON mal formado, campos ausentes, guías e IDs repetidos, destinos inválidos y pesos negativos.

## Ejecutar la verificación

Desde PowerShell en la raíz del proyecto:

```powershell
dotnet build UfgEnvios.sln -c Release
dotnet run --project Tests/MapRegionCheck.csproj -c Release -- "imagen ejemplo.jpg" "artifacts/Verificacion.png"
```

El código de salida esperado es cero. La consola informa la cantidad de comprobaciones. Si falla una aserción, indica el caso y termina con error. La imagen de referencia se pasa como primer argumento; el segundo argumento opcional habilita capturas.

Se comprueban los óptimos de Dijkstra para **24×24×3 = 1,728 consultas**, usando Floyd-Warshall con matrices y sin reutilizar la lógica de Dijkstra. También se verifican que los arcos del camino existen y que sus tres pesos suman los totales informados.

La parte WPF verifica el estado inicial, la navegación, los 24 contornos, el alta con remitente y destinatario, la selección de guía, la animación simultánea BFS/DFS, el autocompletado visible, la consulta de costo y la animación Dijkstra. Genera capturas de Registro, menú, Envío, Consultas y Dijkstra, incluidas vistas a 1200×720. El historial se inspecciona también mediante una simulación completada desde la aplicación.

Si Windows impide cargar el ejecutable o un DLL por una política de Control de aplicaciones, la suite no llega a ejecutar las aserciones. Esa ejecución debe repetirse en un equipo donde la política permita ejecutar el proyecto; el mensaje del sistema no equivale a una prueba fallida del algoritmo.

## Demostración manual

1. Iniciar: Registro debe mostrar 30 paquetes.
2. Intentar registrar sin destinatario: debe rechazarlo y conservar la cantidad.
3. Registrar un envío de Quetzaltenango a Salcajá con ambas personas: debe aparecer una guía nueva.
4. Enviar el seleccionado; usar Paralelo para comprobar dos mapas y Dijkstra para consultar un óptimo.
5. Confirmar Entregado y su entrada de Historial con remitente y destinatario.
6. Buscar la guía en Consultas y datos; probar `san`, `co` y `gén`.
7. Consultar conectividad y explicar el sentido de las flechas.
8. Cargar una copia inválida del JSON: deben conservarse los datos de la sesión.
9. Cargar el JSON original: deben volver los 30 paquetes y vaciarse el historial.
10. Cerrar y reabrir: confirmar que se vuelven a cargar los datos iniciales.
