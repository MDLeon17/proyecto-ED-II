# Manual de usuario — UFG ENVIOS

## Inicio

Abre `UfgEnvios.sln` en Visual Studio y ejecuta con F5, o usa `dotnet run --project UfgEnvios.csproj` desde la carpeta del proyecto. Se necesita Windows y .NET 8 o un SDK compatible con ese destino.

La pantalla inicial es **Registro**. Ya hay 30 paquetes de prueba. El menú ☰ permite cambiar entre Registro, Envío, Historial y Consultas y datos, y abrir la ventana **Editar grafos**. La ventana principal admite un tamaño mínimo de 1200×720.

## Registrar un paquete

1. En Registro, escribe el **nombre del remitente** y el **destinatario**.
2. Selecciona municipios de origen y destino distintos.
3. La guía se genera automáticamente. Puedes renovarla con ↻ antes de registrar.
4. Pulsa **Registrar en tabla hash**. El paquete aparece en la tabla con estado Registrado y queda seleccionado.
5. Para eliminarlo, selecciona su fila y pulsa **Eliminar pedido**; confirma en el mensaje de la aplicación.

Los datos incompletos muestran un aviso en el formulario. Una guía repetida no se registra. La tabla muestra guía, remitente, destinatario, origen, destino y estado. Los contadores resumen los pedidos registrados, en tránsito, entregados y sin ruta.

## Simular un envío

1. Selecciona un paquete en Registro y pulsa **Simular envío seleccionado**, o entra a Envío y elige su guía en **Número de paquete**.
2. Comprueba origen, destino y estado en las tarjetas superiores.
3. Abre **⋮**, junto a Simular envío, para elegir el método:

| Método | Resultado |
|---|---|
| BFS | Ruta con la menor cantidad de tramos. |
| DFS | Ruta encontrada explorando una rama en profundidad. |
| Paralelo | Dos mapas con BFS y DFS animados al mismo tiempo. |
| Dijkstra | Ruta con menor distancia, costo o tiempo, según Optimizar Dijkstra. |

4. Ajusta la velocidad de animación en ese mismo panel.
5. Pulsa **Simular envío**. El mapa dibuja los tramos mientras avanza el vehículo.
6. Consulta kilómetros, horas, costo y registro de eventos. Al terminar, el paquete pasa a Entregado y aparece en Historial.
7. **Limpiar recorrido** borra la presentación. Durante un recorrido también permite cancelarlo; el paquete conserva el estado que tenía antes de iniciar esa simulación.

Las flechas pequeñas indican conexiones de un solo sentido. Pasa el cursor sobre una conexión para consultar sus pesos. Los colores distinguen origen, destino y recorrido. Todos los pesos son datos simulados.

## Agregar, editar y eliminar conexiones del grafo

1. Abre **☰ → Editar grafos**. Se abre una ventana con el mapa y las conexiones actuales.
2. Pulsa **Nueva conexión** y haz clic en el municipio de origen y después en uno de destino. Puedes pulsar el número o el interior del municipio.
3. Elige **Unidireccional** (solo origen → destino) o **Bidireccional** (ida y vuelta).
4. Ingresa la distancia simulada en kilómetros, con punto o coma decimal, y pulsa **Agregar conexión**. El costo y el tiempo se calculan con la misma estimación de las demás rutas. Se admite distancia cero.
5. Para editar, selecciona una línea del mapa o una fila de la lista, elige el sentido y pulsa **Guardar sentido**. La distancia, el costo y el tiempo se conservan.
6. Para elegir qué sentido conservar al convertir una conexión bidireccional en unidireccional, pulsa **Nueva conexión** y selecciona primero el origen deseado y después el destino. La conexión existente se abre para editarla.
7. Para eliminar una conexión seleccionada, pulsa **Eliminar**. Si es bidireccional, se eliminan ambos sentidos. Si son dos conexiones unidireccionales independientes, se elimina solamente la seleccionada.

El editor impide conectar un municipio consigo mismo y duplicar un sentido existente. Si ya existe una conexión independiente en el sentido contrario, debes eliminarla antes de convertir la seleccionada en bidireccional. La lista permite seleccionar cada una de las dos conexiones independientes.

Los cambios se aplican inmediatamente a BFS, DFS, Dijkstra, los mapas y los grupos de conectividad. Durante una animación se deshabilita la entrada al editor; puedes abrirlo al terminar o cancelar el recorrido. Cierra la ventana para volver a la pantalla anterior. Las conexiones editadas duran durante la sesión y no se guardan sobre el JSON.

## Buscar por guía

1. Abre **Consultas y datos**.
2. Escribe una guía exacta, por ejemplo `UFG-DEMO-001`, y pulsa **Buscar guía**.
3. Se muestran remitente, destinatario, origen, destino y estado. El paquete queda seleccionado para simularlo en Envío.
4. Si no existe, aparece el mensaje correspondiente. La búsqueda distingue mayúsculas y minúsculas.

## Autocompletar y consultar rutas

1. En Consultas y datos, escribe un prefijo en **Autocompletar municipio**: `san`, `co` o `gén`.
2. El Trie muestra los nombres coincidentes. La búsqueda ignora tildes y mayúsculas. Un prefijo vacío muestra los municipios disponibles, hasta el límite de sugerencias.
3. Selecciona una sugerencia para colocarla como destino de la consulta. También puedes elegir origen y destino en las listas.
4. Pulsa **BFS**, **DFS**, **Dijkstra: km**, **Dijkstra: costo** o **Dijkstra: tiempo**.
5. El panel derecho muestra el camino, los tres totales, los tramos y el orden de exploración. Estas consultas no cambian el estado de un paquete ni inician una animación.

Para una animación usa un paquete registrado con esos municipios. La consulta permite también origen igual a destino: devuelve cero tramos y totales cero.

## Consultar conectividad

Selecciona dos municipios y pulsa **Conectividad**. Se muestra el número de grupos, si ambos pertenecen al mismo y si existe una ruta dirigida del origen al destino. También se listan los miembros del grupo del origen.

Dos municipios pueden estar en el mismo grupo sin tener ruta en ambos sentidos. Los grupos de Union-Find se forman ignorando la dirección; la disponibilidad del trayecto se comprueba respetando las flechas.

## Cargar otro archivo

1. Prepara un JSON con el formato de `Data/Samples/logistica.json`.
2. Abre Consultas y datos y pulsa **Reemplazar datos desde JSON…**.
3. Elige el archivo. Al cargarse correctamente, reemplaza municipios, rutas y paquetes de la sesión y vacía el historial.
4. Si está incompleto, tiene IDs repetidos, rutas inválidas o coordenadas incompatibles con el mapa, se muestra el error y se conserva la sesión actual.

El mapa sigue representando los municipios de la imagen incluida. Conserva las coordenadas y semillas correctas al modificar sus datos. El [manual técnico](MANUAL_TECNICO.md) y las [pruebas](PRUEBAS.md) explican el formato.

## Historial y cierre

Historial muestra fecha, guía, remitente, destinatario, origen, destino, método y estado de cada simulación completada. Simular de nuevo una guía agrega otro evento de entrega.

Los paquetes creados, los cambios de estado y el historial duran mientras la aplicación está abierta. Al reiniciar se carga nuevamente el archivo inicial. No se guardan automáticamente sobre el JSON.
