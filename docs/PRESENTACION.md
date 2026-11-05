# Guion de exposición — aproximadamente 6 minutos

## 0:00–0:40 · Problema y escenario

Presentar UFG ENVIOS como simulador de paquetes entre los 24 municipios del mapa. Mostrar que se carga un archivo con 30 envíos y 44 relaciones. Explicar que los pesos son simulados y que hay conexiones de uno y dos sentidos.

## 0:40–1:40 · Estructuras y selección

Describir la tabla hash para consultar guías; el grafo para rutas; el Trie para prefijos y Union-Find para grupos. Mostrar el flujo JSON → estructuras → consulta → recorrido → estado e historial. Explicar por qué una tabla hash sirve para guías exactas y el Trie para prefijos.

## 1:40–2:40 · Búsqueda, prefijos y conectividad

En Consultas y datos buscar `UFG-DEMO-001` y mostrar las dos personas. Escribir `san` y luego `gén`; seleccionar Génova y consultar conectividad desde Quetzaltenango. Explicar que estar en el mismo grupo no demuestra una ruta dirigida: BFS comprueba los sentidos.

## 2:40–4:20 · Demostración del recorrido

Registrar un paquete de Quetzaltenango a Salcajá y abrir Envío. Seleccionar Paralelo y una velocidad rápida: BFS y DFS dibujan sus caminos al mismo tiempo. Comparar tramos y pesos. Después seleccionar Dijkstra por costo y repetir. Mostrar el cambio a Entregado y la entrada del Historial.

Si queda poco tiempo, consultar Dijkstra por costo directamente en Consultas y datos para evitar esperar otra animación.

## 4:20–5:20 · Decisiones y pruebas

Explicar los sacrificios: la tabla hash consume cubetas; el Trie consume nodos; Union-Find conserva grupos débiles y Dijkstra con montículo propio es O((V + E) log V) y necesita código adicional para el montículo. BFS minimiza tramos y Dijkstra optimiza un peso; DFS no garantiza un óptimo.

Mostrar `consultas.json`: cinco consultas por opción, incluidas guías y municipios inexistentes. Explicar que las pruebas contrastan Dijkstra con Floyd-Warshall en todos los pares y en los tres criterios, además de probar una red con óptimos distintos, un nodo aislado y aristas dirigidas.

## 5:20–6:00 · Resultados, límites y aprendizajes

Se logró integrar las cuatro estructuras con registro, búsqueda, consulta y simulación. Destacar el aprendizaje de distinguir menor número de aristas, menor peso y conectividad dirigida. Explicar que los datos de sesión no se guardan automáticamente y que una ampliación futura puede agregar persistencia y un montículo para redes mayores. Mostrar los manuales y los aportes reales del equipo.

## Preparación

- Abrir y ejecutar el proyecto antes de presentar; comprobar los datos de ejemplo y las animaciones en el equipo de exposición.
- Tener a mano README, manuales y resultados de pruebas.
- Mantener la explicación entre 5 y 7 minutos. Cualquier integrante debe practicar la demostración completa.
