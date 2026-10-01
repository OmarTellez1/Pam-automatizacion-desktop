# Problemas conocidos y plan de corrección — PAM Automation

Fecha de revisión: **1 de octubre de 2026**, hora de Ecuador.  
Repositorio: `C:\pam automatizacion dotnet\PamAutomation`.

## 1. Propósito y estado de este documento

Este documento distingue incidentes observados, debilidades confirmadas al leer el código e hipótesis que requieren evidencia. Las secciones 2–10 conservan el diagnóstico y las propuestas originales como antecedentes; **el estado de implementación y la evidencia del 1 de octubre se detallan en la sección 11**. La mejora de Favorites se conserva y el margen temporal permanece diferido.

Aplicar [AGENTS.md](AGENTS.md). Este traspaso no sustituye las obligaciones de lectura inicial que correspondan al nuevo thread. Consultar [Fases.md](Fases.md), [LOGIC.md](LOGIC.md), [PAM_NAVIGATION.md](PAM_NAVIGATION.md), [structure.md](structure.md) e [History.md](History.md) para los contratos vigentes. Comprobar el código y el estado de Git antes de trabajar: los números de línea y los archivos pueden cambiar.

Alcance: problemas discutidos durante la estabilización de solicitudes, calendario, hora, recuperación de resultados y diagnóstico. **No es una auditoría exhaustiva de toda la aplicación.**

### Prioridad acordada y orden sugerido

| Tema | Estado y evidencia | Prioridad / siguiente paso |
| --- | --- | --- |
| Calendario: `MonthUnreadable` | Instrumentación implementada; incidente original no reproducido y causa exacta desconocida | Analizar una próxima ocurrencia con las nuevas observaciones; no aumentar timeouts |
| Punto 3: estabilidad de hora/formulario | Comprobación estable y corrección limitada implementadas y verificadas | Conservar regresiones; no atribuirle el incidente original |
| Punto 1: respuesta perdida y lote parcial | Conservación y reconciliación implementadas; aceptación incierta cubierta con servicios simulados | Verificación operativa pendiente ante una interrupción real; no repetir Submit |
| Diagnóstico de intento, contadores y componentes | Correlación, separación último intento/último éxito y metadata implementadas | Exportar diagnóstico explícitamente si vuelve a fallar |
| Punto 4: navegación repetida por Favorites | Corregido y validado; conservar la mejora | Seguimiento, no rehacer |
| Punto 2: margen de hora consumido durante el lote | Riesgo identificado en código; el usuario no lo priorizó | Diferido |

Secuencia recomendada: diagnóstico del calendario y correlación del intento → estabilidad del formulario → recuperación de lotes parciales/inciertos. Tratar cada cambio como una entrega pequeña. No implementar todos los puntos juntos ni ampliar el alcance sin necesidad.

## 2. Evidencia disponible y conclusiones que no deben extrapolarse

Diagnósticos aportados por el usuario, disponibles localmente si no se han movido:

- `C:\Users\tellezoe\Downloads\pam-automation-diagnostic-20260930-195922`.
- `C:\Users\tellezoe\Downloads\pam-automation-diagnostic-20260930-213552`.
- `C:\Users\tellezoe\Downloads\pam-automation-diagnostic-20261001-050359`.

El último contiene un fallo a las **00:03:47 del 01/10/2026 en Ecuador**; el log lo registra como `2026-10-01T05:03:47...+00:00`. Sus datos relevantes son:

- `CalendarFailure=MonthUnreadable`.
- `CalendarTargetDate=2026-11-02`.
- `CalendarNavigationCount=0`.
- `CalendarObservedMonth=Unavailable`.
- La pila pasa por `PamCalendarDateSelector.WaitForMonthAsync`, `SelectAsync` y el llenado del formulario, antes del Submit del formulario afectado.
- El análisis previo identificó tres ejecuciones completas anteriores, con 9, 6 y 6 IDs, y después un intento fallido de seis solicitudes. No sumar sus resultados como si fueran una sola ejecución.
- El diagnóstico de Pending conservaba etapa `Preflight` y `submittedCount=0`. Ese cero **no demuestra que ninguna solicitud del lote alcanzara PAM**: el contador se actualiza después de que regresa el servicio de envío.
- El resumen de ejecución podía corresponder al último resultado exitoso, no al último intento fallido.

Lo que sí demuestra el fallo: no se consiguió una lectura aceptable y estable del mes inicial del calendario del formulario afectado. Lo que no demuestra: cuál de las comprobaciones internas falló, qué número de formulario era, cuántas solicitudes anteriores estaban confirmadas, o que Favorites, la hora, Outlook o un TOTP fueran la causa.

El usuario aclaró que el problema anterior del TOTP ya estaba corregido antes de automatizar. También confirmó resueltos los incidentes anteriores de Pending vacío y Outlook. El problema de la laptop del compañero quedó aplazado. No reabrirlos como causas actuales sin evidencia nueva.

La navegación Favorites se corrigió **después** de ese diagnóstico. Las pruebas exitosas posteriores no reproducen por sí solas la condición transitoria de `MonthUnreadable`.

## 3. Calendario: diagnóstico demasiado general

### Comportamiento actual y por qué no alcanza para encontrar la causa

Archivo principal: [PamCalendarDateSelector.cs](PamAutomation.Browser/PamCalendarDateSelector.cs).

- `SelectAsync` observa un calendario visible único, usa el selector nativo de mes dentro del mismo año y flechas para cambios de año.
- `ReadMonthAsync` lee `select.flatpickr-monthDropdown-months` e `input.cur-year`, y exige coherencia con un día propio de la grilla. Sin ambos controles, conserva una ruta de compatibilidad que exige un mes completo.
- `WaitForMonthAsync` exige dos observaciones iguales. Varias condiciones distintas de `ReadMonthAsync` devuelven simplemente `null`.
- Los errores transitorios de Playwright y timeout dentro de esa espera se reobservan, pero su detalle no queda en el resultado final.
- Al agotarse la lectura inicial, muchas situaciones diferentes terminan en `MonthUnreadable`.
- Ya existen límites centrales de ocho segundos por transición y 45 segundos para toda la selección. **No empezar por aumentarlos.**

Candidatos que el código permite, pero que no están demostrados para el incidente: falta temporal de mes o año, múltiples controles visibles, valores vacíos o no interpretables, grilla todavía sin días, etiqueta de día no interpretable, cabecera y grilla de meses diferentes, repintados del DOM o calendario que dejó de estar visible.

### Cambios recomendados

Introducir una observación interna pequeña que conserve mes válido, cuando exista, y una razón tipada cuando no exista. Evitar reemplazar todo el selector o introducir una infraestructura genérica de máquinas de estados.

Ejemplos de razones **propuestas, aún no existentes**:

| Razón | Qué comprobar / registrar |
| --- | --- |
| CalendarNotVisible / MultipleVisibleCalendars | Cantidad de calendarios abiertos y visibles |
| HeaderIncomplete / HeaderAmbiguous | Cantidades de selectores de mes y año |
| MonthValueInvalid / YearValueInvalid | Estado vacío, formato inválido o fuera de rango; no texto arbitrario |
| GridEmpty | Cantidad de días propios visibles |
| DayLabelInvalid | Fallo de interpretación de etiqueta; no copiar toda la etiqueta sin sanitización |
| HeaderGridMismatch | Mes/año de cabecera y del día observado, normalizados |
| FallbackGridIncomplete | Ruta sin cabecera, conteo de días y extremos normalizados |
| CalendarReadTimeout / CalendarReadFailed | Operación de lectura y categoría técnica segura |
| TargetDayMissing / TargetDayDisabled | Distinguir ausencia de prohibición explícita |
| SelectionReverted | La fecha llegó a coincidir y luego dejó de hacerlo |

Conservar `PamCalendarFailure` como categoría principal compatible y añadir la subcausa, etapa y observaciones. No convertir estados desconocidos en fechas válidas para que el flujo continúe.

Guardar un pequeño historial circular en memoria por formulario, por ejemplo las últimas 10–20 observaciones. Registrar cambios de estado y contadores; no producir un evento completo por cada sondeo de 200 ms. Anexar el resumen al fallo y a la exportación explícita. Incluir:

- Identificador de intento, ordinal del formulario y cantidad esperada.
- Etapa: abrir, leer mes inicial, seleccionar mes, esperar transición, seleccionar día, confirmar fecha.
- Tiempo transcurrido y presupuesto restante mediante reloj monotónico.
- Cantidad de observaciones, coincidencias consecutivas y fallos de lectura.
- Fecha objetivo, mes observado normalizado, ruta nativa/compatibilidad y cantidad de acciones de navegación.
- Última condición fallida y una secuencia corta que permita distinguir carga lenta de oscilación.

### Dónde implementarlo

- Browser: `PamCalendarDateSelector.ReadMonthAsync`, `WaitForMonthAsync`, `OpenAsync`, `SelectAsync`.
- Core: [PamCalendarSelectionException.cs](PamAutomation.Core/Requests/PamCalendarSelectionException.cs); ampliar metadata tipada y compatible. Un record pequeño adicional solo si simplifica el contrato.
- Infrastructure: [RuntimeDiagnosticLoggerFactory.cs](PamAutomation.Infrastructure/Diagnostics/RuntimeDiagnosticLoggerFactory.cs), especialmente `Capture`; incorporar explícitamente los campos seguros.
- Infrastructure: [DiagnosticExportService.cs](PamAutomation.Infrastructure/Diagnostics/DiagnosticExportService.cs), si se añade un resumen/historial exportable.
- Tests: `PamCalendarDateSelectorTests`, `RuntimeDiagnosticLoggerFactoryTests` y `DiagnosticExportServiceTests`.

### Opciones de corrección, condicionadas al diagnóstico

- Cabecera adelantada a la grilla: esperar coherencia, sin pulsar otra vez el mes.
- Controles reemplazados: resolverlos nuevamente dentro del único calendario visible; no conservar elementos obsoletos.
- Calendario cerrado durante el repintado: valorar una reapertura limitada solo si no hay ambigüedad y permanece dentro del presupuesto.
- Cambios de formato reales: ajustar el parser con ejemplos observados y regresiones; no aceptar cualquier texto como fecha.
- Día explícitamente deshabilitado o múltiples calendarios visibles: detenerse con explicación, sin forzar el DOM.

Aceptación: cada escenario local debe indicar qué comprobación falló; un calendario correcto debe seguir funcionando con meses y años distintos, días adyacentes, bisiestos y grillas parciales. Ningún fallo debe permitir Submit. Una prueba real exitosa debe describirse como tal, sin afirmar que reprodujo el incidente original.

## 4. Punto 3: espera fija y estabilidad insuficiente del formulario

### Debilidad confirmada en el código

Archivo: [PlaywrightManagedPage.RequestTest.cs](PamAutomation.Browser/PlaywrightManagedPage.RequestTest.cs). Aunque el nombre diga RequestTest, sus métodos de llenado y verificación también se utilizan desde producción.

`FillAndVerifyRequestFormAsync` escribe la hora, espera 250 ms, la lee y permite una segunda escritura si no coincide. Después llena duración y motivo. Esta espera no demuestra que PAM haya terminado de inicializar o normalizar el campo.

`VerifyRequestFormAsync` observa durante un máximo de 15 segundos, pero **regresa ante la primera lectura completa sin diferencias**. No exige varias lecturas correctas consecutivas. Si el valor queda incorrecto, observa hasta fallar; no realiza una recuperación del conjunto del formulario.

`ReadRequestFormMismatchesAsync` lee los campos secuencialmente. Una lectura completa es una observación lógica, no una captura DOM atómica: un repintado puede cambiar un campo mientras se leen los siguientes.

El calendario ya exige dos confirmaciones tras seleccionar un día, pero su salida rápida cuando Start Date inicialmente coincide acepta una sola lectura. Revisar ese camino al definir la estabilidad integral; no afirmar que el calendario carece por completo de comprobaciones.

**No está demostrado que esta debilidad causara el MonthUnreadable del diagnóstico.** Es un problema independiente de robustez y una oportunidad de mejora.

### Solución recomendada

1. Esperar que el campo objetivo sea único, visible y editable. Utilizar señales de carga del portal solo si están realmente observadas y documentadas.
2. Escribir primero fecha y luego hora. Conservar duración y motivo según el formulario autorizado.
3. Reemplazar los 250 ms fijos por sondeo inmediato y espaciado, con límite total. Exigir al menos dos observaciones completas consecutivas compatibles con todos los valores esperados; definir y probar la ventana mínima de estabilidad.
4. Reiniciar la racha ante diferencias, lecturas incompletas, reemplazos o errores transitorios. Dos valores iguales pero incorrectos no son éxito.
5. Cuando haya evidencia de que PAM restableció un campo durante la carga, permitir **una corrección limitada por formulario**. Corregir solo lo necesario, respetando la dependencia fecha → hora, y volver a verificar el conjunto completo.
6. Conservar las etapas `AfterFill` y `BeforeSubmit`. La comprobación final debe seguir siendo inmediata al límite irreversible.
7. Si continúa inestable o ambiguo, detenerse. No envolver Submit en el reintento ni cambiar automáticamente la fecha/hora solicitada.

El número de muestras y la ventana mínima deben tener justificación y pruebas, no ser otro delay fijo con nombre distinto. Ninguna ventana finita garantiza que el portal jamás volverá a cambiar; combinar estabilidad con señales de disponibilidad y la validación final reduce ese riesgo.

### Diagnóstico y clases

- Browser: `FillAndVerifyRequestFormAsync`, `VerifyRequestFormAsync`, `ReadRequestFormMismatchesAsync`; extraer un helper pequeño de estabilidad solo si mejora su comprensión y pruebas.
- Browser: [PlaywrightManagedPage.RequestSubmission.cs](PamAutomation.Browser/PlaywrightManagedPage.RequestSubmission.cs), conservar la verificación `BeforeSubmit`.
- Core: [PamRequestFormVerificationException.cs](PamAutomation.Core/Requests/PamRequestFormVerificationException.cs), que **ya** contiene etapa, campos distintos, conteos, valores temporales esperados/observados y longitud del motivo.
- Core: [PamOperationTimingPolicy.cs](PamAutomation.Core/Resilience/PamOperationTimingPolicy.cs), centralizar presupuestos y recuperación sin multiplicar tiempos por cada campo.
- Infrastructure: ampliar `CaptureFormVerification` para racha estable, cambios observados, campo corregido, cantidad de correcciones y fallo del control. No volver a inventar el diagnóstico existente.

Pruebas de aceptación: valor correcto inmediato sin demora obligatoria; normalización tardía; correcto→incorrecto después de 250 ms; fecha que altera la hora; campo reemplazado; dos valores erróneos iguales; lectura parcial; motivo distinto con igual longitud; una corrección exitosa; corrección agotada; cancelación. La igualdad del motivo sigue comparándose completa en memoria aunque solo se exporte su longitud/resultado booleano. Cero Submit con valores incorrectos.

## 5. Punto 1: respuesta perdida y resultados parciales del lote

### Qué ocurre y dónde se pierde información

- `PlaywrightManagedPage.RequestSubmission.SubmitRequestFormAsync` espera la respuesta del POST tras el clic. Un timeout no prueba que el servidor rechazó o no recibió la solicitud.
- `PamRequestSubmissionService.ExecuteAsync` agrega resultados únicamente cuando obtiene un desenlace conocido. Si falla otra fila, lanza excepción; conserva un conteo procesado en algunos caminos, pero no devuelve la lista completa de desenlaces parciales al coordinador.
- `PamAutomationExecutionService.ExecuteRealAsync` llama a la reconciliación **después** de recibir un `PamRequestRunResult` completo. Si el envío lanza excepción, no alcanza esa reconciliación.
- `results.Count` representa resultados conocidos procesados y puede incluir conflictos; no equivale necesariamente a solicitudes creadas.

Esto también afecta a un fallo **anterior al Submit del formulario actual**, como el calendario: podrían existir solicitudes confirmadas de formularios anteriores cuya recuperación de IDs queda sin ejecutar. Separar el fallo actual de la situación del lote completo.

### Diseño propuesto

Mantener en memoria un resultado por elemento con estados explícitos. Nombres orientativos, no APIs actuales:

| Estado | Evidencia mínima |
| --- | --- |
| No intentada | Todavía no se alcanzó el límite de envío |
| Fallo anterior al envío | Falló preparación/validación sin emitir Submit para esa fila |
| Confirmada por PAM | Respuesta de aceptación conocida |
| Rechazada / conflicto conocido | Respuesta o condición inequívoca y clasificada |
| Envío incierto | Se inició el intento y no se puede demostrar aceptación ni rechazo |

Distinguir clic intentado, POST observado y respuesta observada cuando sea posible; observar el POST no demuestra aceptación. Tampoco un error genérico HTTP o del navegador debe convertirse automáticamente en rechazo seguro.

Detener los siguientes envíos ante el fallo. Conservar los resultados conocidos y la fila incierta mediante un resultado parcial explícito o una excepción tipada que transporte ese resultado; elegir una opción pequeña coherente con los contratos actuales, no ambas.

El coordinador podrá consultar Pending para recuperar lo verificable:

- Conservar la línea base anterior a cualquier envío y excluir sus IDs.
- Reutilizar las reglas actuales: System + Account + inicio + expiración + motivo; Application no se usa para reconciliar Pending.
- Mantener conteos y agrupación de claves repetidas. No adjudicar por orden, prefijos ni IDs consecutivos.
- Recuperar los IDs compatibles sin pulsar nuevamente Submit.
- Si faltan filas después del presupuesto, dejar el estado sin resolver. Ausencia temporal en Pending no prueba rechazo.
- Si aparecen más coincidencias indistinguibles de las esperadas, conservar la ambigüedad y bloquear una conclusión falsa.
- Tratar explícitamente cancelación y cierre: no continuar envíos después de cancelar. Definir cómo ofrecer o realizar una lectura de recuperación sin ignorar la decisión del usuario.
- No enviar correo automáticamente como si el lote estuviera completo. No incorporar resultados parciales al acumulador actual, que exige ejecuciones completas, sin una decisión funcional separada.

### Clases afectadas y aceptación

- Browser: `PlaywrightManagedPage.RequestSubmission.cs`, `PamSubmissionResponseClassifier`.
- Core: `PamRequestPortalOutcome`, `PamRequestSubmissionStatus`, `PamRequestAccountResult`, `PamRequestRunResult`, `PamRequestSubmissionException`, `PamRequestSubmissionService`.
- Coordinación: [PamAutomationExecutionService.cs](PamAutomation.Core/Execution/PamAutomationExecutionService.cs), `PamExecutionResult` y `PamExecutionPreparation`.
- Reutilizar [PamPendingService.cs](PamAutomation.Core/Requests/PamPendingService.cs); no relajar su comparación para ocultar faltantes.
- App: `MainWindowViewModel.cs` y `MainWindowViewModel.Accumulation.cs`; distinguir resultado parcial, IDs recuperados y solicitudes no intentadas. Conservar los IDs acumulados anteriores.
- Proteger el contrato de [RequestIdAccumulator.cs](PamAutomation.Core/Execution/RequestIdAccumulator.cs).

Pruebas mínimas: aceptación cuya respuesta se pierde y luego aparece en Pending; respuesta incierta que nunca se puede resolver; dos envíos confirmados y fallo de calendario en el tercero; rechazo conocido; conflictos previos; IDs antiguos; claves repetidas; exceso ambiguo; cancelación tras aceptación. Afirmar exactamente un intento de Submit por fila y ningún correo automático del lote incompleto. Extender `PamRequestSubmissionServiceTests`, `PamAutomationExecutionServicePendingTests`, pruebas de correo y acumulación solo en lo directamente afectado.

## 6. Punto 4: Favorites — mejora ya implementada

Archivo principal: [PlaywrightManagedPage.RequestNavigation.cs](PamAutomation.Browser/PlaywrightManagedPage.RequestNavigation.cs).

Antes se cargaba Accounts, se seleccionaba Favorites y se empezaba desde la primera página para cada solicitud. Eso añadía carga y oportunidades de fallo, pero no quedó demostrado que fuera la causa directa del MonthUnreadable.

Ahora `PrepareRequestFormAsync`:

- Comprueba URL de Accounts, categoría Favorites seleccionada, Browse by Category, filas visibles y ausencia del formulario.
- Busca primero en la página actual y recorre las restantes si es necesario.
- Resuelve Access por System + Application + Account exactos; no guarda índices de filas.
- Permite una recuperación desde Accounts ante vista obsoleta o fallo de navegación/apertura.
- Comparte un presupuesto de 60 segundos, controles de 10 y hasta dos segundos para observar el retorno del grid.
- Deja fuera del reintento el llenado, calendario y Submit.

Validación ya realizada: Release x64 y **49 pruebas aprobadas**; 14 formularios reales validados y descartados en Edge visible. Cero recargas innecesarias entre formularios y una sola al forzar una salida a Requests a mitad del recorrido. Cero intentos de creación y cero correos. No se cambió versión ni se publicó.

Tests: `PamFavoritesRequestNavigationTests` y `PamRealFavoritesNavigationTests`. Conservar sus regresiones. La prueba real se limitó a formularios descartados y no equivale a una prueba nueva de envío real después del ajuste.

Si fuera necesario mejorar su diagnóstico, registrar vía de preparación (reutilización/recarga), motivo de recuperación, página y cantidad de intentos. No registrar identidades completas. La navegación de Pending es un recorrido separado y esta mejora no la convierte automáticamente en reutilizable.

## 7. Punto 2: margen de inicio durante un lote largo — diferido

[RequestSettingsValidator.cs](PamAutomation.Core/Validation/RequestSettingsValidator.cs) exige actualmente seis minutos de anticipación. `PamRequestSubmissionService.ExecuteAsync` valida con `ecuadorNow` recibido, antes de autenticar y recorrer las filas; ese valor no se renueva antes de cada Submit.

Un lote lento puede consumir el margen. Es un riesgo de código, **no la explicación del caso con fecha objetivo un mes posterior**. El usuario no lo considera prioritario; mantenerlo diferido.

Cuando se aborde: usar un reloj inyectable, comprobar el inicio con la hora actual antes de cada envío y detener con una causa específica cuando deje de cumplir. Una validación solo antes de abrir el formulario todavía puede quedar obsoleta durante su llenado. Diseñar una comprobación en el límite previo a Submit manteniendo la regla en Core, mediante un contrato pequeño con Browser si hace falta. No trasladar las reglas del validador a selectores ni reprogramar la fecha del usuario.

Diagnóstico: hora normalizada de comprobación, inicio, margen restante y ordinal. Pruebas con reloj controlado y avance durante el lote; no esperas reales de minutos. Si ya se enviaron filas, aplicar el tratamiento parcial de la sección 5.

## 8. Diagnóstico transversal: intento, contadores y binarios

### 8.1 Separar último éxito del último intento

`DiagnosticExportService` exporta `context.LastExecution`. El coordinador de Pending tiene su propio identificador y estados. Un resumen anterior junto con logs nuevos puede inducir conclusiones equivocadas.

Propuesta: conservar explícitamente último intento y último éxito, con identificador, inicio/fin, estado, etapa fallida, cantidad esperada y ordinal actual. El mismo identificador debe acompañar preparación, formularios, Pending y resultado. No borrar el éxito previo para simular que describe el fallo nuevo.

Puntos: `DiagnosticExportContext`, `DiagnosticExecutionSummary`, `PamExecutionPreparation`, `PamAutomationExecutionService`, ViewModels de ejecución y `DiagnosticExportService`.

### 8.2 No usar cero como sinónimo de desconocido

`RecordSubmittedCount` se llama después del retorno completo del servicio de envío. Registrar avances seguros por fila en el flujo principal, no depender exclusivamente de `IProgress` de UI para la contabilidad.

Separar: planificadas, procesadas con desenlace conocido, aceptadas, conflictos, rechazadas, inciertas, no intentadas y IDs reconciliados. Mantener compatibilidad del exportador y versionar el esquema si cambia su semántica. Un resultado parcial nunca debe aparecer como lote completo.

Puntos: `PamRequestSubmissionProgress`, `PamRequestSubmissionService`, `PendingReconciliationDiagnostic`, `IPendingReconciliationDiagnosticSink`, `InMemoryPendingReconciliationDiagnosticStore` y el coordinador.

### 8.3 Hacer que los campos nuevos lleguen al diagnóstico

`RuntimeDiagnosticLoggerFactory` mantiene hasta 500 eventos y aplica una lista permitida de propiedades. Añadir un parámetro a LogInformation no garantiza su exportación: un identificador o una nueva etapa pueden ser omitidos por el filtro.

Agregar nombres, tipos y formatos expresamente permitidos; comprobar el archivo exportado, no solo la excepción en memoria. No habilitar todos los mensajes de excepciones ni serializar objetos completos como atajo. Usar eventos con plantilla estable y metadatos tipados, manteniendo el historial acotado.

Las excepciones de formulario ya se exportan con campos sanitizados. Extender ese mecanismo. Registrar éxito/transiciones recuperadas como información; advertencia para recuperación relevante; error para la operación que realmente falla. No eliminar ERROR/WARNING solo para que el log parezca limpio.

### 8.4 Identificar el componente que realmente cambió

`DiagnosticExportService` obtiene versión y MVID de `Assembly.GetEntryAssembly()`. Esa información identifica App; **un MVID de App repetido no demuestra que Browser no se haya actualizado**.

Propuesta: añadir versión/MVID de App, Browser, Core e Infrastructure, y versión efectiva de Edge/Playwright cuando pueda obtenerse de forma segura. Resolver la dependencia entre capas mediante metadata suministrada al contexto de exportación o ensamblados conocidos; no añadir una dependencia circular Infrastructure → Browser. Evitar rutas locales innecesarias.

Pruebas: exportación con metadata por componente, intento fallido posterior a éxito, progreso parcial y retención limitada. Revisar `DiagnosticExportServiceTests`, `RuntimeDiagnosticLoggerFactoryTests` e `InMemoryPendingReconciliationDiagnosticStoreTests`.

## 9. Restricciones y plan de validación para el siguiente thread

- Mantener .NET 10, WPF, Microsoft.Playwright para .NET y Edge externo. No introducir frameworks nuevos, automatización por JavaScript ni perfiles persistentes en el flujo de solicitudes.
- Leer y aplicar las skills pertinentes conforme a AGENTS.md; para pruebas del navegador, seguir playwright-cli cuando corresponda, subordinado a las restricciones del proyecto.
- No registrar contraseñas, semillas/TOTP, cookies, tokens, cuerpos HTTP completos, motivo ni DOM completo. Las etiquetas/valores que no se puedan normalizar se representan mediante categorías seguras.
- Historial y resultados solo en memoria; exportación de diagnóstico exclusivamente por acción explícita. Las pruebas de desarrollo pueden usar artefactos ignorados y sin secretos.
- Este documento no autoriza nuevas solicitudes PAM ni correos. El siguiente thread debe verificar la autorización vigente para cada prueba real; empezar por simulaciones locales y formularios reales descartables si están autorizados.
- Probar primero la instrumentación: que permita distinguir los casos antes de cambiar la recuperación. Después probar estabilidad y, en otra entrega, resultados parciales.
- Reutilizar los fixtures C# y Edge visible existentes. No confundir respuestas locales simuladas con evidencia capturada del incidente real.
- No reintentar Submit, credenciales, TOTP o envío de correo ciegamente.
- Conservar las reglas de identidad, preflight, Pending vacío, claves repetidas, correo por colección, firma común y acumulación.
- No cambiar versión ni publicar por defecto.

Compilar con la configuración canónica:

```text
dotnet build PamAutomation.slnx -c Release -p:Platform=x64 --no-restore
```

Restaurar antes si las dependencias no están disponibles. Ejecutar únicamente los tests relacionados con el cambio mediante el runner integrado:

```text
dotnet run --project PamAutomation.Tests/PamAutomation.Tests.csproj -c Release -p:Platform=x64 --no-build -- --filter FullyQualifiedName~PamCalendarDateSelectorTests
```

Los tests de navegador requieren sus opt-in existentes; comprobarlos en el archivo antes de ejecutarlos. Ejemplos: `PAM_AUTOMATION_ALLOW_LOCAL_BROWSER_CALENDAR=LOCAL_ONLY`, `PAM_AUTOMATION_ALLOW_LOCAL_BROWSER_FAVORITES=LOCAL_ONLY` y, con autorización real segura, `PAM_AUTOMATION_ALLOW_REAL_REQUEST_FORMS=VALIDATE_AND_DISCARD`. No activar el opt-in de creación de solicitudes para probar navegación o diagnóstico.

Añadir los filtros de logger/exportación para cambios de metadata; los de servicios, coordinación, correo o acumulación solo cuando su comportamiento se vea afectado. No ejecutar toda la suite por rutina ni agregar pruebas que repitan trivialmente la implementación.

## 10. Criterio de entrega y mantenimiento de este traspaso

Cada entrega debe indicar: problema abordado, evidencia, archivos modificados, comportamiento resultante, pruebas ejecutadas/aprobadas/fallidas/omitidas y límites de la reproducción. Actualizar Fases.md, LOGIC.md, History.md, README.md y structure.md cuando corresponda.

Para calendario/formulario, no declarar resuelto el incidente solo porque pasó un formulario. Cubrir múltiples formularios consecutivos, transición de mes/año y condiciones transitorias controladas. Si el incidente real no se reproduce, decirlo expresamente y entregar el diagnóstico que permita estudiarlo en la siguiente ejecución del usuario.

Para lotes parciales, exigir evidencia de conservación de resultados, reconciliación segura y ausencia de reenvíos. Mostrar claramente qué quedó confirmado y qué sigue incierto.

Actualizar este documento al cerrar cada problema, diferenciando instrumentación implementada, corrección implementada y aceptación real. **Al crear este archivo solo se realizó documentación y lectura de código/logs; no se implementó ninguna de las propuestas pendientes ni se ejecutaron pruebas reales nuevas.**

## 11. Entrega implementada y evidencia — 1 de octubre de 2026

### 11.1 Calendario, correlación y formulario

- `PamCalendarDiagnostic` distingue calendario ausente/duplicado, cabecera incompleta/ambigua, mes/año inválidos, grilla vacía, etiquetas inválidas, incoherencia cabecera/grilla, compatibilidad incompleta, errores de lectura, mes aún inesperado y día ausente/deshabilitado. Conserva hasta 20 cambios de condición, conteos de observaciones/fallos, racha, etapa y tiempos monotónicos. Los conteos de controles no sondeados se representan como null/Unknown, sin confundirlos con cero observado. Los presupuestos de ocho segundos por transición y 45 totales no aumentaron.
- `PamExecutionAttempt` genera el ID antes de preparación y lo conserva en formularios, Pending y logs. Registra ordinal, fecha objetivo, cantidades y etapa original del fallo aunque después haya recuperación. La exportación separa `lastAttempt` de `lastExecution`; incluye versiones/MVID de App, Browser, Core e Infrastructure cargados y versiones efectivas de Edge/Playwright disponibles. El manifiesto usa schema 3 cuando incluye intento.
- La verificación compartida por modo real y descarte exige controles disponibles/editables, al menos dos lecturas completas y una ventana continua de 600 ms dentro del presupuesto existente de 15 segundos. Diferencias, fallos o reemplazo de controles reinician la racha. Una sola corrección por formulario cubre AfterFill y BeforeSubmit; al corregir fecha se reescribe la hora dependiente. `Application Session` desactivado bloquea. El motivo se compara completo y solo se exporta longitud/discrepancia.
- Los logs de etapa/conteos y excepciones tipadas incluyen las subcausas, historial, campos afectados, racha, correcciones, controles reemplazados y evidencia de clic/POST/respuesta disponible. Historial acotado en memoria; no se registran identidades, secretos, DOM, cuerpos HTTP ni texto del motivo. La exportación sigue siendo explícita.

### 11.2 Conservación y reconciliación

`PamRequestSubmissionException.PartialRun` conserva todas las filas autorizadas: aceptadas por respuesta, conflictos, rechazo inequívoco, fallo antes del envío, envío incierto y no intentadas. Un HTTP desconocido o respuesta perdida queda incierto. Se detiene el resto y nunca se repite Submit. Los conteos se actualizan directamente en el flujo, sin depender de IProgress, incluso si falla la limpieza posterior.

El coordinador recupera mediante lectura de Pending, con línea base original y comparación exacta existente. Conserva IDs de grupos nuevos, completos e inequívocos, incluso si una lectura posterior falla. No asigna IDs por orden/Application a un grupo indistinguible incompleto; el exceso ambiguo bloquea recuperación. `UncertainCount` conserva la pérdida histórica de respuesta y `UnresolvedCount` distingue lo aún no verificado. Cancelar detiene los siguientes envíos y omite nuevas lecturas. La UI muestra el resultado incompleto y permite copiar IDs recuperados, conserva el último éxito/acumulado previo y no envía correo ni incorpora automáticamente este lote.

### 11.3 Matriz real autorizada

Se usaron credenciales protegidas y contextos temporales de Edge, con creación POST bloqueada y sin Outlook. Cada caso recorrió los 14 Favorites actuales, verificó AfterFill/BeforeSubmit y descartó el formulario.

| Fecha objetivo | Edge | Formularios confirmados/descartados | Resultado |
| --- | --- | --- | --- |
| 02/11/2026 | Visible | 14/14 | Aprobado |
| 02/11/2026 | Headless | 14/14 | Aprobado |
| 02/12/2026 | Visible | 14/14 | Aprobado |
| 04/01/2027 | Visible | 14/14 | Aprobado |
| 02/02/2027 | Visible | 14/14 | Aprobado |

Cinco casos aprobados, cero fallidos/omitidos, 70 formularios y cero intentos de creación. Reporte de desarrollo: `results/calendar-multi-month/calendar-real.trx`. Enero prueba el cambio de año; el fixture elige un día laborable, sin cambiar fechas del producto. Headless se probó adicionalmente en noviembre porque corresponde a la variante de producción; los demás escenarios fueron visibles.

La primera corrida exploratoria, con fecha 01/11/2026, tuvo un caso visible fallido en el formulario 9 después de ocho descartes: `DateUnavailable`, mes observado noviembre y una navegación. Su variante headless pasó los 14. **No es una reproducción de MonthUnreadable y no permite atribuir causa a un fin de semana, Favorites o la hora.** La matriz posterior aprobada no borra esa observación. Se amplió la salida segura del fixture para que una recurrencia conserve la subcausa y el ordinal.

### 11.4 Validación local y límites

La compilación Release x64 aprobó; permanece la advertencia heredada MSTEST0032 de TestExplorerSmokeTests, sin modificarla. Las pruebas relacionadas cubren condiciones del calendario, límites de mes/año/año bisiesto, redibujado, cancelación, reseteos tardíos, reemplazo de controles, motivo diferente de igual longitud, lectura incompleta, corrección agotada, disponibilidad readonly y dependencia fecha/hora. Servicios simulados verifican pérdida de respuesta con/sin aparición en Pending, aceptaciones previas a fallo de calendario, rechazo/conflicto, claves repetidas, exceso, línea base, cancelación, recuperación parcial seguida de fallo de lectura y acumulado intacto. Las pruebas de logger/ZIP verifican metadata sanitizada y separación último intento/éxito.

El runner focalizado aprobó 112 casos, cero fallidos/omitidos: `results/calendar-multi-month/final-112.trx`. Incluye la cancelación después de la última aceptación y la recuperación parcial seguida de fallo de lectura. Los escenarios de navegador local fueron visibles, con toda la red interceptada en C#; no contactaron PAM. Tras el ajuste final del mensaje se recompiló y aprobaron 80 casos de Core/diagnóstico/UI (`results/calendar-multi-month/final-core-and-ui.trx`), incluidos 79 ya cubiertos y la comprobación adicional del resultado incompleto con ID copiable. En total son 113 casos distintos relacionados aprobados, sin fallos/omisiones en la validación final.

La conservación de aceptación incierta se verifica mediante simulaciones; no se indujeron pérdidas de respuesta creando solicitudes reales. La causa interna de `MonthUnreadable` del diagnóstico original **sigue pendiente de reproducción**. Una próxima ocurrencia debe contrastarse con la nueva subcausa/historial, ordinal, conteos y binarios; no se declara resuelta. Favorites se conserva. Margen temporal diferido. Sin cambios de versión, publicación, creación PAM ni correos reales en esta entrega.
