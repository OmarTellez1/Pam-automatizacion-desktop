# Trabajo con subagentes — Pam Automation .NET

## Propósito

Este documento define cómo debe coordinar el agente principal el trabajo delegado a subagentes. Complementa `AGENTS.md` y no sustituye sus reglas de tecnología, seguridad, alcance, documentación o validación.

La finalidad de los subagentes es reducir tiempo y ruido de contexto al repartir trabajos verdaderamente independientes. No deben utilizarse por costumbre ni para dividir una modificación pequeña que un solo agente puede completar y validar con claridad.

Si el trabajo es pequeño, está concentrado en pocos archivos o no ofrece paralelismo real, el agente principal debe implementarlo y validarlo directamente. Crear un subagente en esos casos duplica contexto, revisión e integración, por lo que aumenta el consumo de tokens y puede tardar más que resolver el cambio en el thread principal.

## Configuración recomendada

- Agente principal o coordinador: GPT-5.6 Sol con razonamiento `medium`.
- Subagentes de implementación: GPT-5.6 Terra con razonamiento `medium`.
- Límite absoluto por task: como máximo dos subagentes, incluso si existen más cupos disponibles.
- Dos subagentes son un límite, no un objetivo. Si solo existe una unidad independiente o el costo de coordinación supera el beneficio, utilizar uno o ninguno.
- `high` se reserva para una revisión puntual que exija lógica compleja, análisis de seguridad, comprobación de supuestos o investigación de un fallo difícil.
- No elevar el razonamiento de todos los agentes como medida preventiva. Un esfuerzo mayor aumenta la latencia y el consumo de tokens.

Si el producto no permite seleccionar exactamente estos modelos, el coordinador debe conservar la separación de responsabilidades y utilizar el modelo disponible más cercano, comunicando la diferencia antes de empezar.

## Responsabilidad del agente principal

El agente principal conserva la autoridad sobre el resultado completo. Debe:

1. Realizar la lectura inicial únicamente si el thread todavía no tiene contexto del proyecto y efectuar las consultas posteriores bajo demanda conforme a `AGENTS.md`.
2. Inspeccionar el estado de Git, la solución y los archivos relevantes antes de delegar.
3. Convertir la fase solicitada en tareas pequeñas, independientes y verificables.
4. Definir para cada subagente el alcance, los archivos permitidos, los archivos prohibidos, los criterios de aceptación y las validaciones esperadas.
5. Preparar aislamiento mediante una rama y un worktree distintos cuando más de un agente vaya a escribir.
6. Esperar los resultados requeridos y revisar los cambios reales, no solo el resumen del subagente.
7. Integrar un trabajo a la vez y resolver conflictos de manera deliberada.
8. Restaurar, compilar y ejecutar las pruebas relacionadas desde la terminal después de integrar.
9. Revisar seguridad, arquitectura, documentación y cumplimiento tecnológico.
10. Informar evidencia concreta de lo que pasó, lo que falló y lo que permanece pendiente.

El coordinador no debe delegar la decisión final, aceptar afirmaciones como “debería funcionar” ni integrar automáticamente porque un subagente indique que terminó.
El coordinador siempre debe de indicar explicitamente a los subagentes que son subagentes.

## Política de contexto y lectura

El agente principal es el propietario del contexto completo del proyecto. La primera vez que un thread entra sin contexto previo debe realizar la lectura inicial definida en `AGENTS.md`, relacionar las decisiones vigentes y entregar a cada subagente únicamente lo necesario para realizar su tarea con seguridad. Las nuevas tareas dentro de ese mismo thread no reinician la lectura: el coordinador consultará documentos bajo demanda y no repetirá todo solo porque él mismo haya actualizado uno o varios Markdown.

Un subagente no debe leer por defecto todos los Markdown ni explorar todo el repositorio. Debe:

- Obedecer las instrucciones aplicables de `AGENTS.md`.
- Trabajar con el resumen y las restricciones proporcionados por el coordinador.
- Leer únicamente los archivos fuente, pruebas y documentos o secciones indicados en su delegación.
- Evitar heredar o reconstruir contexto histórico que no afecte su objetivo.
- Solicitar contexto adicional si falta una decisión necesaria o aparece una contradicción.
- Informar en su resultado cualquier archivo adicional que haya tenido que consultar y por qué.

El coordinador no debe reemplazar una regla crítica por una paráfrasis ambigua. Para seguridad, credenciales, TOTP, origen PAM, creación de solicitudes, correo o persistencia, debe incluir la regla exacta en el prompt o señalar la sección canónica concreta que el subagente debe leer.

Una lectura documental amplia solo se justifica cuando el subagente recibe expresamente una tarea de auditoría global, reconciliación de documentación, revisión arquitectónica transversal o investigación cuyo alcance realmente cruce varias capas. Incluso entonces, el coordinador debe indicar qué documentos son pertinentes; “leer todo por si acaso” no es un criterio válido.

## Cuándo delegar

Antes de delegar, el coordinador debe comparar el ahorro esperado de tiempo con el costo de preparar contexto, aislar cambios, revisar resultados e integrarlos. Debe conservar el trabajo en el agente principal cuando pueda completarlo de forma segura y focalizada sin bloquear otra unidad independiente.

El coordinador debe consultar la clasificación vigente de `Fases.md` como punto de partida, pero no tratarla como autorización automática. El estado real del código, los contratos y los archivos modificados tienen prioridad para decidir si dos trabajos siguen siendo independientes.

Los subagentes son apropiados para:

- Exploraciones de solo lectura con preguntas distintas.
- Unidades verticales independientes en proyectos o archivos sin solapamiento, donde cada subagente implementa, crea sus pruebas relacionadas y ejecuta una validación focalizada.
- Creación de pruebas para un contrato ya acordado únicamente cuando no dependa del código que otro subagente todavía está desarrollando.
- Revisión enfocada de seguridad, regresiones o cobertura.
- Análisis separado de documentación oficial o registros de una prueba.

No se debe paralelizar:

- Un cambio pequeño que el agente principal pueda implementar, probar y revisar directamente.
- Dos cambios sobre el mismo archivo o la misma responsabilidad.
- Una interfaz de Core y su consumidor mientras el contrato todavía cambia.
- La implementación de una característica en un subagente y las pruebas de esa misma característica en otro cuando el segundo deba esperar el resultado del primero.
- Migraciones, archivos de solución o configuración compartida sin un único propietario.
- Operaciones reales contra PAM, Credential Manager u Outlook.
- Cualquier trabajo cuya segunda parte dependa de una decisión o resultado de la primera.

No se deben repartir fases únicamente por su número. Antes de delegar una fase 6 a un subagente y una fase 7 a otro, el coordinador debe demostrar que ninguna depende de decisiones, contratos o archivos todavía no terminados por la otra.

Dos tareas son realmente independientes solo si:

- Cada una puede avanzar y validarse sin esperar el resultado de la otra.
- Sus contratos compartidos ya están definidos y no cambiarán durante el trabajo paralelo.
- No modifican los mismos archivos, proyectos compartidos ni una misma responsabilidad.
- Un fallo o cambio de alcance en una no invalida el trabajo de la otra.

Cuando exista riesgo de solapamiento, el coordinador debe ejecutar las tareas de forma secuencial.

## Trabajo por oleadas

El coordinador debe organizar el paralelismo en oleadas:

1. **Oleada 0 — preparación:** inspeccionar el estado, fijar contratos y decisiones compartidas, asignar un único propietario para `.slnx`, `.csproj`, documentación común o interfaces transversales y preparar las bases de trabajo.
2. **Oleada 1 — ejecución:** delegar como máximo dos unidades verticales independientes. Cada subagente es responsable de su implementación, sus pruebas relacionadas y su validación focalizada.
3. **Integración:** revisar e integrar un resultado a la vez, ejecutar la validación consolidada y resolver cualquier incompatibilidad antes de continuar.
4. **Oleada siguiente:** delegar trabajo dependiente solo después de que la base anterior esté integrada y validada.

## Aislamiento con ramas y worktrees

Los subagentes no deben asumir que reciben automáticamente una rama o un directorio aislado. Antes de permitir escrituras paralelas, el coordinador debe crear explícitamente una rama y un worktree por tarea y comunicar a cada subagente la ruta absoluta donde puede trabajar.

Convención recomendada:

```text
coordinador: rama de integración o rama principal seleccionada
subagente A: codex/<fase>-<tarea-a>
subagente B: codex/<fase>-<tarea-b>
```

Cada worktree comienza como una instantánea del commit o estado inicial seleccionado. No se sincroniza continuamente:

- Los cambios posteriores del coordinador no aparecen automáticamente en los worktrees.
- Los cambios de un subagente no aparecen automáticamente en el worktree de otro.
- Antes de integrar, el coordinador debe comprobar la base de cada rama.
- Si la rama coordinadora avanzó, debe actualizar la rama del subagente de forma controlada mediante rebase, merge o cherry-pick, según el caso, y volver a validar.

El punto de partida preferido es un commit estable y un árbol de trabajo limpio. Si existen cambios sin commit que pertenecen al usuario, deben preservarse. El agente no puede descartarlos, moverlos, sobrescribirlos ni incluirlos en un commit sin confirmar que forman parte de la fase.

Cada subagente debe recibir estas reglas explícitas:

- Trabajar únicamente en el worktree y la rama asignados.
- No cambiar de rama ni integrar otras ramas.
- No modificar archivos fuera de su lista permitida.
- No hacer rebase, merge, push ni integración final.
- No revertir cambios que no le pertenezcan.
- Detenerse y reportar si detecta solapamiento, cambios inesperados o una contradicción documental.

## Contrato mínimo de cada delegación

El prompt de un subagente debe incluir:

```text
Objetivo concreto:
Punto de partida y ruta absoluta del worktree:
Rama asignada:
Contexto y decisiones suministrados por el coordinador:
Archivos permitidos:
Archivos que no debe modificar:
Archivos y secciones exactas que debe leer:
Lecturas amplias que no debe realizar:
Restricciones de seguridad y tecnología:
Criterios de aceptación:
Implementación, pruebas relacionadas y validación focalizada a cargo del subagente:
Comandos de validación permitidos:
Formato del informe final:
```

La tarea debe tener un resultado observable. “Avanza el proyecto” o “implementa la fase” no es un alcance suficiente para un subagente.

## Reglas específicas de Pam Automation

Todos los agentes deben respetar, sin excepción:

- Solo C#, .NET 10, WPF/XAML, Playwright para .NET, Edge externo y Outlook clásico mediante COM, según corresponda a la fase.
- No introducir JavaScript, TypeScript, Node.js, Python, Java, PowerShell, Electron, WebView2, Selenium ni Chromium incluido.
- No modificar `C:\pam automatizacion`; esa aplicación es únicamente una referencia de lectura.
- No imprimir, registrar, copiar, hardcodear ni compartir entre agentes credenciales, contraseñas, semillas TOTP, códigos TOTP, cookies o tokens.
- No crear solicitudes PAM ni enviar correos durante pruebas automatizadas.
- No ejecutar integraciones reales con PAM u Outlook sin autorización explícita y separada del usuario.
- No inventar selectores del portal ni debilitar la restricción del origen autorizado.
- Mantener la dirección de dependencias descrita en `structure.md`.
- Validar únicamente el alcance cambiado y actualizar la documentación obligatoria cuando el cambio sea relevante.

Los secretos reales no deben incluirse en prompts de subagentes. Cuando una prueba de integración autorizada los necesite, el código debe leerlos directamente del almacén protegido y limitar su vida al proceso correspondiente.

## Revisión e integración

El coordinador debe integrar en este orden:

1. Confirmar el estado y el diff de la rama del subagente.
2. Verificar que solo se modificaron los archivos autorizados.
3. Revisar corrección, seguridad, manejo de errores y pruebas.
4. Actualizar la rama contra la base vigente si fuera necesario.
5. Integrar el primer trabajo en la rama coordinadora.
6. Restaurar, compilar y ejecutar sus pruebas relacionadas.
7. Actualizar e integrar el segundo trabajo.
8. Restaurar, compilar y ejecutar nuevamente las pruebas afectadas por el conjunto.
9. Revisar los documentos modificados y el diff consolidado.
10. Mantener la integración únicamente si todas las comprobaciones requeridas pasan.

La configuración canónica es `Release | x64`. Desde la raíz del repositorio, utilizar cuando corresponda:

```text
dotnet restore PamAutomation.slnx -p:Platform=x64
dotnet build PamAutomation.slnx -c Release -p:Platform=x64 --no-restore
dotnet run --project PamAutomation.Tests\PamAutomation.Tests.csproj -c Release -p:Platform=x64 --no-build
```

No utilizar `dotnet test` mientras el proyecto mantenga el runner integrado de Microsoft Testing Platform descrito en la documentación actual.

Si una integración falla, el coordinador debe conservar la evidencia, identificar qué cambio causó el fallo y reparar únicamente esa causa. No debe integrar el siguiente trabajo ni avanzar de fase hasta recuperar un estado válido.

## Control del consumo de tokens

Cada subagente consume sus propios tokens de modelo y herramientas. Para limitar el gasto:

- Utilizar uno o dos subagentes solo cuando exista paralelismo real.
- No superar dos subagentes en un mismo task ni reemplazar un subagente finalizado por un tercero para eludir el límite.
- Ejecutar directamente en el agente principal los cambios pequeños para evitar el costo de contexto, seguimiento, revisión e integración de una delegación innecesaria.
- Mantener `medium` como razonamiento normal.
- Entregar contexto relevante y rutas concretas, evitando exploraciones completas duplicadas.
- No hacer que cada subagente relea todos los Markdown ni todo el historial del chat.
- Asignar a un único agente la lectura extensa o la investigación común y compartir con el coordinador un resumen verificable.
- Evitar que varios agentes ejecuten la misma compilación completa; los subagentes pueden hacer validaciones focalizadas y el coordinador realiza la validación consolidada.
- Solicitar informes finales breves con archivos cambiados, decisiones, comandos y resultados.
- Cerrar el trabajo delegado al cumplir sus criterios y no extenderlo automáticamente a la siguiente fase.
- Preferir ejecución secuencial cuando el costo de coordinación sea mayor que el ahorro de tiempo.

## Condiciones para detenerse

El agente principal o cualquier subagente debe detener el trabajo y reportar antes de continuar cuando:

- La documentación y el código se contradigan.
- Sea necesario ampliar el alcance o modificar un archivo no autorizado.
- Aparezcan cambios ajenos o conflictos que no puedan atribuirse con seguridad.
- Haga falta una credencial real, una acción PAM, un envío de correo o una autorización nueva.
- Una prueba revele un riesgo de pérdida de datos, exposición de secretos o navegación fuera del origen permitido.
- No sea posible demostrar la compilación o la prueba exigida.

## Criterio de finalización

Una tarea multiagente termina únicamente cuando el coordinador ha revisado e integrado los cambios, la solución compila, las pruebas relacionadas pasan, la documentación requerida está actualizada y el usuario recibe un resumen consolidado con evidencia. La finalización de los subagentes por separado no equivale a la finalización de la fase.

## Referencias oficiales

- [Subagentes en Codex](https://learn.chatgpt.com/es-419/docs/agent-configuration/subagents)
- [Worktrees de Git en Codex](https://learn.chatgpt.com/es-419/docs/environments/git-worktrees)
