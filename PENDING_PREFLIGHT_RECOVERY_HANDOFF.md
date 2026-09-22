# Traspaso técnico: recuperación integral de Pending en preflight y reconciliación

## Objetivo para Codex Astra

Corregir de forma segura y mantenible los timeouts transitorios que ocurren al leer el grid `Pending` del portal PAM, incluyendo tanto el preflight anterior a los envíos como la reconciliación posterior. La solución debe conservar la garantía más importante del producto: una recuperación de lectura nunca puede volver a crear solicitudes ni mezclar Request ID antiguos con nuevos.

Este documento describe el estado actual del repositorio, la evidencia real, lo que ya se implementó, la brecha que permanece y los criterios concretos para considerar resuelto el problema.

## Reglas obligatorias del repositorio

Antes de actuar, leer y respetar `AGENTS.md`. Este archivo de traspaso no sustituye sus reglas.

- La raíz oficial es `C:\pam automatizacion dotnet\PamAutomation`.
- La solución es `PamAutomation.slnx`.
- Tecnología: C#, .NET 10 LTS, WPF y Microsoft.Playwright para .NET.
- No modificar `C:\pam automatizacion`.
- No crear solicitudes PAM ni enviar correos durante pruebas automatizadas.
- No registrar credenciales, contraseña, semilla o código TOTP, cookies, tokens ni contenido del correo.
- Los Request ID y campos de Pending autorizados solo pueden materializarse mediante una exportación diagnóstica solicitada explícitamente.
- Ejecutar compilación y pruebas relacionadas en `Release | x64`.
- Actualizar `README.md`, `LOGIC.md`, `History.md`, `Fases.md` y `structure.md` si se modifica el comportamiento.
- Preservar cualquier cambio existente en el working tree; no usar operaciones destructivas de Git.

## Síntoma general

La aplicación abre `Requests`, selecciona `Status = Pending`, intenta elegir el mayor `items per page` disponible —actualmente 200— y lee todas las filas. Cada snapshot debe ser completo, tener Request ID únicos y estabilizarse antes de aceptarse.

En ocasiones el portal deja el grid en un estado intermedio o una operación DOM de Playwright no responde dentro del tiempo permitido. La excepción observada es:

```text
System.TimeoutException
Microsoft.Playwright.Transport.Connection.InnerSendMessageToServerAsync
PamAutomation.Browser.PlaywrightManagedPage.ScanAllPendingPagesAsync
PamAutomation.Browser.PlaywrightManagedPage.ReadAllPendingAsync
```

`PamPendingService.ReadAllAsync` traduce esta excepción a:

```text
PamPendingException(PamPendingErrorCode.PortalUnavailable)
```

El fallo puede ocurrir en dos etapas distintas:

1. **Preflight:** antes de pulsar cualquier `Submit Request`.
2. **Reconciliación:** después de que PAM ya confirmó los envíos y la aplicación intenta recuperar exclusivamente los Request ID nuevos.

## Evidencia real 1: fallo posterior a los envíos

Diagnóstico:

```text
C:\Users\tellezoe\Downloads\pam-automation-diagnostic-20260922-061148
```

Datos confirmados:

- Línea base: 24 filas y 24 Request ID únicos.
- Solicitudes esperadas: 12.
- Solicitudes confirmadas por el flujo de envío: 12.
- No había solicitudes idénticas en el preflight.
- La reconciliación comenzó después del envío.
- La primera lectura posterior agotó el tiempo dentro de `ScanAllPendingPagesAsync`.
- La traza presentó `attemptCount = 0` porque no llegó a obtener un snapshot posterior completo.
- `failureCode = PortalUnavailable`.
- No fue un problema del comparador, de `System + Account`, de IDs antiguos o de Outlook.

## Corrección ya implementada para la reconciliación posterior

El working tree actual contiene una recuperación de solo lectura dentro de `PamPendingService.WaitForCompleteComparisonAsync`:

1. Intenta leer Pending.
2. Ante `PortalUnavailable` o `InvalidRequest` transitorio, vuelve a navegar a Pending dentro de la misma sesión.
3. Permite dos lecturas por sesión.
4. Si ambas fallan, dispone la sesión temporal.
5. Crea una segunda sesión autenticada destinada exclusivamente a la lectura.
6. Conserva la misma línea base, solicitudes esperadas y configuración temporal.
7. Nunca vuelve a invocar el servicio de envío ni `Submit Request`.

Los límites están en:

```text
PamAutomation.Core/Resilience/PamOperationTimingPolicy.cs
```

Propiedades incorporadas:

```text
PendingReadMaximumAttemptsPerSession = 2
PendingReadSessionMaximumAttempts = 2
PendingReadRecoveryDelay = 2 segundos
```

También existe `PendingReadRecoveryDiagnostic`, que registra acciones como:

```text
ReloadPendingView
ReadSessionFailed
RecreateAuthenticatedReadSession
```

Las pruebas ya agregadas comprueban:

- Recuperación en la misma sesión.
- Reconstrucción de la sesión de lectura.
- Preservación de la línea base al cambiar de sesión.
- Detención acotada ante un timeout persistente.
- Registro diagnóstico de las recuperaciones.

Antes del segundo diagnóstico, la solución compiló sin errores ni advertencias y las 437 pruebas aprobaron.

## Evidencia real 2: fallo durante el preflight

Diagnóstico:

```text
C:\Users\tellezoe\Downloads\pam-automation-diagnostic-20260922-063349
```

Secuencia observada:

```text
06:32:06 Real PAM execution started
06:32:06 Favorites read started
06:32:24 Pending preflight read started
06:33:41 TimeoutException en ScanAllPendingPagesAsync
```

No existen eventos de:

```text
Real execution submission started
Pending reconciliation started
```

Conclusiones:

- Esta ejecución no creó solicitudes.
- No intervino Outlook.
- El timeout ocurrió antes de cualquier operación irreversible.
- La recuperación posterior ya implementada no falló: nunca llegó a ejecutarse.

## Por qué no se generó `pending-reconciliation.json`

El segundo ZIP contiene únicamente:

```text
manifest.json
environment.json
execution-summary.json
diagnostic.log
```

Su manifiesto usa `schemaVersion = 1` porque `DiagnosticExportService` no recibió una traza de Pending.

La causa está en `PamAutomationExecutionService.PrepareRealCoreAsync`: actualmente la traza se inicializa después de que el preflight haya leído Pending correctamente y haya construido la línea base. El flujo efectivo es:

```text
Leer Favorites
    ↓
Leer Pending
    ↓
Construir línea base
    ↓
pendingDiagnosticSink.Begin(...)
```

Si `ReadAllAsync(credentials)` falla durante el preflight, nunca se ejecuta `Begin`. Por tanto, la ausencia del JSON no es un defecto del exportador; la traza se inicializa demasiado tarde.

## Brecha funcional exacta

Actualmente existen dos rutas diferentes:

```text
Preflight:
PamPendingService.ReadAllAsync(PamCredentials)
```

```text
Después del envío:
PamPendingService.WaitForCompleteComparisonAsync(PamCredentials, ...)
```

La recuperación nueva está en la segunda ruta. El preflight continúa utilizando la primera ruta directa, la cual:

- Crea una sola sesión autenticada.
- Ejecuta una sola lectura completa.
- Propaga inmediatamente `PortalUnavailable`.
- No registra una recuperación detallada.

## Resultado requerido

La aplicación debe aplicar una estrategia compartida de lectura resiliente a ambas etapas, sin duplicar reglas y sin ampliar la superficie de acciones irreversibles.

### Preflight

1. Inicializar una traza diagnóstica antes de intentar leer Pending.
2. Marcar la etapa como `Preflight` o equivalente estable.
3. Intentar la lectura inmediatamente.
4. Ante un fallo recuperable, volver a navegar a Pending dentro de la misma sesión.
5. Si la sesión sigue bloqueada, disponerla y crear una segunda sesión autenticada exclusivamente para lectura.
6. Si se recupera, actualizar la traza con la línea base completa y continuar la preparación normal.
7. Si se agotan los intentos, conservar en la traza cada recuperación y el código final.
8. El preflight nunca puede acceder al servicio de envío ni pulsar `Submit Request`.

### Reconciliación posterior

1. Mantener la recuperación que ya existe.
2. Mantener la línea base original sin reemplazarla al reconstruir sesiones.
3. Obtener solo Request ID que no estaban en la línea base.
4. No volver a enviar credenciales/TOTP salvo cuando sea estrictamente necesario reconstruir la sesión de lectura.
5. No volver a ejecutar `Submit Request` bajo ninguna circunstancia.
6. Conservar la semántica existente de ambigüedad, cantidades y campos estables.

## Diseño recomendado

Evitar duplicar los bucles de recuperación entre `ReadAllAsync(PamCredentials)` y `WaitForCompleteComparisonAsync(PamCredentials, ...)`.

Una opción mantenible es extraer en `PamPendingService` una operación privada de lectura autenticada resiliente que:

- Reciba las credenciales.
- Cree y disponga sesiones temporales.
- Ejecute un delegado u operación de lectura de Pending.
- Reintente solamente errores clasificados como lectura transitoria.
- Reciba contexto diagnóstico de etapa e intento.
- Devuelva un snapshot validado.

No convertir esta operación en un reintento genérico de cualquier excepción. Clasificar explícitamente:

```text
Recuperables:
- PamPendingErrorCode.PortalUnavailable
- PamPendingErrorCode.InvalidRequest cuando proviene de un snapshot transitorio

No recuperables automáticamente:
- OperationCanceledException
- PamPendingErrorCode.DuplicateRequestId
- PamPendingErrorCode.AmbiguousIdentity
- PamPendingErrorCode.FieldMismatch
- errores no clasificados
```

Revisar cuidadosamente si `InvalidRequest` necesita una causa o categoría más específica para no reintentar indefinidamente una estructura permanentemente inválida. Todos los límites deben continuar siendo pequeños y explícitos.

## Paquetes, proyectos y clases involucradas

### `PamAutomation.Browser`

Archivo principal:

```text
PamAutomation.Browser/PlaywrightManagedPage.Pending.cs
```

Métodos relevantes:

```text
ReadAllPendingAsync
SelectPendingStatusAsync
SelectLargestPendingPageSizeAsync
ScanAllPendingPagesAsync
ReadStablePendingPageAsync
ReadCurrentPendingPageAsync
```

Responsabilidad:

- Navegación y lectura DOM.
- Distinguir renderizado transitorio, timeout, cancelación y estructura inválida.
- No administrar autenticación ni decidir recrear sesiones completas.

No reducir la seguridad aceptando snapshots parciales. Una lectura válida todavía necesita filas completas, IDs únicos y estabilidad.

### `PamAutomation.Core`

Archivo principal:

```text
PamAutomation.Core/Requests/PamPendingService.cs
```

Responsabilidad:

- Orquestar lecturas de Pending.
- Aplicar recuperación acotada.
- Crear y disponer sesiones autenticadas de lectura.
- Validar el snapshot.
- Comparar con solicitudes esperadas.
- Preservar la línea base.

Archivo coordinador:

```text
PamAutomation.Core/Execution/PamAutomationExecutionService.cs
```

Método relevante:

```text
PrepareRealCoreAsync
```

Responsabilidad pendiente:

- Inicializar el diagnóstico antes del preflight.
- Registrar las solicitudes esperadas aunque todavía no exista una línea base.
- Actualizar la línea base después de una lectura exitosa.
- No cambiar el momento en que se autoriza o ejecuta el envío.

Política temporal:

```text
PamAutomation.Core/Resilience/PamOperationTimingPolicy.cs
```

Conservar aquí todos los límites y retrasos; no dispersar números mágicos.

### `PamAutomation.Core.Diagnostics`

Archivos:

```text
PamAutomation.Core/Diagnostics/PendingReconciliationDiagnostic.cs
PamAutomation.Core/Interfaces/Diagnostics/IPendingReconciliationDiagnosticStore.cs
```

Cambios probablemente necesarios:

- Representar la etapa actual, por ejemplo `Preflight` o `Reconciliation`.
- Permitir iniciar una traza sin línea base todavía.
- Permitir registrar o reemplazar la línea base después de recuperarla.
- Registrar cada intento de lectura y reconstrucción de sesión.
- Mantener `FailureCode` nulo si una recuperación posterior fue exitosa.
- Conservar `ToString()` redactado para impedir filtraciones accidentales.

### `PamAutomation.Infrastructure`

Archivo:

```text
PamAutomation.Infrastructure/Diagnostics/InMemoryPendingReconciliationDiagnosticStore.cs
```

Cambios probablemente necesarios:

- Incorporar una operación defensiva para actualizar el snapshot de línea base.
- Mantener sincronización y copias defensivas.
- Continuar sin escribir archivos automáticamente.

Archivo de exportación:

```text
PamAutomation.Infrastructure/Diagnostics/DiagnosticExportService.cs
```

No existe evidencia de un fallo aquí. Solo verificar que una traza iniciada durante el preflight produzca `pending-reconciliation.json` incluso si su línea base está vacía y terminó con error.

### `PamAutomation.App`

Archivo:

```text
PamAutomation.App/App.xaml.cs
```

Solo debería requerir cambios menores de composición si cambian los constructores o contratos. La recuperación pertenece a Core, no a WPF.

### `PamAutomation.Tests`

Archivos principales:

```text
PamAutomation.Tests/Core/Requests/PamPendingServiceTests.cs
PamAutomation.Tests/Core/Resilience/PamOperationTimingPolicyTests.cs
PamAutomation.Tests/Infrastructure/Diagnostics/InMemoryPendingReconciliationDiagnosticStoreTests.cs
PamAutomation.Tests/Infrastructure/Diagnostics/DiagnosticExportServiceTests.cs
```

## Pruebas de aceptación obligatorias

Agregar o ajustar pruebas que demuestren:

1. Un timeout inicial de preflight se recupera en la misma sesión.
2. Dos fallos de lectura provocan la reconstrucción exclusiva de la sesión de lectura.
3. Un preflight recuperado devuelve el snapshot correcto.
4. Un timeout persistente se detiene al alcanzar el límite.
5. `OperationCanceledException` no se reintenta.
6. `DuplicateRequestId` y `AmbiguousIdentity` no reconstruyen sesiones.
7. El diagnóstico existe aunque el primer preflight nunca consiga una línea base.
8. El diagnóstico distingue etapa, sesión, lectura, acción y código de error.
9. Una recuperación exitosa no deja un `FailureCode` final obsoleto.
10. La línea base obtenida después de recuperarse se utiliza sin cambios durante la reconciliación posterior.
11. Ningún reintento de lectura invoca el servicio de envío.
12. Una reconstrucción posterior al envío conserva los Request ID excluidos.
13. La exportación incluye `pending-reconciliation.json` para un fallo de preflight.
14. Ningún log normal contiene Request ID, `System`, `Account`, descripción, credenciales o TOTP.

Si resulta práctico, agregar una prueba de coordinación alrededor de `PamAutomationExecutionService` que cuente invocaciones al servicio de envío y demuestre que permanece exactamente en una, aunque Pending reconstruya su sesión después de los envíos.

## Comandos de validación

Desde la raíz:

```powershell
dotnet build PamAutomation.slnx -c Release -p:Platform=x64
dotnet run --project PamAutomation.Tests\PamAutomation.Tests.csproj -c Release -p:Platform=x64 --no-build
git diff --check
```

No ejecutar pruebas reales que creen solicitudes o envíen correo. Una prueba real de solo lectura contra Pending solo debe considerarse si el usuario la vuelve a autorizar explícitamente y si es necesaria después de completar las pruebas locales.

## Criterios de terminación

La corrección se considera completa únicamente cuando:

- El preflight y la reconciliación utilizan una política coherente de recuperación de lectura.
- Un timeout de preflight puede recuperarse sin iniciar el envío.
- Un timeout posterior puede recuperarse sin repetir el envío.
- La línea base permanece correcta durante cualquier reconstrucción de sesión.
- Un fallo de preflight genera una traza exportable.
- Los reintentos son acotados, cancelables y observables.
- No se ocultan errores permanentes como si fueran transitorios.
- Las pruebas específicas y la suite completa aprueban.
- La documentación obligatoria queda actualizada.

## Resumen ejecutivo

La aplicación ya sabe recuperar una lectura de Pending después de enviar solicitudes. El nuevo diagnóstico demostró que el mismo timeout puede ocurrir antes, durante el preflight. Esa ruta todavía llama directamente a `ReadAllAsync(PamCredentials)`, no utiliza la recuperación y falla antes de inicializar el diagnóstico detallado. La solución no consiste en aumentar indiscriminadamente los timeouts: debe compartir una estrategia de lectura idempotente, iniciar la traza antes del preflight, actualizar la línea base cuando se obtenga y mantener `Submit Request` completamente fuera de cualquier reintento.
