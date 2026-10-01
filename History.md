# Historial — Pam Automation .NET

## 1 de octubre de 2026 — Reutilización de Favorites antes de cada solicitud

Se atendió primero el punto 4 solicitado: el adaptador volvía a cargar Accounts y seleccionar Favorites para cada cuenta. La nueva preparación comparte la ruta de prueba y producción, reutiliza la vista comprobada, busca desde la página actual y vuelve a resolver la identidad completa al hacer clic. Una recuperación desde Accounts queda limitada a navegación y apertura anteriores al llenado, con dos intentos en total y un presupuesto compartido de 60 segundos.

La inspección real autorizada confirmó que el texto Favorites está dentro de un contenedor con clases text selected. Se usa ese estado junto con la URL, Browse by Category, filas visibles y ausencia del panel de formulario; no se deduce la categoría solo por existir una tabla. La validación y sus resultados se registran en el ajuste correspondiente de Fases.md. No se modifican versión, publicación, lógica de calendario, reconciliación ni correo.

Resultado: Release x64 correcto y 49 pruebas aprobadas sin fallos ni omisiones. Las 15 regresiones locales cubren reutilización, paginación, identidades similares, reordenación, duplicados, ausencia, recuperación acotada y cancelación. En Edge visible se validaron y descartaron los 14 formularios reales; no hubo recargas innecesarias de Accounts y se observó exactamente una al volver desde Requests, salida provocada expresamente a mitad del recorrido. La integración completa tardó 69,5 segundos, con cero intentos de creación y sin Outlook. La opción RegexOptions inicialmente incompatible con Playwright fue corregida antes de la ejecución final. Permanece MSTEST0032 heredada.

## 30 de septiembre de 2026 — Seguimiento: selector nativo del mes

El diagnóstico `pam-automation-diagnostic-20260930-213552` identifica `MonthUnreadable`, fecha objetivo 01/10/2026, cero cambios de mes y mes observado no disponible. El lote tenía 14 accesos y horario 00:00–08:00. El valor `submittedCount=0` del archivo de reconciliación no demuestra cero creaciones: la ejecución aún estaba en etapa Preflight y ese contador se actualiza al completar el lote. No se atribuye el fallo al TOTP ni a Pending.

La primera corrección dependía de observar 28–31 días propios, desde el primero hasta el último. Las capturas del usuario mostraron el selector nativo de mes. La revisión usa ahora esa cabecera y su año, elige la opción por valor exacto y espera coherencia con la grilla antes del día. La prueba anterior sobre una sola cuenta no cubría toda la colección. El recorrido sin envío del código anterior pasó en los 14 accesos tanto visible como headless; por ello la condición transitoria exacta del fallo del usuario no quedó reproducida en vivo. Las nuevas regresiones modelan grilla parcial y cabecera adelantada respecto a los días; no se presenta esa simulación como captura del incidente.

Validación final: compilación Release x64 correcta y 60 pruebas relacionadas aprobadas, cero fallos/omisiones. Las simulaciones detectaron una coincidencia indistinta entre etiqueta numérica y valor de opción; se corrigió mediante `SelectOptionValue.Value` explícito. También se estabilizó el repintado de las páginas locales esperando DOMContentLoaded. En PAM real, el código corregido validó y descartó los 14 accesos para 01/10/2026 00:00–08:00, tanto visible (102,2 s) como headless (94,1 s): 28 formularios. Con la autorización expresa del usuario, una prueba separada creó exactamente tres solicitudes de 00:00–00:05 del 01/10/2026 y recuperó sus tres IDs nuevos en Pending (36,8 s), sin repetir Submit ni abrir Outlook. Los IDs operativos se comunicaron al usuario y no se persisten en la documentación. Sin cambio de versión ni publicación. Permanece la advertencia heredada MSTEST0032.

## 30 de septiembre de 2026 — Selección estable del calendario PAM

- El diagnóstico `pam-automation-diagnostic-20260930-195922` ubicó dos fallos en `SelectStartDateAsync`, al preparar solicitudes para el 1 de octubre. La excepción genérica exportada no permite identificar retrospectivamente la condición exacta. El problema previo de TOTP fue corregido por el usuario y no forma parte de este cambio.
- Se sustituyó el avance ciego al no encontrar el día por `PamCalendarDateSelector`: calendario visible único, mes completo estable, dirección calculada, una acción por transición y confirmación del valor retenido. Excluye días de meses adyacentes, calendarios ocultos y fechas deshabilitadas. Conserva la verificación integral previa a Submit.
- `PamCalendarSelectionException` y el diagnóstico añaden categoría, fecha objetivo, mes observado y conteo de navegación, sin texto libre del portal ni secretos. Los límites están en `PamOperationTimingPolicy`.
- Compilación Release x64 correcta; 57 pruebas relacionadas aprobadas, incluidas 16 regresiones con Edge visible y respuestas locales. Una primera ejecución detectó un timeout demasiado breve del clic de la simulación; se separó del presupuesto de lectura y la repetición completa pasó. La advertencia heredada MSTEST0032 se deja sin cambios.
- Integración real expresamente autorizada: una prueba pasó en 26,7 segundos, confirmó fechas 01/10/2026 y 02/10/2026 de 11:00 a 19:00 y descartó ambos formularios. El adaptador bloqueó el endpoint de creación y verificó cero intentos. No hubo solicitudes ni correos reales. Sin cambio de versión ni publicación; la aceptación de un lote enviado corresponde a una ejecución operativa posterior.

## 27 de septiembre de 2026 — Ajuste visual del selector de acumulación

- Corrección posterior al reporte de texto recortado: altura automática y mínimo de 64 DIP; estado deshabilitado gris explícito en fondo, borde, texto e interruptor. Outlook aparece gris durante acumulación incluso si estaba activado. Compilación Release x64 y tres pruebas WPF aprobadas, con comprobación de texto completo y colores.

- Se reemplaza la casilla estándar por un `ToggleButton` que reutiliza el estilo de la acción final, situado inmediatamente a su derecha. Etiqueta propia en dos líneas, colores y estados visuales coherentes con la aplicación.
- Compilación Release x64 correcta y tres pruebas WPF de la ventana principal aprobadas, incluida alineación y ausencia de desbordamiento a 800×540 y 1050×660. Permanece la advertencia heredada MSTEST0032; no se cambió la lógica, versión ni publicación.

## 27 de septiembre de 2026 — Fase 27 implementada, acumulado y correo manual

- Se añade `RequestIdAccumulator` en Core: lotes completos nuevos, deduplicación, vínculo por GUID y estados de envío exclusivamente en memoria.
- La pantalla principal incorpora modo, contador, envío manual y descarte. Las acciones finales automáticas se deshabilitan sin modificar su preferencia; Pending idéntico bloquea solo la ejecución acumulativa.
- El servicio de colecciones bloquea selección/desactivación/eliminación del vínculo; actualizar Favorites conserva la selección incluso vacía. Se permite corregir el correo de esa colección sin perder los IDs.
- El envío usa correo actual, remitente principal y firma común con una única confirmación. Se congela la operación y se reutilizan las barreras de Outlook. Incertidumbre conserva IDs y permite comprobar el mismo mensaje sin reenviarlo; confirmación en Enviados o descarte explícito libera el vínculo.
- Cierre normal con aviso y elección segura; cierre de Windows sin espera modal. Descartar no cancela PAM ni elimina mensajes de Outlook. No hay recuperación tras cierre inesperado.
- Evidencia: compilación Release x64 correcta y 119 pruebas relacionadas aprobadas, 0 fallidas/omitidas; incluye WPF, cierre, selección, fallos, doble clic, firma y rutas de correo mediante dobles. La primera pasada detectó dos fixtures con catálogo vacío, incompatible con el contrato existente; se ajustaron para retirar las cuentas de la colección con un catálogo válido y la selección final aprobó completa. Se conserva MSTEST0032 heredada. No se operó PAM/Outlook real, no se cambió versión ni se publicó.

## 27 de septiembre de 2026 — Planificación de fase 27, acumulado por colección

- El usuario confirma que varias automatizaciones podrán reunir sus Request ID en memoria para un único correo manual, exclusivamente dentro de una misma colección.
- Con IDs acumulados, cambiar de colección o desactivar el modo queda bloqueado hasta enviar de forma confirmada o descartar. Las acciones finales automáticas se deshabilitan mientras el modo esté activo.
- Se documentan incorporación atómica de resultados nuevos reconciliados, preservación ante fallos, bloqueo de selección automática/eliminación, configuración de correo de la colección vinculada, estado incierto de Outlook sin reenvío y advertencia de cierre.
- El alcance conservador evita reutilizar automáticamente IDs antiguos en este modo; los duplicados exactos bloquean esa ejecución. La opción habitual de correo con IDs existentes permanece fuera del modo.
- Solo se actualiza documentación; implementación y pruebas quedan pendientes de Astra. No se modificó código ni se ejecutaron PAM/Outlook.

## 27 de septiembre de 2026 — Fase 26, correo por colección y firma del principal

- Se implementan los cuatro campos de correo e imágenes inline por colección, incluido `All favorites`, dentro del documento protegido de colecciones (esquema 3).
- La migración desde esquemas 1/2 copia el correo global a las colecciones ya guardadas, verifica la lectura del nuevo estado y conserva el archivo legado. Las copias son independientes; nuevas colecciones no heredan correo y su eliminación retira también el contenido.
- `Configurar correo de esta colección` reutiliza el editor enriquecido con el remitente principal informativo. Los cambios solo persisten al guardar la colección; cancelar descarta también el borrador de correo.
- El botón principal pasa a `Configurar firma del correo`. Firma opcional, protegida separadamente y agregada una sola vez en la plantilla efímera de envío. El contenido de las colecciones no se reescribe. La firma antigua, si estaba pegada dentro del correo global, se conserva como contenido y debe retirarse manualmente al configurar la nueva firma común.
- Confirmación, inicio y reutilización de Pending utilizan exclusivamente el correo activo. Outlook incompleto bloquea antes de contactar PAM; Outlook desactivado no exige correo ni firma. SMTP continúa asociado al principal.
- Verificación local: compilación Release x64 y pruebas focalizadas de migración, firma, colecciones, editores WPF, confirmaciones y composición/envío mediante dobles. Ninguna solicitud PAM ni correo real. Se conserva la advertencia previa MSTEST0032 de la prueba de descubrimiento, ajena a esta fase.

## 8 de septiembre de 2026 — Inicio documental

- Se decidió construir una aplicación nueva desde cero exclusivamente con C# y .NET.
- La aplicación objetivo será Windows x64 con WPF/XAML.
- La automatización web utilizará Microsoft.Playwright con Microsoft Edge instalado externamente.
- No se incluirán Chromium, Node.js, JavaScript, Python, PowerShell, Electron, Selenium ni WebView2.
- Outlook clásico se integrará mediante Microsoft Office Interop/COM.
- La aplicación anterior en C:\pam automatizacion queda como referencia funcional y visual de solo lectura.
- Se creó la documentación inicial propia del nuevo proyecto.
- Todavía no existe código fuente, solución .NET, instalador ni prueba de integración.
- No se han usado credenciales reales, no se han creado solicitudes PAM y no se han enviado correos desde este proyecto.

## Decisiones pendientes

- Confirmar si la API oficial de BeyondTrust puede cubrir alguna parte del flujo sin sustituir Playwright.
- Validar mediante un prototipo el editor nativo de contenido enriquecido y su conversión limitada a HTML para Outlook.
- Definir MSI o MSIX como formato final de distribución.
- Confirmar políticas corporativas de Edge y Outlook clásico.

## 8 de septiembre de 2026 — Correcciones de pruebas, perfil y distribución

- Se autorizó el uso de credenciales reales únicamente en pruebas de integración visibles y explícitamente autorizadas; las pruebas unitarias y simulaciones seguirán usando datos falsos.
- Se decidió utilizar Edge instalado con un contexto temporal de Playwright, sin seleccionar ni reutilizar perfiles existentes.
- La aplicación no debe conectarse a un Edge ya abierto ni modificar perfiles, cookies, extensiones o sesiones del usuario.
- Se explicitó que el entregable final será un ejecutable Windows x64 autocontenido que no requerirá .NET, Node.js, Python ni PowerShell instalados.
- Edge, Outlook clásico, Windows y la conectividad al portal PAM permanecen como requisitos externos deliberados.

## 8 de septiembre de 2026 — Decisión sobre perfiles del navegador

- Se eligió la opción 1 para Playwright.
- La aplicación lanzará el Edge instalado y utilizará un contexto temporal de Playwright.
- No seleccionará ni reutilizará perfiles existentes, cookies, extensiones o sesiones del usuario.
- No utilizará LaunchPersistentContext, userDataDir ni conexión CDP a un navegador existente.
- El contexto temporal no debe mostrarse como una ventana de incógnito; el aislamiento será interno de Playwright.

## 8 de septiembre de 2026 — Persistencia local y credenciales

- Se decidió no utilizar archivos .env en la aplicación nueva.
- Las credenciales PAM se almacenarán en Windows Credential Manager por usuario de Windows.
- La configuración general se guardará en app-settings.json bajo %LOCALAPPDATA%\PamAutomation.
- La configuración de correo se guardará en mail-settings.dat y se protegerá con DPAPI CurrentUser.
- La configuración privada permanecerá bajo %LOCALAPPDATA%\PamAutomation\config.
- Se descartó guardar resultados, logs, artefactos y temporales operativos en la PC del usuario. Los resultados vivirán en memoria durante la ejecución y no habrá auditoría local por defecto.
- La configuración utilizará schemaVersion y escrituras atómicas.

## 8 de septiembre de 2026 — Entorno de desarrollo inicial

- Para desarrollar la aplicación se utilizarán las herramientas de escritorio de .NET.
- No se requieren .NET MAUI, WinUI, C++, desarrollo móvil, ASP.NET, Node.js, Python, Java ni componentes opcionales como Blend o Copilot.
- La solución se desarrollará con WPF, XAML, C# y .NET 10 LTS.

## 8 de septiembre de 2026 — Eliminación de archivos operativos persistentes

- La estructura persistente se limitará a `%LOCALAPPDATA%\PamAutomation\config\app-settings.json` y `mail-settings.dat`.
- Las credenciales permanecerán en Windows Credential Manager y la configuración de correo seguirá protegida con DPAPI CurrentUser.
- No se crearán carpetas persistentes de `results`, `logs`, `artifacts` o `temp`.
- Request ID, conflictos y estados se conservarán únicamente en memoria durante la ejecución.
- El debugging o diagnóstico requerirá una activación y exportación explícitas por parte del desarrollador o soporte.

## 8 de septiembre de 2026 — Separación entre desarrollo y publicación

- La aplicación publicada no persistirá logs, resultados, artefactos ni temporales en la PC de los usuarios.
- El repositorio de desarrollo sí podrá crear `results`, `logs`, `artifacts` y `temp` en su raíz para facilitar el análisis de fallos.
- Esas carpetas serán exclusivas de desarrollo, estarán excluidas de Git, no se incluirán en el instalador y no contendrán secretos.

## 8 de septiembre de 2026 — Alineación de la solución y decisiones iniciales

- Se confirmó que la raíz oficial es `C:\pam automatizacion dotnet\PamAutomation`.
- Se confirmó que la solución existente utiliza `PamAutomation.slnx`; no se requiere convertirla a `.sln`.
- Se actualizó el estado documental: ya existe `PamAutomation.App`, un proyecto WPF mínimo para `net10.0-windows`; todavía faltan los demás proyectos y toda la implementación funcional.
- Inicialmente se estableció un flujo de compilación y pruebas dependiente de un entorno gráfico; esta decisión fue sustituida posteriormente por el uso exclusivo de la terminal y la CLI `dotnet`.
- Se eligió MSTest con Microsoft Testing Platform por su runner integrado y su soporte para pruebas STA necesarias alrededor de Outlook COM.
- Se definió `testModeEnabled`, falso por defecto, como opción no sensible para habilitar el botón de prueba. El usuario podrá cambiarla desde una sección avanzada de Configuración de solicitud y el botón requerirá configuración válida.
- Se autorizó el envío manual de correos del modo de prueba únicamente a `tellezoe@hitss.com`, sin CC, con confirmación explícita y asunto `[PRUEBA]`.
- Se decidió conservar las reglas funcionales comprobadas del flujo anterior, pero utilizar Edge visible y un contexto temporal en la primera versión; no se reutilizarán perfiles y no se habilitará headless inicialmente.
- Se decidió evitar persistencia local de resultados y huellas de correo. La protección contra duplicados combinará estado en memoria con consulta de Enviados, Bandeja de salida y Borradores de Outlook.
- Se seleccionó `RichTextBox`/`FlowDocument` con un convertidor C# limitado a HTML saneado como dirección del editor. La viabilidad de tablas, imágenes CID y fidelidad de ida y vuelta sigue pendiente de un prototipo antes de implementar el editor completo.
- En esta actualización solo se modificó documentación; no se cambió código, no se compiló, no se accedió a PAM y no se abrió ni utilizó Outlook.

## 8 de septiembre de 2026 — Fase 1: estructura base

- Se agregaron a `PamAutomation.slnx` los proyectos Core, Browser, Outlook, Infrastructure y Tests junto al proyecto WPF existente.
- App referencia Browser, Core, Infrastructure y Outlook. Browser, Infrastructure y Outlook referencian únicamente Core. Tests referencia inicialmente Core.
- Se configuró MSTest SDK 4.1.0 con Microsoft Testing Platform y una prueba mínima de descubrimiento del runner.
- Se eliminaron `using` y espacios vacíos innecesarios de la plantilla WPF, sin agregar comportamiento funcional.
- No se agregaron Playwright, Outlook Interop, Credential Manager, DPAPI, TOTP ni lógica PAM.
- La compilación Release y la ejecución de la prueba quedaron pendientes en ese momento porque el flujo inicial todavía no autorizaba comandos de terminal.
- No se accedió a PAM, no se utilizaron credenciales reales, no se crearon solicitudes y no se enviaron correos.

## Regla histórica

Cada cambio relevante debe registrar qué se decidió, por qué, qué se verificó y qué queda pendiente.

## 8 de septiembre de 2026 — Fase 2: presentación WPF y MVVM

- Se reemplazó la ventana WPF vacía por la pantalla principal de configuración, acción final, estado y acciones de prueba y ejecución.
- Se agregó `CommunityToolkit.Mvvm` 8.4.2 para mantener propiedades observables y comandos fuera del code-behind.
- Se centralizaron colores y estilos XAML para facilitar consistencia, escalado y mantenimiento.
- La aplicación inicia con configuración incompleta; la automatización real permanece deshabilitada hasta cumplir sus requisitos y el botón de prueba requiere modo de prueba y configuración válida.
- Se agregaron pruebas unitarias para el estado inicial seguro, el bloqueo durante una ejecución y las condiciones de visibilidad del modo de prueba.
- Se verificó estáticamente que los archivos XAML son XML válido y que el diff no contiene errores de espacios. La restauración de paquetes, compilación y ejecución de pruebas quedaron pendientes en ese momento.
- Esta fase no agregó persistencia, credenciales, Playwright, acceso a PAM, Outlook ni envío de correo; no produjo efectos externos.

## 8 de septiembre de 2026 — Fase 2B: alineación visual con la aplicación anterior

- El usuario confirmó que la fase 2 compiló, pasó sus comprobaciones y pudo visualizarse correctamente.
- Se realineó la portada con las capturas de la aplicación anterior: encabezado `Accesos PAM`, marca con la letra P, fondo claro con degradado, selector Outlook superior, acción azul principal centrada y tres accesos horizontales de configuración.
- Se conservaron el ViewModel, las condiciones seguras de habilitación, el botón de prueba condicionado y las pruebas de presentación existentes.
- Se aclaró que existen dos acciones diferentes: la verificación manual de Outlook puede enviar a la misma cuenta configurada; el correo de resultados del modo de prueba PAM continúa limitado a `tellezoe@hitss.com`.
- El rediseño utiliza únicamente WPF/XAML y recursos nativos; no se copió código, HTML, CSS o JavaScript de la aplicación anterior.
- Se comprobó estáticamente la estructura XML de los XAML. La compilación, las pruebas y la revisión visual del nuevo diseño quedaron pendientes en ese momento.
- No se accedió a PAM, no se utilizaron credenciales, no se crearon solicitudes y no se envió correo.

## 8 de septiembre de 2026 — Documentación de estructura

- Se creó `structure.md` como inventario de la estructura física actual y referencia de la arquitectura objetivo.
- Se documentaron las responsabilidades y límites de App, Core, Browser, Infrastructure, Outlook y Tests.
- Se estableció la dirección permitida de dependencias y se aclaró que actualmente solo App y Tests contienen implementación.
- Se documentó una estructura final orientativa que deberá materializarse gradualmente, sin crear carpetas vacías por adelantado.
- Este cambio fue únicamente documental; no se modificó código, proyectos ni referencias de la solución.

## 9 de septiembre de 2026 — Fase 3A: dominio de configuración no sensible

- El usuario confirmó que la fase 2B compiló y se visualizó correctamente con `Release | x64`; la composición fue aprobada, pero sus elementos se perciben demasiado grandes y su escala queda pendiente de ajuste.
- Se agregaron en Core `AppSettings`, `RequestSettings` y `FinalAction`, sin dependencias de WPF o Windows.
- Los valores predeterminados mantienen `schemaVersion = 1`, `America/Guayaquil`, visualización de IDs y modo de prueba deshabilitado.
- Se agregaron validadores para esquema, acción final, zona horaria, fecha, hora, duración de 1 a 8 horas y razón obligatoria.
- La hora actual de Ecuador se recibe como entrada del validador para permitir pruebas deterministas sin implementar todavía un reloj del sistema.
- Se agregaron pruebas MSTest unitarias para escenarios válidos, nulos, límites y múltiples errores simultáneos.
- No se implementó persistencia, interfaz de configuración, Credential Manager, DPAPI, TOTP, Edge, PAM u Outlook.
- La revisión estática quedó a cargo de esta fase; la compilación y ejecución de pruebas quedaron pendientes en ese momento.

## 9 de septiembre de 2026 — Fase 3B: persistencia JSON no sensible

- El usuario confirmó que la fase 3A compiló y que todas sus pruebas terminaron correctamente con `Release | x64`.
- Se agregó `IAppSettingsStore` en Core para separar el dominio del almacenamiento físico.
- Se implementó `JsonAppSettingsStore` en Infrastructure con la ruta predeterminada `%LOCALAPPDATA%\PamAutomation\config\app-settings.json`.
- El formato persistido es plano, versionado y estricto; utiliza representaciones ISO para `DateOnly` y `TimeOnly` y rechaza JSON inválido, propiedades desconocidas, valores faltantes y esquemas incompatibles.
- Un archivo inexistente devuelve valores predeterminados seguros sin crear almacenamiento. Guardar crea la carpeta cuando se necesita y sustituye el archivo mediante un temporal único en la misma ubicación.
- Se agregaron pruebas MSTest aisladas para valores predeterminados, formato JSON, ida y vuelta, reemplazo, cancelación segura, limpieza de temporales, contenido inválido y versión incompatible.
- `PamAutomation.Tests` ahora referencia `PamAutomation.Infrastructure` para probar la implementación concreta.
- No se conectó todavía la persistencia con la interfaz y no se implementaron Credential Manager, DPAPI, correo, TOTP, Edge o PAM.
- La revisión estática quedó a cargo de esta fase; la compilación y ejecución de pruebas quedaron pendientes en ese momento.

## 9 de septiembre de 2026 — Fase 4: credenciales PAM protegidas

- El usuario confirmó que las fases 3A y 3B compilaron y que todas sus pruebas terminaron correctamente con `Release | x64`.
- Se agregaron en Core `PamCredentials`, `PamCredentialUpdate`, `IPamCredentialStore` y errores estables de almacenamiento.
- Se implementó `WindowsPamCredentialStore` sobre `CredReadW`, `CredWriteW`, `CredDeleteW` y `CredFree`, sin dependencias NuGet nuevas.
- Usuario, contraseña y semilla TOTP se guardan juntos en una única credencial genérica versionada bajo `PamAutomation/PamCredentials/v1`.
- Una edición con campos vacíos conserva internamente los valores protegidos existentes; una primera configuración incompleta se rechaza.
- El blob binario incluye versión y longitudes, respeta los límites de Windows Credential Manager y sus copias administradas y no administradas se limpian con ceros después de usarse.
- Los modelos sensibles redactan `ToString()` y su presentación en el depurador; los mensajes controlados no contienen valores de credenciales.
- Se agregaron pruebas MSTest con una API de Windows falsa para no crear, leer, modificar ni eliminar credenciales reales.
- Se eliminó de `README.md` una frase heredada sobre una carpeta común de datos que contradecía la regla canónica de no persistir archivos operativos.
- La interfaz todavía no usa este almacén y no se implementaron TOTP, Edge, acceso a PAM, DPAPI de correo u Outlook.
- La revisión estática quedó a cargo de esta fase; la compilación y ejecución de pruebas quedaron pendientes en ese momento.

## 9 de septiembre de 2026 — Fase 5: generación TOTP

- El usuario confirmó que la fase 4 compiló y que todas sus pruebas terminaron correctamente con `Release | x64`.
- Se agregó en Core un decodificador Base32 estricto que acepta el formato agrupado con espacios mostrado por Microsoft Authenticator, sin registrar ni reproducir la clave en errores.
- Se implementó RFC 6238 con HMAC-SHA1, intervalos de 30 segundos y códigos de seis dígitos.
- El resultado representa explícitamente el inicio y la expiración UTC del código y permite exigir al menos ocho segundos de vigencia antes de enviarlo.
- El código TOTP se oculta en `ToString()` y en el depurador; las copias binarias administradas de la clave y del hash se limpian después del cálculo.
- Se agregaron pruebas MSTest con vectores públicos RFC 6238, formatos Base32 válidos e inválidos, límites de ventana, vigencia mínima y redacción del resultado.
- La validación inicial detectó una ambigüedad de precedencia entre el operador módulo y una expresión `switch`; se hizo explícito el residuo Base32 para rechazar correctamente longitudes de un solo símbolo.
- Se completó `Fases.md` como resumen acumulativo de las etapas construidas y se añadió al inventario documental.
- No se utilizó la clave real compartida por el usuario, no se leyeron credenciales protegidas, no se abrió Edge, no se contactó PAM y no se envió correo.
- La hora HTTP del portal y su abstracción de reloj quedan para la fase 6.

## 9 de septiembre de 2026 — Flujo de desarrollo exclusivo por terminal

- El usuario sustituyó la decisión anterior y estableció que restauración, compilación, ejecución, pruebas y publicación deben realizarse desde la terminal.
- Se confirmó `dotnet` en `PATH` con el SDK x64 de .NET 10.0.400.
- La solución se restauró y compiló con `Release | x64`: 0 errores y una advertencia heredada en `TestExplorerSmokeTests.cs` por una aserción siempre verdadera.
- El intento con `dotnet test` confirmó que el destino VSTest no es compatible con Microsoft Testing Platform 2.1 bajo .NET 10 sin seleccionar el runner MTP.
- El runner integrado se ejecutó directamente mediante `dotnet run` sobre `PamAutomation.Tests`: 71 pruebas correctas, 0 fallidas y 0 omitidas.
- La documentación canónica se actualizó para usar exclusivamente los comandos `dotnet` desde la terminal y dejar de requerir un entorno gráfico de desarrollo.

## 9 de septiembre de 2026 — Protocolo de trabajo con subagentes

- Se documentó en `Subagentes.md` el uso recomendado de GPT-5.6 Sol `medium` como coordinador y hasta dos GPT-5.6 Terra `medium` como implementadores.
- Se estableció que el coordinador conserva la responsabilidad de dividir, revisar, integrar y validar todo el trabajo.
- Se aclaró que el aislamiento no debe asumirse: las escrituras paralelas requieren ramas y worktrees preparados explícitamente, cuya base es una instantánea y no se sincroniza automáticamente.
- Se definieron contratos de delegación, límites de archivos, integración secuencial, validación `Release | x64`, protección de secretos y condiciones obligatorias para detenerse.
- Se incluyeron medidas para limitar el consumo de tokens y reservar razonamiento `high` para revisiones puntuales complejas o de seguridad.
- Se distinguió la política de lectura por rol: el coordinador conserva la lectura completa y entrega a cada subagente un paquete de contexto con rutas, alcance, restricciones y secciones concretas.
- Los subagentes no deben recorrer todo el repositorio ni releer todos los Markdown por defecto; solo pueden ampliar la lectura ante una dependencia directa, un riesgo de seguridad o una contradicción, dejando constancia de ello.
- Se alinearon `AGENTS.md`, `CODEX_BUILD_SPEC.md` y `Subagentes.md` para eliminar la obligación contradictoria de repetir toda la documentación en cada agente delegado.
- Se refinó la lectura documental del coordinador: debe completarla únicamente la primera vez que un thread de Codex entra al proyecto sin contexto previo. Las nuevas tareas, fases o prompts dentro del mismo thread conservan el contexto y usan `AGENTS.md` como índice para consultar solo lo necesario; una edición hecha por el propio agente tampoco fuerza otra lectura completa.
- Se convirtió el máximo de dos subagentes en un límite absoluto por task, no en una cantidad que deba agotarse.
- Cada subagente de implementación debe recibir una unidad vertical independiente y ser responsable de su código, sus pruebas relacionadas y su validación focalizada; queda descartado dividir una misma característica entre un productor de código y otro agente que espere para escribir sus pruebas.
- Se adoptó un trabajo por oleadas: el coordinador fija primero contratos y propietarios de archivos compartidos, delega hasta dos tareas que puedan avanzar sin esperarse, e integra y valida un resultado a la vez antes de iniciar trabajo dependiente.
- Se hizo explícito que un cambio pequeño, concentrado o sin paralelismo real debe implementarlo directamente el agente principal para evitar el consumo adicional de contexto, supervisión, revisión e integración.
- `Fases.md` ahora clasifica cada fase pendiente como independiente, independiente condicionada o dependiente, registra sus prerrequisitos y propone únicamente dos combinaciones iniciales de paralelismo: fases 6 con 7 y, después de integrarlas, fase 8 con 14.
- La clasificación de paralelismo no autoriza automáticamente a crear subagentes: el coordinador debe volver a comprobar contratos, archivos y tamaño real antes de cada oleada.
- Este cambio fue exclusivamente documental; no se modificó código, no se ejecutaron integraciones reales y no se crearon subagentes.

## 9 de septiembre de 2026 — Fases 6 y 7: reloj HTTP y sesión Edge restringida

- Se desarrollaron en paralelo mediante un subagente aislado por fase y se integraron secuencialmente después de revisar y validar cada resultado.
- La fase 6 agregó contratos de reloj en Core y una implementación en Infrastructure calibrada con HTTP `Date`, resolución del encabezado, mitad del tiempo de ida y vuelta y avance monotónico, sin respaldo al reloj local.
- La fase 7 agregó Microsoft.Playwright 1.62.0, contratos de sesión en Core y una implementación en Browser que usa Edge externo visible con `Channel = "msedge"` y un contexto temporal.
- El contexto restringe solicitudes HTTP(S) al origen exacto `https://10.30.17.117`, bloquea service workers y limita `IgnoreHTTPSErrors` a la sesión PAM.
- El contrato de navegación expone solamente URI final, estado y HTTP `Date`; no enumera ni entrega cookies, tokens u otras cabeceras.
- Se añadieron errores estables para distinguir un fallo al lanzar Edge de un fallo al inicializar la sesión y se garantizó la liberación de recursos creados parcialmente.
- Las pruebas usan adaptadores falsos y no abren Edge, no contactan PAM, no leen credenciales reales, no crean solicitudes y no envían correo.
- La validación consolidada con `Release | x64` compiló sin errores y ejecutó 90 pruebas correctas; permanece una advertencia MSTest heredada en `TestExplorerSmokeTests.cs`.

## 9 de septiembre de 2026 — Fases 8 y 14: autenticación simulada y Outlook clásico

- Se desarrollaron en paralelo mediante dos subagentes nativos y worktrees separados, con integración y validación secuencial por el coordinador.
- La fase 8 agregó un servicio de autenticación simulado que coordina sesión Browser, reloj HTTP, TOTP, espera de ventana segura, reintento único y confirmación final mediante contratos testeables.
- `PamNavigationResponse` ahora incluye el tiempo monotónico de ida y vuelta utilizado para calibrar el reloj del portal.
- La fase 14 agregó contratos y modelos redactados de Outlook, validación de cuenta y mensaje, ejecución STA y un adaptador Microsoft Office Interop/COM con liberación explícita.
- Se actualizó `Microsoft.Office.Interop.Outlook` a 15.0.4797.1004, que restauró y compiló sin la advertencia `NU1701` observada con la versión anterior.
- Las pruebas de Outlook usan un seam COM falso; no abren Outlook ni envían mensajes. El resultado posterior a `Send()` queda como no confirmado hasta consultar Enviados.
- La herramienta de parche de los subagentes nativos quedó inicialmente anclada al checkout principal aunque sus comandos estaban en los worktrees. Ambos se detuvieron o fueron interrumpidos; los cambios se replicaron mediante rutas verificadas hacia cada worktree y el checkout principal se limpió antes de integrar.
- La validación consolidada compiló en `Release | x64` con 0 errores y ejecutó 116 pruebas correctas. No se abrió Edge, no se contactó PAM, no se inició Outlook real y no se envió correo.

## 9 de septiembre de 2026 — Fase 9: autenticación PAM real visible

- El usuario autorizó explícitamente una prueba real limitada al login contra `https://10.30.17.117/webconsole/login?redir=dashboard` y prohibió solicitar accesos.
- Las credenciales proporcionadas se guardaron en Windows Credential Manager bajo `PamAutomation/PamCredentials/v1`; no se incorporaron a archivos, código, logs, capturas ni documentación.
- Se implementaron `PamAuthenticationPortalFactory` y `PamAuthenticationPortal`, enlazados exclusivamente con la página del contexto temporal creado por `PamBrowserSessionFactory`.
- Se centralizaron los nombres accesibles observados para usuario, contraseña, inicio de sesión, desafío TOTP, envío y confirmación del dashboard.
- La primera integración descubrió que el portal emite `Navigation complete.` mediante `role=alert`; se corrigió su clasificación para no confundir ese anuncio de transición con un rechazo de credenciales.
- Los estados ambiguos o agotados no se convierten en rechazos explícitos y, por tanto, no habilitan reintentos adicionales.
- Una integración posterior abrió Edge externo visible, utilizó el contexto no persistente y la hora HTTP del portal, completó el TOTP y confirmó `Return to dashboard`.
- La centralización inicial del patrón de rechazo TOTP añadió `RegexOptions.CultureInvariant`, una bandera que Playwright .NET no admite en locators; la integración se detuvo antes de introducir el código. Se retiró exclusivamente esa bandera y la validación real final volvió a confirmar el dashboard.
- La ejecución terminó inmediatamente después del login y cerró el contexto; no navegó a Favorites, no abrió formularios, no creó solicitudes, no inició Outlook y no envió correo.
- La validación consolidada compiló en `Release | x64` con 0 errores y ejecutó 127 pruebas correctas, 0 fallidas y 0 omitidas. Permanece la advertencia heredada `MSTEST0032` fuera del alcance de esta fase.

## 9 de septiembre de 2026 — Fase 10: navegación de solo lectura a Favorites

- El usuario autorizó continuar con la fase 10 utilizando el agente principal y sin subagentes; la inspección real se limitó a navegación y lectura, manteniendo prohibido solicitar accesos.
- `PamAuthenticationService` ahora implementa `IPamAuthenticatedSessionFactory` y puede transferir al consumidor una sesión autenticada confirmada; el consumidor conserva la responsabilidad de cerrarla. El método de verificación anterior sigue cerrando automáticamente su propia sesión.
- Se agregaron `PamFavoriteAccount`, errores estables, contratos de portal y `PamFavoritesService` en Core. Las identidades se validan como completas y únicas y su representación textual permanece redactada.
- `PamAutomation.Browser` navega a Accounts, identifica `Browse by Category > Favorites`, espera filas completas y estables, lee `SystemName`, `ApplicationName` y `AccountName` y recorre el paginador accesible cuando existe, con un límite seguro de 500 páginas.
- El contrato de fase 10 no permite seleccionar filas, abrir formularios ni enviar solicitudes. Los valores observados se conservaron únicamente en memoria y no se imprimieron ni persistieron.
- La integración `PAM_AUTOMATION_ALLOW_REAL_FAVORITES=READ_ONLY` abrió Edge visible, reutilizó la sesión autenticada, confirmó cuentas válidas en Favorites y cerró el contexto temporal.
- No se abrieron formularios, no se pulsó `Submit Request`, no se crearon solicitudes, no se inició Outlook y no se envió correo.
- La validación consolidada compiló en `Release | x64` y ejecutó 146 pruebas correctas, 0 fallidas y 0 omitidas. La advertencia heredada `MSTEST0032` permanece fuera del alcance de esta fase.

## 9 de septiembre de 2026 — Fase 11: modo de prueba de solicitudes

- El usuario autorizó el desarrollo con el agente principal y la prueba real visible de formularios, manteniendo prohibido crear solicitudes.
- Se agregó `PamRequestTestService`, que reutiliza una única sesión autenticada para leer Favorites y procesar cada formulario.
- `IPamRequestTestPortal` solo permite validar y descartar; deliberadamente no contiene una operación de envío.
- El adaptador Browser vuelve a comprobar la identidad de cada fila, abre `Access`, selecciona la pestaña del formulario, llena fecha, hora, duración y razón, verifica `Application Session` y ejecuta `Cancel > Discard Changes`.
- Antes de abrir formularios se instala un bloqueo de red para el endpoint de creación; cualquier intento de `POST /api/ps-portal/requests` se aborta y se informa como fallo.
- La primera integración se detuvo de forma segura porque React restableció `Start Time`; se incorporó el segundo llenado condicionado ya conocido en la referencia anterior.
- Otra ejecución reveló que ciertos formularios abren Flatpickr automáticamente; la selección de fecha se hizo idempotente para no cerrar el calendario con un clic redundante.
- La integración final abrió Edge visible y confirmó 14 `Favorites` rows encontradas, 14 formularios validados y descartados y `submitted = 0`.
- No se creó ninguna solicitud, no se abrió Outlook, no se envió correo y no se imprimieron ni persistieron identidades, credenciales o valores del formulario.
- La solución compiló en `Release | x64` con 0 errores y el proyecto de pruebas ejecutó 154 pruebas correctas. Permanece la advertencia heredada `MSTEST0032`, fuera del alcance de la fase.

## 9 de septiembre de 2026 — Plan post-migración: colecciones personalizadas

- El usuario definió una ampliación nueva que no existe en la aplicación JavaScript anterior y debe comenzar solo después de completar la migración.
- Una colección local permitirá seleccionar cualquier subconjunto de `Favorites` rows, asignarle un nombre editable y conservar fecha, hora, duración y razón predeterminadas.
- Se registró como fase 20, dependiente de la finalización de la migración y de las pruebas administradas de fase 19; no cambia el alcance de fase 12.
- Se tomó la referencia visual como guía para día, mes, descripción completa de fecha, entrada de hora con `AM/PM`, duración en horas, razón y guardado explícito.
- Se previeron sobrescrituras opcionales por `Favorites` row para solicitudes personalizadas, cuya experiencia exacta deberá confirmarse al diseñar la fase.
- `Favorites` permanecerá como fuente de verdad. Las colecciones guardarán `System + Application + Account` y se reconciliarán con todas las páginas para detectar filas `Available`, `Missing`, `New in Favorites` o `Ambiguous` sin sustituciones silenciosas.
- Las colecciones se tratarán como configuración privada, versionada, atómica y protegida con DPAPI `CurrentUser`; no contendrán credenciales, TOTP, cookies ni resultados.
- Se creó `PAM_NAVIGATION.md` con el recorrido web confirmado, selectores, comportamiento observado de los controles e invariantes necesarios para implementar colecciones sin depender de índices del grid.
- El usuario aclaró que los nombres visibles del portal deben conservarse en inglés. Se documentaron `Account`, `Description`, `System`, `Account Status`, `IP Address`, `Platform` y `Application`, y se reservó `Favorites` row para referirse inequívocamente a una fila completa.
- Se dejó explícito que `Account` no identifica por sí solo una fila: puede repetirse y la clave confirmada hasta fase 11 es `System + Application + Account`.
- Este cambio fue exclusivamente documental: no se inició la fase 12, no se modificó código, no se abrió PAM y no se creó ninguna solicitud.

## 10 de septiembre de 2026 — Fase 12: solicitudes reales confirmadas

- El usuario autorizó el desarrollo con el agente principal, sin subagentes, y permitió exactamente una solicitud PAM real para hoy con duración de 5 minutos o el mínimo disponible.
- Se agregó `IPamRequestSubmissionPortal` como contrato separado del modo de prueba. El contrato seguro de fase 11 conserva su bloqueo de red y continúa sin capacidad de envío.
- `PamRequestSubmissionService` valida la configuración, reutiliza una sola sesión autenticada, vuelve a leer todas las `Favorites` rows y exige una autorización ligada al conteo y a la configuración exactos mostrados al usuario.
- Si `Favorites` o la configuración cambian después de confirmar, el flujo termina antes de crear el portal de envío. Antes de cada fila también se respeta la cancelación; después del clic se espera la respuesta para no presentar como cancelada una mutación cuyo estado remoto sea ambiguo.
- Browser vuelve a localizar la fila por `System + Application + Account`, abre `Access > Submit Request`, llena el formulario y verifica inmediatamente sus valores y `Application Session` antes del botón final.
- El envío espera específicamente `POST /api/ps-portal/requests`. Solo 2xx se considera `Submitted`; el 400 con código exacto `PSPortal.Requests.Policies.RemoteAppAccesssAlreadyAvailable` se clasifica como conflicto conocido y permite continuar; cualquier otra respuesta detiene el proceso sin exponer el payload.
- Los resultados se copian y permanecen en memoria. Sus representaciones textuales están redactadas y no se añadieron logs persistentes ni datos de cuenta a la salida de pruebas.
- La integración destructiva quedó aislada bajo la categoría `ExplicitPamMutation`, el opt-in exacto `PAM_AUTOMATION_ALLOW_SINGLE_REAL_REQUEST=CREATE_ONE_REQUEST_TODAY` y un límite interno de un solo intento. No se ejecuta como parte del conjunto ordinario.
- La única ejecución autorizada creó exactamente una solicitud el 10 de septiembre de 2026, con inicio `08:20 AM` normalizado como `08:20` y duración `Days = 0`, `Hours = 0`, `Minutes = 5`. PAM devolvió éxito y mostró `Successfully submitted request.`; no se intentó otra fila.
- Se guardaron localmente `img/fase12-request-form-before-submit.png` y `img/fase12-request-result-after-submit.png`, ambas excluidas de Git. Las capturas documentaron la estructura y terminología del portal sin contener credenciales, TOTP, cookies o tokens.
- No se abrió Outlook ni se envió correo. La integración real no debe repetirse para validaciones posteriores; las comprobaciones restantes de esta fase son locales y no destructivas.
- La validación final no destructiva compiló la solución en `Release | x64` con 0 errores y ejecutó 174 pruebas correctas, 0 fallidas y 0 omitidas. Solo permanece la advertencia heredada `MSTEST0032`, fuera del alcance de esta fase.

## 10 de septiembre de 2026 — Fase 13: lectura y comparación de Pending

- El usuario autorizó continuar con el agente principal, sin subagentes, y hasta dos pruebas reales si eran necesarias.
- Se agregaron contratos separados de solo lectura, modelos redactados, errores estables y `PamPendingService` para leer y comparar solicitudes `Pending` sin persistir Request ID.
- Browser navega a `Requests`, selecciona exactamente `Status = Pending`, admite el estado vacío, recorre hasta 500 páginas, rechaza Request ID inválidos o duplicados y reintenta un snapshot cambiante hasta tres veces.
- La comparación exige `Request Start Date`, `System`, `Account`, `Application`, `Expires` y `Description`. Coincidencias múltiples se rechazan como ambiguas y faltantes se conservan explícitamente.
- Se añadió una espera acotada de hasta seis lecturas separadas por dos segundos para la consistencia eventual posterior a una creación.
- La primera prueba real abrió Edge visible y confirmó `Requests`, `Status = Pending`, `0 items`, `There are no records to display.` y el paginador vacío. No creó ni modificó solicitudes.
- Una prueba inicial limitada recibió `ApprovedAccessOverlap` y no creó solicitudes. Después el usuario amplió la autorización para solicitar accesos con hora actual del sistema y duración de 5 minutos.
- Una prueba creó una solicitud de 5 minutos con inicio ajustado al siguiente minuto. La lectura inmediata confirmó una fila, un Request ID numérico, `Response = Pending`, el formato inglés de `Request Start Date` y todos los identificadores DOM de columna; la reconciliación estricta detectó una diferencia aún no modelada en `Application`.
- Dos bloques posteriores de tres intentos terminaron únicamente en `ApprovedAccessOverlap`; crearon cero solicitudes adicionales. No se siguió ampliando el alcance.
- Siguiendo la corrección del usuario se probaron el 10 de septiembre de 2026 solicitudes de `05:05 PM` a `05:10 PM`. La primera ejecución creó una fila; un intento de recuperación defectuoso no la reconoció y creó una segunda fila idéntica autorizada. Se detuvieron inmediatamente las mutaciones y no se creó una tercera solicitud.
- Una lectura posterior exclusivamente no destructiva confirmó que `System` y `Account` coincidían de forma única y que Pending representa `Application` como `nombre solicitado - destino`. La regla final valida el nombre completo solicitado y permite únicamente ese sufijo estructurado; no usa familias ni deriva valores desde `System`.
- La prueba final fue idempotente: reconoció individualmente una solicitud autorizada existente, validó `Request Start Date = 05:05 PM`, `Expires = 05:10 PM`, `Description`, `Application`, `System`, `Account` y `Response = Pending`, e informó cero solicitudes nuevas.
- Se conservaron tres capturas locales ignoradas por Git: `img/fase13-pending-grid.png`, `img/fase13-pending-grid-description-and-expiration.png` e `img/fase13-pending-grid-application.png`. Pueden incluir datos operativos visibles de las filas, pero nunca credenciales, TOTP, cookies o tokens; no deben versionarse ni copiarse a documentación.
- La validación final no destructiva compiló en `Release | x64` con 0 errores y ejecutó 208 pruebas correctas. Permanece únicamente la advertencia heredada `MSTEST0032`.

## 10 de septiembre de 2026 — Fase 15: composición HTML saneada

- El usuario autorizó desarrollar la fase con el agente principal y sin subagentes. Aunque existía autorización condicionada para pruebas reales, no fueron necesarias: no se abrió PAM ni Outlook, no se crearon solicitudes y no se enviaron correos.
- Se agregó `OutlookHtmlComposer` en Core con parser XML de .NET, DTD prohibidos, límite de 8 millones de caracteres y una allowlist pequeña para texto enriquecido, listas, tablas y referencias de imagen `cid:`.
- El saneamiento elimina elementos activos, atributos de eventos, enlaces, recursos externos, atributos desconocidos y declaraciones CSS no permitidas. Los errores usan códigos estables y mensajes que no reproducen el HTML privado.
- `{{REQUEST_IDS}}` debe existir exactamente una vez como texto. Los Request ID deben ser numéricos y únicos; se insertan en el orden recibido y uno por línea mediante `<br />`.
- `OutlookMessageValidator` valida otra vez el cuerpo final antes de crear COM. Rechaza HTML alterado, contenido que aún tenga el marcador y cuerpos que no contengan todos los Request ID como tokens numéricos completos.
- Se documentó que el adaptador WPF desde `FlowDocument`, la persistencia DPAPI del editor y la materialización de adjuntos CID siguen pendientes; esta fase establece el límite seguro que deberán consumir.
- La solución compiló en `Release | x64` con 0 errores y la suite ordinaria ejecutó 225 pruebas correctas. Solo permanece la advertencia heredada `MSTEST0032`, fuera del alcance de esta fase.

## 10 de septiembre de 2026 — Fase 16: resultados en memoria e interfaz

- El usuario autorizó desarrollar la fase con el agente principal y hasta diez pruebas reales. No fue necesario abrir PAM ni crear solicitudes; la cuota consumida fue cero.
- Se introdujo `IPamAutomationExecutionService` y su implementación en Core para preparar el conteo dinámico de `Favorites`, ejecutar el modo real detrás de una confirmación de un uso, reconciliar `Pending` y devolver resultados defensivos exclusivamente en memoria.
- `PamExecutionResult` conserva modo, hora, acción final, conteos, conflictos y Request ID mediante copias defensivas; su representación textual no enumera identificadores.
- La aplicación ahora compone las implementaciones reales en `App.xaml.cs`, carga `app-settings.json` y Windows Credential Manager, y conecta comandos asíncronos de prueba y ejecución real.
- Se añadieron ventanas WPF nativas para guardar credenciales sin recuperar secretos y configurar fecha, hora de 12 horas con `AM/PM`, duración, razón y `testModeEnabled`.
- Antes de crear solicitudes, un diálogo modal muestra el conteo y la configuración exacta. Rechazarlo termina sin invocar el envío. Durante todo el flujo los controles permanecen deshabilitados.
- El panel y diálogo de resultado muestran procesadas, Request ID, conflictos y hora final. Los errores controlados mantienen sus mensajes seguros y los desconocidos se sustituyen por un texto fijo.
- Se completó `mail-settings.dat` con escritura atómica y cifrado DPAPI `CurrentUser`. Contiene remitente, destinatarios, asunto, XHTML saneado e imágenes PNG inline; las representaciones textuales permanecen redactadas.
- El editor WPF usa `RichTextBox`/`FlowDocument` y admite negrita, cursiva, subrayado, listas, tablas e imágenes pegadas. Su convertidor realiza ida y vuelta con XHTML permitido y genera referencias `cid:`; Outlook materializa cada PNG en un temporal único, lo agrega y elimina el temporal en `finally`.
- Guardar correo valida la cuenta mediante un mensaje descartado. El envío de prueba de Outlook y el correo del modo de prueba PAM son acciones distintas, con destinatarios y confirmaciones independientes según la especificación.
- Se implementó protección contra duplicados en memoria y mediante una propiedad privada de Outlook con el asunto y conjunto exacto de Request ID. Se consultan Enviados, Bandeja de salida y Borradores antes y después de enviar. El ajuste posterior del 14 de septiembre volvió la consulta no destructiva: Bandeja de salida ya no se modifica y los estados ambiguos no se marcan como confirmados.
- Los fallos parciales de envío PAM conservan el conteo procesado y la interfaz lo presenta sin identidades ni detalles sensibles, indicando revisar `Pending` antes de reintentar.
- La compilación final en `Release | x64` tuvo 0 errores y la suite ordinaria ejecutó 243 pruebas correctas. No se abrió PAM ni Outlook, no se creó ninguna solicitud y no se envió correo; se consumieron cero de las diez pruebas reales autorizadas. Computer Use no expuso superficies nativas en esta sesión, por lo que la revisión visual final queda para el usuario en Visual Studio.

## 10 de septiembre de 2026 — Fase 16.5: paridad funcional y claridad de configuración

- Se inspeccionó en Edge visible la aplicación JavaScript anterior disponible en `http://127.0.0.1:4317/` y se contrastaron inicio, credenciales, solicitud y correo con las capturas locales aportadas por el usuario. No se guardaron datos ni se activaron acciones externas durante la inspección.
- La aplicación anterior quedó como referencia de claridad y paridad funcional, no como dependencia ni objetivo de copia exacta. La portada WPF existente se conservó y el trabajo se concentró en las tres ventanas de configuración.
- `Configurar credenciales` pasó a presentar tarjetas separadas para `Credenciales del PAM` y `Cuenta de Outlook`, estados configurado/no configurado, ayuda contextual, revelado temporal de los secretos recién introducidos y resultados dentro de la propia ventana.
- `Guardar cambios y probar acceso` actualiza Windows Credential Manager y realiza un login PAM visible no destructivo en una sesión temporal. `Guardar cambios y verificar Outlook` comprueba la cuenta exacta antes de persistirla; el correo de prueba continúa separado, exige confirmación y solo utiliza la misma dirección como remitente y destinatario.
- Se creó `outlook-account.dat` para desacoplar la cuenta de Outlook de la plantilla. El documento tiene esquema, cifrado DPAPI `CurrentUser` y escritura atómica; al iniciar, una instalación con remitente antiguo en `mail-settings.dat` lo migra automáticamente.
- La solicitud ahora muestra fecha completa en español, campo `hh:mm` con `AM/PM`, bloques verticales para duración y razón, modo de prueba secundario y errores dentro del formulario.
- El editor de correo conserva formato, listas, tablas e imágenes, muestra la cuenta remitente como solo lectura, explica Outlook clásico y la separación de destinatarios, e incorpora `Insertar {{REQUEST_IDS}}` con conteo y validación visibles.
- La validación final se realizó con el SDK 10.0.401 y runtime x64 10.0.12. El muxer x64 canónico `C:\Program Files\dotnet\dotnet.exe` no estaba presente, por lo que se ejecutó `MSBuild.dll` con el host x64 instalado por Visual Studio y el ejecutable x64 del runner MSTest, sin modificar el sistema.
- La solución compiló sin errores y la suite ordinaria ejecutó 253 pruebas correctas, incluida una prueba STA que carga conjuntamente las tres ventanas de configuración. No se abrió PAM ni Outlook, no se enviaron correos y no se crearon solicitudes.

## 11 de septiembre de 2026 — Fase 16.6: logging seguro de desarrollo

- El usuario solicitó logging local en la raíz del repositorio, con retención de dos días y cobertura suficiente para depurar operaciones importantes, sin subagentes.
- Se implementó un proveedor basado en `Microsoft.Extensions.Logging` que solo se activa con depurador conectado o `PAM_AUTOMATION_DEVELOPMENT_LOGGING=1` y exige encontrar `.git` y `PamAutomation.slnx` antes de crear `logs`.
- Los eventos cubren ciclo de vida WPF, inicialización, configuración, ejecución y etapas COM de Outlook. Las excepciones conservan tipo, `HRESULT` y stack trace, pero no su mensaje; tampoco se registran direcciones, credenciales, TOTP, cookies, tokens, Request ID ni contenido del correo.
- Los archivos diarios `pam-automation-AAAA-MM-DD.log` se eliminan automáticamente cuando superan dos días. La publicación normal conserva un logger nulo y no genera archivos operativos.

## 11 de septiembre de 2026 — Fase 16.7: corrección de activación COM de Outlook

- Una prueba manual del usuario reprodujo el rechazo de configuración y el nuevo log ubicó el fallo en `ComApplicationCreate`: `FileNotFoundException`, `HRESULT 0x80070002`.
- Se confirmó que `Microsoft.Office.Interop.Outlook.dll` exigía `office, Version=15.0.0.0`, ausente de la salida y de `PamAutomation.App.deps.json`; no era un problema del SMTP, la cuenta, la sesión de Outlook ni DPAPI.
- Se retiró la PIA administrada y `OutlookComApplicationFactory` pasó a activar `Outlook.Application` mediante `ProgID` y despacho COM tardío nativo de C#. Se mantuvieron STA, interfaces internas, resolución exacta, carpetas, huellas, adjuntos CID y liberación explícita.
- Las salidas Debug y Release ya no contienen ni declaran `Microsoft.Office.Interop.Outlook`, `office.dll` o `stdole.dll`. La solución compiló y las 257 pruebas ordinarias finalizaron correctamente; no se abrió Outlook, no se envió correo, no se contactó PAM y no se crearon solicitudes.

## 11 de septiembre de 2026 — Fase 16.8: retroalimentación visual de credenciales y Outlook

- El usuario confirmó manualmente que la cuenta Outlook ya se verifica y que el correo de prueba llega correctamente, pero señaló que `SubmittedUnconfirmed` generaba dudas y que faltaba una señal visible durante las esperas.
- Las comprobaciones de PAM y Outlook ahora muestran `Solicitud recibida`, un indicador circular animado y controles deshabilitados mientras trabajan. El indicador se oculta al mostrar el resultado o una cancelación.
- El correo de prueba muestra `El correo de prueba se envió con éxito` cuando COM acepta `Send()`. `NotConfirmed` y cualquier estado no exitoso se presentan como fallo, no como panel verde.
- La primera prueba manual al abrir `Editar credenciales` reveló una excepción `Storyboard.ResolveTargetName`: el template intentaba iniciar al cargarse una animación dirigida al nombre `SpinnerRotation`, incluso con el indicador oculto.
- Se eliminó el disparador automático del template. Cada comprobación crea e inicia directamente su `RotateTransform` y lo detiene al presentar éxito, error o cancelación; abrir la ventana no mantiene animaciones activas.
- El límite asíncrono de la ventana captura fallos inesperados, registra la etapa, presenta un mensaje seguro y restaura los controles en el `Dispatcher`. Los fallos al iniciar o detener el indicador también quedan contenidos y registrados.
- El manejador global del `Dispatcher` conserva el diagnóstico y, ante una excepción WPF que escape de sus límites locales, muestra un aviso seguro y cierra ordenadamente la aplicación; las tareas no observadas se marcan como observadas después de registrarse.
- La solución compiló en `Release | x64` y las 260 pruebas ordinarias finalizaron correctamente. Las nuevas pruebas renderizan la ventana, verifican el ciclo completo del indicador y fuerzan un fallo inesperado sin abrir PAM ni Outlook, crear solicitudes o enviar correos.

## 11 de septiembre de 2026 — Fase 17: diagnóstico seguro exportable

- Se incorporó `RuntimeDiagnosticLoggerFactory`, que conserva únicamente los últimos 500 eventos seguros en memoria y continúa enviándolos al archivo de desarrollo de dos días cuando ese destino está explícitamente habilitado.
- El buffer guarda plantillas de mensaje, propiedades de estado o conteo permitidas y metadatos de excepción sin mensaje ni rutas de código. No recibe modelos de credenciales, configuración de correo ni resultados con valores de Request ID.
- `DiagnosticExportService` crea bajo acción explícita un ZIP con manifiesto, entorno, resumen reducido y log. Usa un temporal junto al destino, movimiento atómico y limpieza en `finally`; cancelar el selector no escribe nada.
- La portada añadió `Exportar diagnóstico`. Los fallos recuperables recomiendan usarlo y la barrera WPF fatal ofrece exportar el contexto antes de cerrar ordenadamente, sin crear archivos si el usuario responde que no.
- La compilación `Release | x64` finalizó con 0 errores y las 271 pruebas ordinarias fueron correctas. Las pruebas cubrieron seguridad del contenido, capacidad, cancelación, fallo, limpieza, restauración del comando y renderizado WPF sin contactar PAM u Outlook, crear solicitudes ni enviar correo.

## 11 de septiembre de 2026 — Fase 18: publicación portable y actualización informativa

- El usuario sustituyó el instalador MSI/MSIX por distribución manual de un ejecutable portable. Cada usuario elegirá su ubicación, descargará una nueva versión cuando corresponda y eliminará manualmente el ejecutable anterior.
- `PamAutomation.App` quedó versionado como `0.0.1`; Windows muestra producto, descripción y versión, y la interfaz usa el título `PAM Automation V. 0.0.1`.
- Se añadió `Portable-x64.pubxml`: `Release`, `win-x64`, self-contained, single-file, trimming/AOT desactivados y símbolos excluidos. La salida final contiene solo `PAM Automation.exe`, pesa 247.748.746 bytes y tiene SHA-256 `D6F001DA9D7F464BE21AE8B02BBF4EB3757A61662D738FADA3F248939BD3BE61`.
- El ejecutable se abrió desde `artifacts/publish/final/PAM-Automation-0.0.1`, mostró la versión correcta, permaneció respondiendo y extrajo en `%TEMP%` el driver Windows de Playwright incluido en el bundle. No generó archivos junto al ejecutable.
- Core incorporó el contrato de consulta; Infrastructure valida HTTPS, tamaño máximo, JSON, versión y repositorio; WPF consulta de forma asíncrona y muestra un banner descartable únicamente si la versión es superior. No hay descarga, reemplazo ni borrado automáticos.
- El `latest.json` público se visualizó sin autenticación y contiene `0.0.1`, pero su descarga redirige a `drive.usercontent.google.com`. Fortinet bloquea expresamente ese host y .NET recibe `PartialChain`; también falla el botón Descargar del navegador. Se conservó TLS estricto y la fase queda pendiente únicamente de sustituir el origen por un endpoint HTTPS permitido.
- La solución compiló en `Release | x64` y la suite ordinaria quedó ampliada a 280 pruebas. La integración real fallida fue de solo lectura; no se abrió PAM ni Outlook, no se crearon solicitudes y no se enviaron correos.

## 11 de septiembre de 2026 — Fase 18: migración del manifiesto a GitHub

- El usuario creó el repositorio público `OmarTellez1/Pam-automation-installer`. La aplicación dejó de depender de Google Drive y pasó a consultar `https://raw.githubusercontent.com/OmarTellez1/Pam-automation-installer/main/latest.json`.
- `GoogleDriveApplicationUpdateService` se generalizó como `PublicManifestApplicationUpdateService`; conserva HTTPS obligatorio, timeout, cancelación, límite de 16 KiB, JSON estricto, versión numérica y coincidencia exacta del repositorio.
- La descarga pública del JSON funciona desde la red corporativa y con TLS válido. La integración todavía rechaza el contenido porque `repositoryUrl` conserva la dirección de Drive; el rechazo confirma la defensa prevista y la única acción externa pendiente es cambiar ese campo por `https://github.com/OmarTellez1/Pam-automation-installer`.
- Se documentó el flujo de release: incrementar metadatos, restaurar, compilar, probar, publicar con `Portable-x64`, adjuntar el ejecutable a un GitHub Release y actualizar `latest.json` al final.
- El usuario corrigió el manifiesto público. La integración explícita de solo lectura pasó 1/1 en 825 ms y confirmó HTTPS, TLS, JSON, versión y repositorio sin contactar PAM ni Outlook.
- Se regeneró la salida canónica `artifacts/publish/final/PAM-Automation-0.0.1/PAM Automation.exe`: es el único archivo de la carpeta, pesa 247.748.746 bytes y tiene SHA-256 `DBFD6226B3EC31CB6290C65C67B0B35D5ECE1F5BAFAF1D2168AC9484EE835D71`. Con esta evidencia la fase 18 quedó completada.

## 11 de septiembre de 2026 — Fase 18.5: interfaz compacta y hora separada

- La ventana principal pasó a iniciar maximizada y se redujeron botones, tarjetas, tipografías, márgenes y anchos del contenido para aprovechar mejor pantallas de trabajo.
- `CredentialsWindow`, `RequestSettingsWindow` y `MailSettingsWindow` recibieron dimensiones iniciales y mínimas más pequeñas. La barra de formato del correo usa un panel adaptable para envolver acciones cuando no caben horizontalmente.
- `Hora de inicio` dejó de ser un único campo `hh:mm`: `hh` es un desplegable de `01` a `12`, `mm` acepta hasta dos dígitos y se valida entre `0` y `59`, y `AM/PM` permanece separado. `RequestStartTimeInput` convierte la selección al formato de 24 horas.
- La compilación `Release | x64`, 14 pruebas focalizadas y las 290 pruebas ordinarias pasaron. No se abrió PAM ni Outlook, no se crearon solicitudes y no se enviaron correos.

## 11 de septiembre de 2026 — Fase 18.6: firma enriquecida y ventana de correo compacta

- La ventana de correo pasó a `700×620`, redujo su mínimo a `560×500` y compactó márgenes, avisos, editor y botones sin retirar scroll ni la barra adaptable.
- La aplicación anterior demostró que el caso de firma dependía de interceptar HTML y sus imágenes. La versión WPF ahora prioriza RTF de Outlook y, si solo existe CF_HTML, usa `RichClipboardHtmlImporter` con límites de tamaño, nodos y profundidad.
- Se añadió `HtmlAgilityPack` 1.13.0 como parser tolerante de 1,37 MB; el saneamiento propio sigue siendo la única autoridad. Se conservan estilos visuales seguros, enlaces `mailto:`/HTTPS, tablas e imágenes PNG/CID; se eliminan scripts, protocolos peligrosos y recursos remotos.
- `FlowDocumentHtmlConverter` conserva estilos, enlaces y dimensiones de imágenes durante la ida y vuelta, por lo que la firma no se pierde al guardar y reabrir la configuración DPAPI.
- La compilación `Release | x64`, 25 pruebas focalizadas y las 294 pruebas ordinarias pasaron. No se abrió Outlook, no se usó el portapapeles real, no se enviaron correos y no se contactó PAM.

## 11 de septiembre de 2026 — Fase 18.7: tablas pegadas y editor simplificado

- La aplicación JavaScript se revisó como referencia de lectura: su editor no ofrece una barra de construcción y al pegar prioriza `text/html`. La versión WPF adoptó esa misma interacción sin copiar su implementación ni relajar el saneamiento.
- Se retiraron las acciones de negrita, cursiva, subrayado, lista, creación de tabla e imagen. Solo permanece la inserción explícita de `{{REQUEST_IDS}}`; imágenes, tablas y formato se incorporan al pegar el contenido compuesto externamente.
- La conversión de tablas ahora conserva anchos de `colgroup`, `rowspan`, `colspan`, padding y bordes básicos. También crea un párrafo posterior cuando una tabla es el último bloque, permitiendo continuar escribiendo fuera de ella.
- La compilación `Release | x64` terminó con 0 errores y la suite ordinaria pasó 295/295. No se abrió Outlook, no se utilizó el portapapeles real, no se enviaron correos y no se contactó PAM.
- La primera corrección probó conversión y persistencia, pero asumía que WPF siempre entregaría `DataFormats.Html` como `string`; esa suposición no equivale a `clipboardData.getData("text/html")` del navegador y no cubría `text/html` ni streams. Se añadió normalización explícita de formatos y representaciones, logging seguro del recorrido y una prueba que carga el paquete resultante en el cursor real de un `RichTextBox`. La suite quedó en 298/298.

## 11 de septiembre de 2026 — Fase 18.8: tabla manual estable

- Se decidió conservar el pegado enriquecido para texto y firma, pero dejar de depender de tablas externas porque WPF no reproduce consistentemente todos los formatos que generan Outlook, navegadores y otras aplicaciones.
- `Editar envío de correo` muestra una advertencia y recupera una única acción `Insertar tabla 2×2`. La tabla controlada contiene las filas de solicitudes y motivo, columnas 30/70 y el marcador único.
- `FlowDocumentTableInserter` divide el párrafo en el cursor, inserta la tabla entre ambas partes, mantiene una línea posterior y evita anidarla si la acción se vuelve a pulsar con el foco dentro de ella.
- Una primera prueba reveló que modificar un `Run` mientras se enumeraba su `InlineCollection` invalida el enumerador; se corrigió trabajando sobre snapshots antes de modificar el marcador. La compilación quedó limpia y la suite pasó 301/301.

## 11 de septiembre de 2026 — Fase 18.9: reconciliación de Application observada

- El diagnóstico exportado de la primera ejecución completa confirmó 13 envíos correctos y un fallo posterior dentro de `PamPendingService.Compare`; el correo no llegó a ejecutarse.
- Las capturas aportadas demostraron coincidencia exacta de inicio `05:30 PM`, expiración `06:30 PM`, `Description = Pruebas`, `System` y `Account`. La diferencia estaba en la representación de `Application` entre Favorites y Pending.
- La comparación incorporó entonces familias controladas para `NOSQLBOOSTER`, `PLSQL`, `PuTTY` y `WinSCP`, sin derivarlas desde `System` ni utilizar `Ticket`. Esta decisión histórica fue sustituida íntegramente por la reconciliación genérica de fase 24.
- Se reprodujeron los nombres observados, incluido un mismo `System + Account` con PuTTY y WinSCP. La compilación `Release | x64` pasó y la suite terminó 307/307 sin contactar PAM, crear solicitudes ni enviar correo.

## 11 de septiembre de 2026 — Fase 18.10: aviso de inicio bloqueado

- Se confirmó que una solicitud con fecha u hora pasada deshabilitaba correctamente `Iniciar automatización`, pero el texto operativo podía seguir mostrando un estado ajeno, como `Configuración de credenciales sin cambios`.
- La ventana principal incorporó un aviso ámbar separado que explica la primera causa pendiente y señala la sección que debe corregirse. Los casos de fecha y hora vencidas tienen textos específicos.
- El aviso se actualiza al cargar o cambiar el estado de configuración y se oculta durante operaciones o cuando el inicio vuelve a estar habilitado.
- La compilación `Release | x64` pasó y la suite terminó 309/309 sin contactar PAM, crear solicitudes ni enviar correo.

## 11 de septiembre de 2026 — Fase 18.11: resultado y correo sin estados ambiguos

- El resultado ya no abre un `MessageBox` dentro de `PresentResult`, antes de que el bloque `finally` terminara de restaurar `IsRunning`. Ahora se conserva en memoria y se consulta con el botón `Ver resultado de la automatización` cuando la interfaz ya está inactiva.
- La tarjeta persistente con resumen e IDs se sustituyó por ese único botón para evitar duplicar el contenido de la ventana de detalle.
- `SubmittedUnconfirmed` conserva su clasificación interna, pero muestra `El correo se envió correctamente desde Outlook.` cuando COM aceptó `Send()` sin error. Los estados de borrador, duplicado o fallo mantienen sus mensajes específicos.
- El cuerpo final aplica Aptos 11 con respaldos compatibles y respeta estilos explícitos internos. Las tablas pasan de 100% a 85%, centradas y con contenido alineado arriba.
- La compilación `Release | x64` pasó y la suite terminó 309/309 sin abrir Outlook, contactar PAM, crear solicitudes ni enviar correo.

## 11 de septiembre de 2026 — Fase 21: acceso interactivo persistente

- Se añadió `Acceder a PAM` como acción independiente de la automatización de solicitudes. Solo necesita credenciales PAM configuradas y reutiliza el login real con reloj HTTP y TOTP.
- Tras aclarar que el navegador debía ser normal y sobrevivir al cierre de la aplicación, se descartó la primera implementación parcial basada en una sesión temporal propiedad del ViewModel.
- La excepción nueva de `AGENTS.md` se aisló en `PamInteractiveBrowserSessionFactory`: perfil persistente dedicado, Edge iniciado como proceso externo y conexión CDP solamente en loopback durante la autenticación.
- `PamInteractiveAccessService` separa Playwright después de confirmar el dashboard sin cerrar el navegador. El flujo productivo de solicitudes continúa usando `PamBrowserSessionFactory`, contexto temporal, bloqueo de orígenes externos y disposición completa.
- Una sesión válida ya existente evita reenviar credenciales o TOTP. Los fallos de autenticación se presentan con los mensajes seguros existentes y nunca registran secretos.
- La compilación `Release | x64` terminó con 0 errores y 0 advertencias; la suite pasó 315/315. No se realizó un login PAM real ni se ejecutaron solicitudes o correos.

## 12 de septiembre de 2026 — Corrección de fase 21: certificado del perfil interactivo

- El diagnóstico exportado después de dos intentos confirmó que el navegador y la conexión CDP estaban disponibles, pero el primer `GotoAsync` fallaba antes de entregar usuario, contraseña o TOTP.
- Se contrastó en modo de solo lectura `C:\pam automatizacion\ABRIR_SESION_PAM.cmd` y su recorrido `scripts\open-manual-session.ps1`. La aplicación anterior usa el perfil habitual de Chrome, donde la confianza del certificado interno ya estaba resuelta; la nueva fase 21 partía de un perfil Edge dedicado limpio.
- El flujo interactivo ahora crea una sesión CDP de seguridad para la página, ignora el certificado exclusivamente durante la autenticación PAM, restaura la validación normal y separa Playwright sin cerrar Edge. Ante un fallo también intenta restaurar la validación antes de liberar recursos.
- Se añadieron logs de las etapas de conexión y excepción temporal sin registrar puerto, ruta, URL, credenciales ni TOTP. Una regresión verifica que la preparación segura ocurre antes de liberar o cerrar recursos.
- La compilación `Release | x64` pasó con la advertencia heredada `MSTEST0032`; la suite terminó 316/316. No se contactó PAM durante esta corrección.

## 12 de septiembre de 2026 — Fase 21.1: WebSocket CDP y preflight de Pending

- Un segundo diagnóstico mostró tres fallos `TimeoutException` dentro de `ConnectOverCDPAsync`: Edge sí había iniciado, pero la conexión no superaba el endpoint HTTP de descubrimiento.
- `PamInteractiveBrowserSessionFactory` ahora lee y valida puerto y ruta desde `DevToolsActivePort` y conecta al WebSocket exacto sobre `127.0.0.1`. Los timeouts transitorios participan en los reintentos controlados.
- La integración real autorizada inició sesión, confirmó el dashboard y terminó dejando abierto el Edge dedicado. No creó solicitudes ni envió correo.
- La ejecución real incorporó un preflight de `Pending`. Su decisión original permitía repetir o cancelar; la política posterior bloquea crear solicitudes idénticas y solo permite reutilizar un conjunto completo para correo o cancelar.
- Los Request ID preexistentes se conservan solo en memoria y se excluyen de la reconciliación posterior. Esto permite identificar las nuevas filas cuando PAM admite varias solicitudes Pending iguales, sin confundirlas con las anteriores.
- La compilación `Release | x64` pasó y la suite ordinaria terminó 323/323; solo permanece `MSTEST0032`, heredada y ajena al cambio.

## 12 de septiembre de 2026 — Fase 21.2: Edge normal y confianza manual

- La comprobación del usuario aclaró que el Edge dedicado no debía considerarse un navegador normal: una descarga podía quedar asociada al entorno controlado y la conexión CDP seguía siendo un punto de fallo.
- Se volvió a contrastar el recorrido de solo lectura de `C:\pam automatizacion\scripts\open-manual-session.ps1` y se trasladó su técnica de accesibilidad a C#; no se modificó la aplicación anterior ni se incorporó PowerShell a producción.
- `Acceder a PAM` ahora abre el perfil habitual de Microsoft Edge sin `userDataDir`, CDP ni depuración remota. Windows UI Automation limita el control a ventanas `msedge`, al origen exacto de PAM y a los controles accesibles del login.
- Si Edge presenta el certificado interno, la aplicación muestra instrucciones y espera que el usuario elija manualmente `Avanzado` y `Continuar`; la automatización se reanuda al aparecer `Username`. La petición auxiliar para el reloj permite el certificado exclusivamente en el origen autorizado y no altera la confianza del navegador.
- El foco de ventana es una mejora opcional: una denegación de foco de Windows ya no aborta el flujo. Timeouts, cierre de ventana y controles ausentes producen errores controlados sin registrar secretos.
- La compilación `Release | x64` pasó; la suite ordinaria terminó 321/321 y una integración real autorizada confirmó el dashboard dejando Edge abierto. No se crearon solicitudes ni se envió correo; permanece la advertencia heredada `MSTEST0032`.

## 12 de septiembre de 2026 — Fase 21.3: zoom de PAM al 67 %

- Se trasladó a C# el comportamiento validado de la aplicación anterior: tras autenticar el dashboard se ejecuta `Ctrl+0` y cuatro veces `Ctrl+-` exclusivamente sobre la ventana PAM recién abierta.
- Antes de enviar teclas se vuelve a validar el origen, se enfoca la barra de direcciones y se comprueba el elemento que recibió el foco. Si Windows no permite demostrarlo, el zoom se omite de forma controlada y la sesión permanece abierta.
- La primera integración demostró que Edge no expone necesariamente el indicador como `Button`; la implementación final reconoce `67 %` en `Text`, `Group` o `Button` sin confundir porcentajes del contenido web.
- La segunda integración real confirmó explícitamente `67 %`, conservó Edge abierto y no creó solicitudes ni envió correo.
- El acceso directo se reubicó en el encabezado, entre `Exportar diagnóstico` y `Enviar correo con Outlook`, sin cambiar su comando ni reglas de habilitación.

## 12 de septiembre de 2026 — Fase 21.4: solicitudes en segundo plano

- El usuario decidió que la navegación de la automatización de solicitudes no debe mostrarse. `PamBrowserSessionFactory` ahora lanza el canal externo `msedge` con `Headless = true`.
- El cambio cubre el contexto temporal compartido por autenticación, `Favorites`, formularios y `Pending`, sin alterar su perfil aislado, política de origen, excepción HTTPS o disposición de recursos.
- `Acceder a PAM` permanece deliberadamente visible porque utiliza la fábrica interactiva separada y el Edge normal del usuario.
- La compilación `Release | x64` y las 12 pruebas de la fábrica pasaron. Un login real autorizado confirmó el dashboard en segundo plano y cerró el contexto sin visitar `Favorites`, crear solicitudes ni abrir Outlook.

## 12 de septiembre de 2026 — Fase 21.5: confirmación antes de preparar

- La confirmación general se movió al primer momento del comando `Iniciar automatización`, usando únicamente la configuración local que ya está disponible.
- Rechazarla evita por completo `PrepareRealAsync`; por tanto, no autentica, no lee `Favorites` ni `Pending` y no crea solicitudes.
- Aceptarla inicia todas las validaciones en segundo plano y elimina la confirmación general que antes aparecía al terminar la preparación.
- El aviso de solicitudes idénticas en `Pending` permanece porque requiere una decisión explícita; la política vigente no permite repetirlas y solo aparece cuando se detectan coincidencias reales.
- La compilación `Release | x64` y las 22 pruebas de `MainWindowViewModel` pasaron.

## 12 de septiembre de 2026 — Fase 21.6: confirmación y configuración más claras

- El resumen previo convierte la hora interna de 24 horas a `hh:mm AM/PM`, conservando la elección que el usuario entiende desde `Configurar solicitud`.
- Para el envío por Outlook se sustituyó la etiqueta genérica de destinatarios por líneas independientes `Para` y `CC`; una copia vacía se comunica como `CC: Ninguno`.
- Se retiró visualmente `Habilitar modo de prueba` de `Configurar solicitud`. El valor interno existente se conserva sin cambios al guardar para no alterar silenciosamente configuraciones compatibles.
- Se añadieron regresiones de formato y una comprobación de que el control eliminado ya no forma parte del árbol WPF.
- La compilación `Release | x64` pasó y las 6 pruebas específicas terminaron correctamente; no se contactó PAM ni Outlook.

## 12 de septiembre de 2026 — Fase 21.7: avisos urgentes

- Se centralizaron todos los cuadros importantes de la aplicación en `UrgentMessageBox`.
- Windows recibe las opciones nativas `MB_TASKMODAL`, `MB_TOPMOST` y `MB_SETFOREGROUND`; el diálogo queda por encima de las ventanas normales y bloquea el flujo hasta obtener respuesta.
- Si la ventana WPF propietaria está minimizada, se restaura antes de presentar el aviso. Se prefiere la ventana secundaria activa para conservar correctamente la modalidad durante la configuración.
- Se migraron confirmación inicial, duplicados en `Pending`, certificado, correo, errores, resultados y validaciones de la plantilla. Los avisos no bloqueantes continúan dentro de la interfaz.
- La compilación `Release | x64` y 7 pruebas específicas pasaron; no se contactó PAM ni Outlook.

## 12 de septiembre de 2026 — Fase 21.8: progreso por Application y acceso concurrente

- Se agregó `PamRequestSubmissionProgress` para comunicar un resultado conocido después de procesar cada `Favorites` row, sin trasladar secretos ni detalles de autenticación.
- La portada conserva el mensaje de validación inicial hasta el primer `Submitted` y después muestra `Se solicitó {Application}.` con el nombre observado en la columna `Application`.
- Los resultados `ApprovedAccessOverlap` no se describen como solicitudes creadas.
- `Acceder a PAM` ahora puede ejecutarse mientras continúa la automatización oculta; sigue evitando aperturas interactivas duplicadas y exportaciones simultáneas.
- El acceso concurrente conserva el mensaje de progreso de la automatización incluso si su propio login termina o falla; el fallo se presenta mediante un aviso urgente separado.
- La compilación `Release | x64` y 39 pruebas específicas pasaron; no se contactó PAM ni Outlook.

## 12 de septiembre de 2026 — Fase 21.9: actualización temporal automática

- `MainWindow` inicia un `DispatcherTimer` de un minuto al cargarse y lo detiene al cerrarse.
- Cada tick solo revalida en memoria la fecha y hora de Ecuador; no abre navegador, no consulta PAM y no ejecuta Outlook.
- Cuando la hora vence, el botón real se deshabilita y el aviso visible explica que debe actualizarse `Configurar solicitud`.
- `StartAutomationAsync` incorpora una validación fresca antes de la confirmación y otra después de responder `Sí`. Si el vencimiento ocurre con el diálogo abierto, cancela antes de cualquier interacción con PAM.
- La compilación `Release | x64` y 32 pruebas específicas pasaron; no se contactó PAM ni Outlook.

## 12 de septiembre de 2026 — Fase 21.10: margen mínimo y respuesta PAM defensiva

- Se confirmó como causa operativa probable del diagnóstico que el primer formulario alcanzó `Submit Request` cuando su hora de inicio ya era demasiado cercana o había vencido.
- `RequestSettingsValidator` incorpora `StartTimeTooClose` y exige un margen mínimo de 6 minutos respecto de la hora actual de Ecuador, también al cruzar medianoche.
- La revisión de cada minuto deshabilita el botón cuando el margen deja de cumplirse y muestra una explicación accionable. Las validaciones alrededor de la confirmación y las defensas de Core aplican la misma regla.
- El clasificador de respuestas comprueba que la raíz JSON sea un objeto antes de consultar `ErrorDetails`. Las demás formas JSON se convierten en `RequestRejected`, sin filtrar el contenido ni lanzar `InvalidOperationException`.
- La compilación `Release | x64`, 51 pruebas relacionadas y las 337 pruebas ordinarias pasaron. No se contactó PAM ni Outlook.

## 12 de septiembre de 2026 — Preparación de la versión estable 1.0.0

- La versión del proyecto se elevó a `1.0.0`, con `AssemblyVersion` y `FileVersion` en `1.0.0.0`, para preparar el primer ejecutable destinado a otros equipos.
- Dos pruebas de presentación que esperaban literalmente `0.0.1` se cambiaron para obtener la versión real del ensamblado. Así continúan comprobando el título sin romperse en cada publicación posterior.
- La compilación `Release | x64` terminó sin errores y la suite ordinaria pasó con 337 de 337 pruebas.
- Se aclaró que `artifacts/publish/PAM-Automation-0.0.1` y `artifacts/publish/win-x64` son publicaciones históricas con símbolos, ignoradas por Git y prescindibles. La salida distribuible canónica se genera bajo `artifacts/publish/final/PAM-Automation-<versión>`.

## 13 de septiembre de 2026 — Fase 20.1: catálogo dinámico de Favorites

- Se añadió `Colecciones` como sección WPF propia, con estado inicial vacío, acción `Obtener solicitudes favoritas` y presentación reducida a `Application - IP Address`.
- El lector de `Favorites` captura ahora `IP Address` además de la identidad estable. Esta metadata queda excluida de la igualdad de `PamFavoriteAccount`, por lo que no altera la localización y validación histórica por `System + Application + Account`.
- `FavoriteCatalogService` reutiliza el login y recorrido paginado existentes, reemplaza el snapshot completo y calcula altas y bajas sin abrir formularios ni producir efectos PAM.
- `DpapiFavoriteCatalogStore` guarda `favorite-catalog.dat` bajo la configuración privada del usuario mediante DPAPI `CurrentUser`, `schemaVersion = 1` y escritura atómica. La existencia del snapshot cambia el botón a `Actualizar solicitudes favoritas` después de reiniciar.
- Se planificó que una fase posterior retire automáticamente de cada colección las claves exactas ausentes de `Favorites`, mientras las filas nuevas requerirán selección explícita. Antes de ejecutar una colección se releerá `Favorites` y después del envío se reconciliará `Pending`.
- La compilación `Release | x64`, 35 pruebas relacionadas y las 350 pruebas ordinarias pasaron. No se contactó PAM ni Outlook; falta validar explícitamente el selector real de `IP Address` desde la nueva interfaz.
- El diagnóstico exportado `pam-automation-diagnostic-20260913-063957` confirmó dos agotamientos de tiempo dentro de `ReadStableCurrentPageAsync`: el login y la apertura de `Colecciones` sí comenzaron, pero la fila no podía completarse al leer la nueva columna. Una prueba real de solo lectura autorizada enumeró únicamente los nombres técnicos del grid y confirmó que el portal representa `IP Address` mediante `data-column-id="SystemIP"`, no `IPAddress`; el selector quedó corregido y el error interno conserva ahora el nombre seguro de la columna ausente para futuros diagnósticos.
- Después de aplicar `SystemIP`, la prueba explícita `PamRealFavoritesTests` completó la autenticación y la lectura real de todas las páginas, verificando valores no vacíos de `System`, `Application`, `Account` e `IP Address`. No abrió formularios, no creó solicitudes y no ejecutó Outlook. La compilación `Release | x64` y las 350 pruebas ordinarias también pasaron; la fase 20.1 quedó validada.

## 13 de septiembre de 2026 — Fases 20.2 y 20.3: colecciones editables y ejecución seleccionada

- `Colecciones` muestra ahora definiciones nombradas y permite crear o editar un subconjunto no vacío del catálogo junto con fecha, hora `AM/PM`, duración y razón propias.
- Cada fila tiene un toggle exclusivo: puede haber varias colecciones, pero solo una queda seleccionada como candidata para automatización. La portada añade un segundo toggle que alterna entre esa candidata y todas las filas de `Favorites`.
- `favorite-collections.dat` persiste el estado con DPAPI `CurrentUser`, `schemaVersion = 1` y escritura atómica. La actualización de Favorites elimina claves exactas ausentes y desactiva el modo si la colección activa queda vacía.
- La ejecución con colección lee Favorites antes del preflight de Pending, filtra por `System + Application + Account` y transporta esa selección en `PamExecutionPreparation`. La autorización de un solo uso vuelve a resolver las mismas claves dentro de la sesión de envío; una ausencia o diferencia bloquea antes de crear solicitudes.
- Se añadieron pruebas de dominio, DPAPI, ViewModels, presentación WPF, confirmación del alcance y límite de envío. La compilación `Release | x64` y las 363 pruebas ordinarias pasaron. No se crearon solicitudes PAM ni se enviaron correos.

## 13 de septiembre de 2026 — Fase 20.4: automatización exclusivamente por colecciones

- Se retiraron `UseCollectionsForAutomation` y el toggle de la portada. Toda ejecución de la interfaz requiere una colección activa y usa únicamente sus claves exactas.
- `All favorites` se crea y activa en la primera obtención del catálogo. Cada actualización reemplaza automáticamente sus solicitudes con todas las filas actuales; el usuario solo puede editar su razón.
- Las colecciones dejaron de guardar fecha, hora y duración. `Editar solicitud` conserva exclusivamente esos tres valores y la razón efectiva se toma de la colección activa al validar, confirmar y ejecutar.
- `favorite-collections.dat` avanzó a `schemaVersion = 2`; la carga admite el esquema 1 y migra nombre, razón, selección y colección activa sin exponer el documento protegido.
- La compilación `Release | x64` y las 368 pruebas ordinarias pasaron. No se contactó PAM, no se crearon solicitudes y no se envió correo.

## 13 de septiembre de 2026 — Ajuste de inicio para configuración temporal vencida

- Al cargar la aplicación, `MainWindowViewModel` detecta si la fecha y hora guardadas ya pasaron en Ecuador.
- Solo en ese caso calcula y persiste la primera hora válida: hora actual más el margen obligatorio de 6 minutos, redondeada hacia arriba al minuto completo.
- Una hora futura permanece intacta. Después del arranque, el temporizador continúa deshabilitando y explicando configuraciones demasiado cercanas sin modificar la elección del usuario.
- La compilación `Release | x64` y las 369 pruebas ordinarias pasaron. No se contactó PAM, no se crearon solicitudes y no se envió correo.

## 13 de septiembre de 2026 — Icono oficial, barra de título y versión 1.0.1

- El monograma circular `PA` generado para la aplicación se guardó como PNG transparente y se convirtió a un `.ico` con nueve resoluciones entre 16 y 256 píxeles.
- El `.ico` quedó configurado como `ApplicationIcon`; Windows pudo extraerlo del ejecutable compilado. Tras aclarar la separación visual, la portada conservó su insignia azul redondeada original y cambió únicamente el texto de `P` a `PA`; no renderiza allí el gráfico circular.
- `WindowTitleBarAppearance` aplica centralmente a todas las ventanas el modo oscuro y, en Windows 11 o superior, fondo gris oscuro, borde coherente y texto claro mediante DWM. Un sistema sin esos atributos conserva el comportamiento nativo sin interrumpir la aplicación.
- Las cuatro versiones del proyecto avanzaron a `1.0.1`/`1.0.1.0`. El publicador local queda preparado para generar la carpeta canónica `PAM-Automation-1.0.1`.
- La compilación `Release | x64` y las 369 pruebas ordinarias pasaron. El ejecutable de desarrollo reportó `ProductVersion 1.0.1`, `FileVersion 1.0.1.0` y un icono Win32 extraíble.

## 14 de septiembre de 2026 — Fase 20.5: eliminación de colecciones personalizadas

- La lista de colecciones incorpora un botón rojo con el glifo de papelera de Windows únicamente para colecciones creadas por el usuario.
- La eliminación presenta una confirmación urgente y explica que la acción no altera `Favorites` en PAM.
- `FavoriteCollectionService` impide borrar `All favorites` aunque la operación se invoque sin pasar por la interfaz.
- Al borrar la colección activa se selecciona automáticamente `All favorites`; una eliminación inactiva conserva la selección actual.
- La persistencia continúa usando el documento DPAPI `schemaVersion = 2` y su escritura atómica.
- La compilación `Release | x64` y las 374 pruebas ordinarias pasaron. No se contactó PAM, no se crearon solicitudes y no se envió correo.

## 14 de septiembre de 2026 — Fase 22: optimización del arranque

- Se identificó como principal cuello de botella previo a WPF la publicación autocontenida en un único EXE de aproximadamente 248 MB con extracción total de bibliotecas nativas y contenido.
- `Portable-x64` cambió a una publicación autocontenida multifichero sin trimming ni AOT. El usuario conserva la ausencia de prerrequisitos, pero debe extraer y mantener completa la carpeta distribuida.
- `Publish-Portable.ps1` valida el EXE y sus metadatos, exige dependencias contiguas, crea `PAM-Automation-1.0.2-win-x64.zip` con carpeta base y calcula el SHA-256 del ZIP.
- La portada muestra `Preparando PAM Automation…` y bloquea acciones durante la carga local. El diagnóstico registra etapas y milisegundos, y la comprobación remota de versiones se ejecuta después del arranque funcional.
- La versión avanzó a `1.0.2`. Pasaron 44 pruebas específicas y las 377 pruebas ordinarias iniciales. El ZIP inicial contenía 523 archivos; posteriormente se regeneró con la recuperación de Outlook descrita debajo.
- La prueba local visible mostró la ventana 1.0.2 receptiva en aproximadamente 4,9 segundos. La comparación 1.0.1 de archivo único continuó sin ventana durante el mismo intervalo y se cerró sin realizar acciones externas. No se abrió PAM ni Outlook y no se crearon solicitudes.

## 14 de septiembre de 2026 — Recuperación de Outlook `0x80080005`

- El diagnóstico de otro equipo confirmó que PAM, credenciales y Favorites funcionaban, pero dos verificaciones de Outlook fallaron antes de enumerar cuentas: `Activator.CreateInstance` agotó unos 31 segundos y devolvió `CO_E_SERVER_EXEC_FAILURE` en `ComApplicationCreate`.
- Se descartó hardcodear `C:\ProgramData\Microsoft\Windows\Start Menu\Programs`: contiene accesos directos y el `ProgID` ya estaba registrado, por lo que no atacaba la causa observada.
- `OutlookComApplicationFactory` consulta primero la Running Object Table. Solo ante `0x80080005` inicia `outlook.exe` mediante ShellExecute, espera tres segundos en su STA dedicado y reintenta una vez mediante instancia activa o activación COM.
- La recuperación no finaliza Outlook, no usa una ruta fija y no cambia permisos. Si falla, `ApplicationUnavailable` llega a la interfaz como instrucciones para reiniciar Outlook completamente y ejecutar ambos programas con el mismo nivel de integridad.
- La compilación `Release | x64`, 30 pruebas específicas y las 383 pruebas ordinarias pasaron. No se ejecutó una prueba real de Outlook ni se envió correo.
- El paquete final `PAM-Automation-1.0.2-win-x64.zip` pesa 105.062.496 bytes y su SHA-256 es `5D141DD2EDDD0C5DB67F497819B39443ADD067BB0C32A0C2951383693E10758D`.

## 14 de septiembre de 2026 — Confirmación tolerante y no destructiva de Outlook

- El diagnóstico `pam-automation-diagnostic-20260914-211847` confirmó que las 14 solicitudes y sus Request ID terminaron correctamente y que Outlook aceptó `Send()`.
- La comprobación posterior comenzó demasiado pronto: encontró el mensaje todavía en Bandeja de salida y la lógica anterior lo movió a Borradores, interrumpiendo el procesamiento normal de Outlook.
- La aplicación ahora espera cuatro segundos antes de cada observación y admite hasta diez lecturas. Un fallo transitorio de COM durante esas lecturas se reintenta de forma segura porque la operación es únicamente de consulta.
- Bandeja de salida se conserva intacta y se comunica como correo pendiente; tampoco se crea un duplicado si una ejecución posterior encuentra allí la misma huella.
- El envío no se repite automáticamente porque un fallo posterior a `Send()` no permite demostrar con seguridad que Outlook no recibió el mensaje.
- La compilación `Release | x64`, 26 pruebas específicas iniciales y las 387 pruebas ordinarias finales pasaron. No se abrió Outlook real ni se envió correo.

## 15 de septiembre de 2026 — Redefinición de la fase 23: usuario principal y cuentas PAM

- Se descartó el diseño de perfiles completos, configuración aislada e importación/exportación portable. La rama `mejorarRendimiento`, que conserva la recuperación y confirmación tolerante de Outlook, se eligió como base limpia.
- La aplicación seguirá teniendo una sola configuración de solicitudes, Favorites, colecciones, Outlook y correo. Esa configuración y la automatización real pertenecen siempre al usuario principal.
- En el primer inicio será obligatorio elegir el nombre del principal. Su rol no podrá eliminarse, cambiarse ni transferirse; su nombre visible sí será editable.
- La portada incorporará `Usuarios PAM` para administrar el principal y cuentas secundarias. Cada secundaria tendrá nombre descriptivo y credenciales propias protegidas, pero solo podrá usarse con `Acceder a PAM`.
- Al abrir PAM se elegirá la identidad por nombre. La selección no cambiará el propietario de la aplicación ni permitirá crear solicitudes con credenciales secundarias.
- Se documentó la fase como cuatro entregas: identidad principal, administración y persistencia de cuentas, selección interactiva, y migración/aceptación. En este paso no se modificó código ni configuración local.

## 15 de septiembre de 2026 — Fases 23.1 y 23.2: identidad principal y administración de cuentas PAM

- Se añadió un registro de identidades no sensible, protegido con DPAPI CurrentUser, esquema explícito y escritura atómica en `pam-accounts.dat`.
- El primer inicio ahora exige crear el nombre del usuario principal antes de mostrar la portada. Cancelar el alta cierra la aplicación; una instalación existente conserva sin cambios sus credenciales, configuración, correo y colecciones.
- La portada incorpora `Usuarios PAM`. El principal puede renombrarse, pero no eliminarse ni cambiar de rol. Se pueden crear, editar y eliminar hasta cuatro cuentas secundarias, para un máximo total de cinco identidades.
- Las credenciales principales continúan en `PamAutomation/PamCredentials/v1`. Cada secundaria utiliza un destino independiente derivado de su GUID; la interfaz nunca vuelve a mostrar secretos y conserva los campos vacíos durante una edición.
- `PamAccountService` serializa mutaciones, valida las semillas TOTP y aplica rollback entre el índice y Credential Manager cuando una operación falla.
- La fase 23.3 permanece pendiente: `Acceder a PAM` todavía usa el principal y aún no muestra el selector de identidad.
- La compilación `Release | x64` y las 403 pruebas ordinarias pasaron. No se abrió PAM ni Outlook, no se crearon solicitudes y no se envió correo.

## 17 de septiembre de 2026 — Fase 23.3: selección de identidad para Acceder a PAM

- Cada pulsación de `Acceder a PAM` abre un selector modal con el principal y las cuentas secundarias, incluso cuando solo hay una identidad.
- Las identidades sin credenciales completas permanecen visibles como pendientes, pero no pueden confirmarse. Cancelar no recupera secretos ni abre Edge.
- Tras confirmar, `PamAccountService` carga exclusivamente la credencial vinculada al GUID elegido y el ViewModel la entrega al flujo interactivo existente. Los logs registran únicamente GUID y rol, nunca nombre técnico, contraseña, semilla o TOTP.
- La disponibilidad de `Acceder a PAM` depende de que al menos una identidad tenga credenciales. Se recalcula al iniciar, editar las credenciales principales y cerrar `Usuarios PAM`.
- `Iniciar automatización` conserva el almacén principal original y no consulta la identidad seleccionada para acceso interactivo. Una prueba específica demuestra este límite.
- La compilación `Release | x64` y las 407 pruebas ordinarias pasaron. No se abrió PAM ni Outlook, no se crearon solicitudes y no se envió correo.

## 17 de septiembre de 2026 — Fase 23.4: migración, seguridad y aceptación

- El alta inicial se extrajo a `PrincipalIdentityBootstrapper` para probar y mantener explícita su idempotencia. Una instalación con principal no vuelve a preguntar ni escribir; una instalación nueva crea solo su identidad; cancelar no cambia el estado.
- Una carrera de creación reconoce `PrincipalAlreadyExists` como estado listo, sin reemplazar el principal ni reinterpretar la configuración.
- Se verificó que el destino histórico `PamAutomation/PamCredentials/v1` continúa perteneciendo al principal y que crear el registro no mueve ni sobrescribe sus credenciales.
- El registro DPAPI manipulado y un `schemaVersion` futuro se rechazan de forma controlada, conservando el archivo existente y evitando migraciones implícitas.
- Los eventos de administración y resolución de cuentas se validaron contra el diagnóstico en memoria: usuario técnico, contraseña y semilla TOTP no aparecen en plantillas ni propiedades exportables.
- Las pruebas WPF cargan la administración, el primer inicio, la edición secundaria y el selector interactivo. No se realizó acceso real a PAM, no se crearon solicitudes y no se abrió Outlook.
- La compilación `Release | x64` y las 414 pruebas ordinarias pasaron. La fase 23 queda completada.

## 17 de septiembre de 2026 — Ajuste de Usuarios PAM y cabecera principal

- El diagnóstico local identificó que `PamAccountsWindow` cerraba la aplicación porque `Run.Text` usa enlace bidireccional por defecto e intentaba escribir en `RoleText` y `CredentialText`, propiedades calculadas de solo lectura.
- La plantilla declara ahora ambos enlaces como `OneWay`; la prueba WPF muestra y renderiza la ventana con una cuenta principal para cubrir el recorrido que produjo la excepción.
- La cabecera muestra únicamente `Acceder a PAM`, `Usuarios PAM` y una tarjeta con la inicial y el nombre del principal. El título visible pasa a ser `PAM Automation`, sin incluir la versión.
- `Exportar diagnóstico` y la acción final `Mostrar IDs al final`/envío por Outlook se trasladaron sobre las tarjetas de configuración.
- Se conserva la administración acordada: el principal puede renombrarse pero no eliminarse; los secundarios pueden crearse, renombrarse, actualizar sus credenciales o eliminarse con confirmación.
- La compilación `Release | x64`, 63 pruebas relacionadas y la suite completa de 414 pruebas pasaron sin abrir PAM ni Outlook ni usar secretos reales.

## 17 de septiembre de 2026 — Ajustes visuales de usuarios y diálogo Acerca de

- Las tarjetas del principal y de las cuentas secundarias ahora ocupan uniformemente todo el ancho disponible en `Usuarios PAM`; se deshabilitó el desplazamiento horizontal innecesario.
- La esquina inferior izquierda de la portada incorpora un botón circular de información accesible mediante teclado y automatización.
- El nuevo diálogo `Acerca de PAM Automation` muestra la versión de tres componentes leída del ensamblado (`1.0.2`), el autor `Hitss Omar Téllez` y un botón de cierre.
- La presentación del diálogo permanece detrás de `IConfigurationDialogService`, sin incorporar decisiones de UI al dominio o a la infraestructura.
- La compilación `Release | x64`, 43 pruebas específicas de presentación/ViewModel y la suite completa de 414 pruebas pasaron.

## 17 de septiembre de 2026 — Simplificación del estado central

- Se retiró de la portada el indicador permanente de colección activa y se conservó únicamente el aviso contextual que explica por qué no puede iniciarse la automatización.
- El texto gris de estado se oculta mientras la aplicación está en reposo y aparece únicamente durante la ejecución para mostrar validación de Favorites/Pending y solicitudes creadas.
- Se eliminó visualmente la repetición de mensajes sin modificar el contenido ni la secuencia del progreso operativo existente.
- `Usuarios PAM` elimina el resaltado rectangular de selección propio de `ListBoxItem`; cada cuenta conserva únicamente su tarjeta blanca y sus acciones. Los botones `Renombrar` y `Eliminar` ganan ancho para evitar texto comprimido.

## 21 de septiembre de 2026 — Diseño de fase 24: Pending sin familias de Application

- Un diagnóstico de una ejecución de 15 solicitudes confirmó que los envíos llegaron a Pending y que el fallo ocurrió después, durante `PamPendingService.Compare`.
- Las capturas y la comprobación del usuario confirmaron que `System` y `Account` son iguales entre Favorites y Pending, mientras `Application` puede ser diferente para múltiples accesos. `Ticket` aparece una sola vez y no aporta identidad.
- Se aclaró que existen aproximadamente 383 accesos distintos disponibles, no que una ejecución deba pedirlos todos. La colección activa continuará determinando el subconjunto real.
- Se aprobó retirar completamente las familias hardcodeadas de `Application`; no son escalables y no deben ampliarse con reglas por cada acceso.
- El preflight conservará la advertencia de solicitudes ya existentes usando `System`, `Account`, `Request Start Date`, `Expires` y `Description`, con semántica de multiconjunto para claves repetidas.
- La reconciliación posterior conservará una línea base en memoria, excluirá todos los Request ID anteriores y aceptará únicamente IDs nuevos cuyos campos estables coincidan con el lote enviado.
- Las cargas parciales de Pending se reintentarán; nuevas filas ajenas se ignorarán y cantidades superiores indistinguibles se tratarán como ambigüedad. En esta fecha solo se documentó la fase 24; no se modificó código.

## 21 de septiembre de 2026 — Implementación de fase 24

- `PamPendingService` dejó de conocer familias de aplicaciones. `Application` ya no participa en el preflight ni en la reconciliación de Pending.
- El comparador filtra los atributos exactos del lote, agrupa por `System + Account` y resuelve repeticiones por conteo. Una lectura parcial permanece incompleta y se reintenta; un exceso indistinguible se rechaza como ambiguo.
- Todos los Request ID observados antes del primer envío continúan excluidos en cada lectura posterior. Las pruebas mantienen un ID antiguo visible junto a uno o varios IDs nuevos y demuestran que solo los nuevos llegan al resultado.
- `PamAutomationExecutionService` incorpora defensa en profundidad antes del resultado y del correo: exige exactamente un ID por solicitud enviada y rechaza cualquier intersección con la línea base.
- La compilación `Release | x64`, 30 pruebas específicas de Pending y modelos de ejecución y las 418 pruebas ordinarias pasaron sin abrir PAM, crear solicitudes ni enviar correo.

## 21 de septiembre de 2026 — Fase 24.4: reutilización explícita de Pending

- El aviso de solicitudes idénticas dejó de ser una confirmación binaria. Inicialmente ofrecía cancelar, automatizar con solicitudes repetidas o usar los Request ID existentes; la opción de repetir fue retirada por el ajuste seguro del 22 de septiembre.
- La tercera opción solo se habilita cuando todas las solicitudes esperadas tienen una coincidencia previa completa y no ambigua y la acción final configurada es correo. Coincidencias parciales o excesivas no pueden producir un correo incompleto.
- Elegir IDs existentes omite por completo el envío PAM, crea un resultado `ExistingPending` y comunica cero solicitudes nuevas. Los IDs permanecen en memoria y pasan por las validaciones y protecciones de Outlook ya existentes.
- La ruta normal mantiene la garantía de fase 24: excluye todos los IDs antiguos y solo devuelve los nuevos. Las dos rutas no pueden mezclarse.
- La compilación `Release | x64`, 54 pruebas relacionadas y las 423 pruebas ordinarias pasaron sin abrir PAM, crear solicitudes ni enviar correo.

## 21 de septiembre de 2026 — Corrección del reenvío con Pending existente

- El diagnóstico `pam-automation-diagnostic-20260921-175432` demostró que los diez Request ID existentes se recuperaron correctamente, pero el correo no se creó porque la misma huella ya se había enviado durante esa sesión y actuó la barrera `DuplicateInMemory`.
- La opción explícita de usar IDs existentes ahora prevalece sobre las barreras de correo duplicado, tanto en memoria como en la consulta de Outlook. La protección continúa intacta para ejecuciones normales y pruebas.
- Al terminar esa ruta se muestra una confirmación modal diferenciada para `SentConfirmed`, `SubmittedUnconfirmed` y `OutboxPending`; los estados no satisfactorios se comunican como error y nunca como entrega confirmada.
- El mismo diagnóstico registró un fallo intermitente de login al leer una alerta que PAM reemplazó durante la observación. La lectura usa ahora un plazo breve y reintenta la observación del DOM sin reenviar credenciales.
- La compilación `Release | x64`, 38 pruebas focalizadas y la suite completa de 425 pruebas aprobaron. Las regresiones nuevas demuestran que una repetición ordinaria continúa bloqueada y que un segundo envío sí ocurre cuando fue ordenado expresamente; no se contactó PAM ni Outlook real.

## 21 de septiembre de 2026 — Pending incompleto durante una transición paginada

- El diagnóstico `pam-automation-diagnostic-20260921-191315` confirmó que PAM procesó diez solicitudes y que el fallo ocurrió después, durante la reconciliación de sus Request ID. No fue un problema de login ni de Outlook.
- `WaitForPendingPageAsync` leía la firma de la página mientras el grid todavía reemplazaba sus filas. Una columna momentáneamente ausente produjo una `ArgumentException` que, a diferencia de la lectura estable posterior, no estaba clasificada como transitoria.
- La transición verifica primero el paginador, limita cada lectura de celda a un segundo y vuelve a observar errores transitorios de estructura, formato o timeout. Cancelaciones y errores inesperados no se absorben.
- Las filas solo se aceptan después de dos snapshots completos e idénticos; una estructura permanentemente inválida continúa terminando con un error seguro.
- Los fallos de reconciliación posteriores a envíos PAM se comunican ahora como automatización incompleta, con el conteo procesado y la instrucción de revisar Pending o reutilizar sus IDs antes de repetir.
- La compilación `Release | x64`, 80 pruebas focalizadas y la suite completa de 428 pruebas aprobaron. No se contactó PAM ni Outlook real.

## 21 de septiembre de 2026 — Fase 25: esperas adaptativas

- Se creó `PamOperationTimingPolicy` para retirar decisiones temporales dispersas y diferenciar observación rápida, probe transitorio y límite máximo.
- Playwright usa 60 segundos como techo de navegación y controles, pero sondea cada 200 ms y continúa inmediatamente cuando PAM está listo. Las lecturas DOM individuales no pueden consumir toda la ventana.
- Favorites y Pending toleran renderizados parciales sin aceptar filas incompletas. Pending amplió sus refrescos de consistencia eventual a diez y sus reescaneos por cambios de total a cinco.
- La verificación del formulario reemplazó pausas fijas por una comprobación adaptativa de todos los valores. `Submit Request` permanece fuera de cualquier reintento.
- Outlook observa Enviados inmediatamente, amplía su ventana acotada de confirmación y recupera un arranque COM lento mediante comprobaciones de un segundo. `Send()` no se repite.
- El acceso visible amplió a 60 segundos el arranque de Edge y las respuestas del portal, conservando una ventana distinta para la decisión manual del certificado.
- La compilación `Release | x64` y las 431 pruebas ordinarias aprobaron. No se contactó PAM ni Outlook real.

## 21 de septiembre de 2026 — Lectura de Pending con tamaño máximo de página

- El diagnóstico con 56 filas motivó retirar la paginación innecesaria de los lotes que caben en una sola página.
- Browser espera ahora que `Status = Pending` cierre y vuelva a estar habilitado; esto evita aceptar el `0 items` transitorio que PAM muestra mientras reemplaza el grid.
- El lector abre `items per page`, elige la mayor opción numérica disponible, confirma el total de páginas y conserva el recorrido paginado para cantidades superiores o versiones sin selector.
- Una prueba real autorizada y de solo lectura confirmó mediante captura `200 items per page`, `Page 1 of 1` y `1-56 of 56 items`. El navegador temporal permaneció oculto como exige el flujo automático; no se crearon solicitudes ni se abrió Outlook.

## 21 de septiembre de 2026 — Diagnóstico explícito de Request ID y reconciliación

- El usuario autorizó incluir Request ID, `System`, `Application`, `Account`, descripción, inicio y expiración para diagnosticar las ambigüedades que los conteos anteriores no podían distinguir.
- Cada ejecución preparada reemplaza una traza en memoria con línea base, valores esperados, decisión, conteo enviado e intentos completos posteriores. La diferencia identifica intersección, IDs ausentes y nuevos, candidatos exactos, filas inesperadas y multiplicidades por identidad estable.
- Las excepciones de Pending registran ahora `PendingErrorCode` en el log estructurado y en la traza. El ZIP explícito incorpora `pending-reconciliation.json`; ninguna evidencia detallada se persiste automáticamente.
- `environment.json` identifica la compilación mediante versión informativa y `ModuleVersionId`. Siguen excluidos credenciales, contraseña, semilla y código TOTP, cookies, tokens y contenido del correo.
- La compilación `Release | x64`, 30 pruebas focalizadas y la suite completa de 434 pruebas aprobaron sin contactar PAM ni Outlook.

## 22 de septiembre de 2026 — Recuperación segura de la lectura posterior de Pending

- El diagnóstico `pam-automation-diagnostic-20260922-061148` registró 24 Request ID en la línea base, 12 solicitudes confirmadas por el flujo de envío y cero intentos de comparación. La primera lectura posterior agotó el tiempo dentro de `ScanAllPendingPagesAsync` y terminó como `PortalUnavailable`; no fue una ambigüedad ni una confusión entre IDs antiguos y nuevos.
- `PamPendingService` reintenta ahora una lectura fallida volviendo a navegar a Pending dentro de la misma sesión. Si dos lecturas fallan, cierra esa sesión temporal y crea una segunda sesión autenticada exclusivamente para recuperar los IDs.
- La línea base, las solicitudes esperadas y los campos temporales permanecen inmutables entre sesiones. La recuperación no tiene acceso al servicio de envío y nunca vuelve a ejecutar `Submit Request`.
- Solo `PortalUnavailable` y `InvalidRequest` transitorio participan en la recuperación; cancelaciones, IDs duplicados, ambigüedad y errores no clasificados conservan el fallo inmediato. Todos los límites y pausas quedaron centralizados en `PamOperationTimingPolicy`.
- `pending-reconciliation.json` registra recargas, sesiones fallidas y reconstrucciones sin trasladar Request ID ni identidades al log general. Si se agotan los intentos, conserva el código final y el mensaje existente pide revisar Pending antes de repetir.
- La compilación `Release | x64` y la suite completa de 437 pruebas aprobaron. No se abrió PAM ni Outlook, no se creó ninguna solicitud y no se envió correo durante esta corrección.

## 22 de septiembre de 2026 — Recuperación integral del preflight de Pending

- Se verificó en el código la brecha descrita en `PENDING_PREFLIGHT_RECOVERY_HANDOFF.md`: el preflight tenía una sola lectura y el diagnóstico se inicializaba después de obtener la línea base.
- Se extrajeron bucles privados compartidos de recuperación para preflight y reconciliación: dos intentos de lectura y como máximo dos sesiones temporales. Cada sesión se dispone antes de crear su reemplazo y la autenticación fallida no se reintenta.
- Browser identifica `SnapshotUnstable` de forma explícita. `InvalidRequest`, errores no clasificados, IDs duplicados, ambigüedad, diferencias definitivas y cancelación ya no disparan reconstrucción. Se mantienen las validaciones de integridad y estabilidad.
- La traza empieza antes de leer Pending, distingue etapas y registra todas las lecturas, errores y recuperaciones. La línea base se incorpora mediante copia defensiva sin borrar el historial. El detalle usa esquema 3 y omite la línea base todavía desconocida; el manifiesto del ZIP mantiene esquema 2.
- Una prueba de coordinación recupera tanto preflight como reconciliación con sesiones nuevas y demuestra exactamente un envío simulado, conservando y excluyendo el ID antiguo. Otra exporta el fallo persistente anterior a todo envío. Se validaron cancelación, fallos definitivos, autenticación sin reintento, aislamiento de copias y ausencia de campos privados o secretos en el log.
- La primera comprobación de exportación esperaba un nulo explícito; se corrigió la prueba para respetar el comportamiento existente del exportador, que omite propiedades nulas. El exportador no necesitó cambios.
- Validación final: compilación `Release | x64` sin errores, 74 pruebas focalizadas y 453 pruebas ordinarias aprobadas. Solo permanece la advertencia heredada `MSTEST0032` de `TestExplorerSmokeTests.cs`, fuera del alcance. No se contactó PAM ni Outlook ni se utilizaron credenciales reales; no se publicó una nueva distribución.

## 22 de septiembre de 2026 — Captura atómica de Pending

- El diagnóstico `pam-automation-diagnostic-20260922-071241` confirmó que las cuatro lecturas de preflight agotaban o desestabilizaban el grid incluso después de recargar y reconstruir la sesión; ninguna solicitud llegó a enviarse.
- La comparación con el aplicativo anterior identificó la diferencia decisiva: .NET realizaba una llamada Playwright por cada celda y exigía después repetir todo el recorrido, mientras la versión anterior capturaba la tabla visible en una sola operación DOM.
- `PlaywrightManagedPage.Pending` obtiene ahora todas las filas y columnas técnicas visibles en una única captura. La instantánea solo transporta texto hacia C#; allí se rechazan columnas ausentes, duplicadas o vacías, Request ID y fechas inválidos y estados distintos de Pending.
- Permanecen dos capturas completas e iguales, conteo total, IDs únicos, paginación de respaldo, exclusión de la línea base y los reintentos seguros incorporados previamente. `Submit Request` y Outlook siguen fuera de cualquier reintento de lectura.
- La compilación `Release | x64` y 83 pruebas focalizadas aprobaron. Se verificaron snapshots de 30, 60, 100 y 200 filas, además de 250 registros repartidos en 200 + 50, sin pérdida ni repetición de IDs. Una integración local ejecutó la captura masiva real con Edge sobre esos tamaños en 3,4 segundos y sin red. La integración real opt-in de solo lectura inició sesión, capturó Pending y terminó correctamente en 22,6 segundos; no abrió formularios, no creó ni modificó solicitudes y no usó Outlook.

## 22 de septiembre de 2026 — Eliminación del reenvío de solicitudes idénticas

- Se retiró la opción `Automatizar con solicitudes repetidas`. Una coincidencia exacta en Pending nunca puede continuar por la ruta normal de `Submit Request`.
- Un conjunto completo y no ambiguo para toda la colección ofrece únicamente reutilizar sus Request ID para el correo o cancelar. La reutilización sigue requiriendo que Outlook esté habilitado.
- Coincidencias parciales, ambiguas o sin correo habilitado solo permiten cancelar. El aviso instruye cambiar la fecha u hora de inicio, la duración de la sesión o el motivo antes de intentarlo otra vez.
- `PamAutomationExecutionService` aplica la misma barrera antes de crear una sesión de envío, como defensa independiente de la UI, y registra `BlockedExistingPending` si una llamada intenta evadirla.
- La compilación `Release | x64` y 97 pruebas focalizadas aprobaron. No se abrió PAM ni Outlook y no se creó ninguna solicitud o correo real.

## 22 de septiembre de 2026 — Integridad y recuperación acotada de Pending

- El diagnóstico `pam-automation-diagnostic-20260922-175659` contiene cuatro timeouts y un único snapshot de 25 filas (18 antiguas y siete nuevas), obtenido entre el segundo y el tercer timeout. No demuestra retraso del servidor en crear las cinco solicitudes restantes.
- Se corrigió el reconocimiento del contador con rango, se eliminó la aceptación de un total desconocido y se exige coherencia de filas, rango, página y tamaño seleccionado. Cada salto revalida el estado y el botón habilitado.
- La primera integración real de esta corrección descubrió que PAM retira temporalmente el selector de tamaño durante la carga; la confirmación ya no vuelve a localizar su ancestro en ese intervalo.
- Se añadieron límites de 30 segundos por lectura y 120 segundos por etapa con cancelación propagada. Una lectura vencida cierra su página y exige otra sesión para impedir clics pendientes sobre una vista recargada.
- El diagnóstico añade duración, operación y conteos técnicos. Continúa excluyendo credenciales, TOTP, cookies, tokens y correo.
- Las pruebas locales reproducen 25→30 filas, paginación 25+5 y 200+50, contador desconocido, botón deshabilitado, vencimiento global y propagación/exportación del diagnóstico.
- La integración autorizada de solo lectura con Edge visible recuperó 30 filas tres veces consecutivas: 6109, 6094 y 8984 ms. No creó ni modificó solicitudes ni utilizó Outlook. La creación de un lote nuevo queda para la aceptación operativa del usuario.
- Una selección amplia de pruebas descubrió el fallo heredado `RepeatedPendingDecisionWindow_CompleteSetWithoutEmail_RequiresOutlook`: espera una frase que ya no coincide con el texto del diálogo. Sus archivos no se modificaron.
- Validación final focalizada: compilación Release x64 correcta y 96 pruebas aprobadas (87 de Pending y navegador, tres de coordinación, cuatro de exportación y dos de almacenamiento del diagnóstico). Las pruebas del lector completo y de captura masiva usaron Edge visible con respuestas locales; la prueba real de tres lecturas pasó por separado. Permanece la advertencia heredada MSTEST0032.
- Se generó mediante `dotnet publish` la copia Windows x64 autocontenida `artifacts/publish/pending-recovery-20260922`, identificada por `1.0.2-pending-recovery.20260922`. No se publicó ningún release externo.

## 22 de septiembre de 2026 — Consulta de Outlook filtrada y observación posterior independiente

- El diagnóstico operativo posterior confirmó seis ejecuciones completas y nueve incidentes COM de lectura `0x80040114`, cada uno registrado en dos capas. El diagnóstico no permite atribuir todos los fallos a una propiedad o carpeta concreta.
- La confirmación utiliza `ObserveDeliveryAsync`, con Enviados durante las observaciones y consulta adicional de Outbox/Drafts solo al final. La búsqueda previa de duplicados mantiene las tres carpetas y bloquea si no se puede completar.
- `Items.Restrict` filtra por asunto estándar con escape DASL. La clase se valida antes de acceder al asunto; asunto y huella se verifican exactamente sobre los candidatos. No se usan campos personalizados en el filtro ni se convierten excepciones en ausencia.
- Solo los códigos COM de lectura reconocidos permiten reintentar. Un error permanente o el agotamiento de errores termina la observación preservando el resultado original de `Send()`, sin reenviar ni retirar la protección de duplicados en memoria.
- Se evita registrar el mismo fallo de observación como ERROR y WARNING. Los diagnósticos añaden carpeta, operación, HRESULT, cantidad/índice de candidatos, duración y número de recuperaciones sin contenido privado.
- Validación: compilación Release x64 correcta; 58 pruebas del adaptador/composición de Outlook, ocho de confirmación, cuatro de coordinación de correo y siete de configuración aprobadas (77 en total). No se abrió PAM ni Outlook ni se enviaron correos reales.
- El filtro inicial amplio por `Outlook` incluyó también la prueba heredada del texto de `RepeatedPendingDecisionWindow_CompleteSetWithoutEmail_RequiresOutlook`; falló por la misma diferencia de frase ya documentada y no se modificó. Permanece la advertencia heredada MSTEST0032.
- Se generó mediante `dotnet publish` la copia autocontenida Windows x64 `artifacts/publish/outlook-observation-20260922`, identificada por `1.0.2-outlook-observation.20260922`, conservando sus dependencias. No se publicó ningún release externo. La validación frente al Outlook real queda para la siguiente ejecución operativa del usuario.

## 22 de septiembre de 2026 — Pending vacío sin interacción con el selector de tamaño

- El diagnóstico `pam-automation-diagnostic-20260922-223600` identificó cuatro fallos de preflight con total cero y operación `SelectPageSize`; no se habían creado solicitudes ni utilizado Outlook.
- El lector conserva la primera vista estable y, si confirma cero registros, omite `items per page`. El escaneo posterior vuelve a validar filas, paginación y contador; no se convierte una carga en curso ni un total desconocido en una lectura vacía.
- Antes de cambiar producción, las pruebas nuevas reprodujeron tres fallos con el código anterior: selector deshabilitado, etiqueta sin selector y selector habilitado que no debía utilizarse. El caso sin paginador ya funcionaba.
- Después del cambio aprobaron 21 pruebas de `PamPendingGridTransitionTests` con la integración local habilitada y Edge visible (incluidas siete nuevas variantes), además de 45 pruebas de `PamPendingServiceTests`. Compilación Release x64 correcta. No se alteraron los cambios anteriores de Outlook.
- La integración real expresamente autorizada recuperó cero filas tres veces consecutivas en 4265, 4047 y 4125 ms. Utilizó un contexto temporal de Edge visible y las credenciales protegidas, sin crear ni modificar solicitudes ni usar Outlook. Evidencia local: `results/pending-empty-real-20260922/pending-empty-real.trx`, con conteos y tiempos, sin contenido de solicitudes.
- Se generó mediante `dotnet publish` la copia autocontenida `artifacts/publish/pending-empty-20260922`, con `InformationalVersion=1.0.2-pending-empty.20260922`. Incluye los cambios anteriores de Outlook y sus dependencias; no se publicó un release externo.

## 22 de septiembre de 2026 — Copiar Request ID desde la ventana de resultados

- El usuario confirmó que las correcciones de Pending y Outlook resolvieron los problemas observados y solicitó un botón de copia en el resultado.
- Se añadió `AutomationResultWindow`, una ventana WPF modal con el resumen, lista seleccionable/desplazable, `Copiar Request ID` y `Aceptar`. El texto copiable se recibe separado del mensaje y conserva solo los IDs, uno por línea y en su orden original.
- La copia Unicode ocurre únicamente al pulsar el botón. Muestra confirmación local o un aviso que permite reintentar ante un portapapeles ocupado, sin cerrar el resultado ni registrar IDs.
- Compilación Release x64 y 36 pruebas existentes de la ventana principal aprobadas. Una comprobación WPF visible verificó 12 y 250 IDs, copia exacta, fallo/reintento simulado y acciones accesibles con la ventana reducida. El portapapeles del usuario no fue alterado; no se contactó PAM ni Outlook. Permanece la advertencia heredada MSTEST0032.
- Se generó la publicación local autocontenida `artifacts/publish/copy-request-ids-20260922` con `InformationalVersion=1.0.2-copy-request-ids.20260922`, incluyendo las correcciones previas. No se publicó un release externo.

## 22 de septiembre de 2026 — Desbloqueo de la publicación portable

- La publicación se detenía correctamente por una aserción obsoleta: la prueba esperaba `activa el envío con Outlook`, mientras el diálogo identifica la opción real `activa «Enviar correo con Outlook»`.
- Se actualizó esa prueba y se verifican también la imposibilidad de reutilizar IDs para correo cuando está desactivado y la instrucción de cancelar. No se cambió el diálogo ni se omitieron pruebas fallidas.
- Restauración win-x64, compilación Release x64 y suite completa aprobadas: 512 casos, 505 correctos, siete omitidos por falta de opt-in del navegador y cero fallos. La advertencia heredada MSTEST0032 no bloquea el proceso.
- Se ejecutó `dotnet publish` y se empaquetó la salida con la misma estructura portable del script: `artifacts/publish/final/PAM-Automation-1.0.2-win-x64.zip`, 523 archivos y metadatos 1.0.2/1.0.2.0 verificados. Se calculó SHA-256 y se comprobó la presencia del ejecutable y de todos los archivos dentro del ZIP. No se contactó PAM ni Outlook ni se publicó un release externo.

## 25 de septiembre de 2026 — Detalle diagnóstico de verificación del formulario

- Tras un fallo antes de `Submit Request`, el diagnóstico anterior no conservaba qué campo cambió o si la lectura de PAM fue inestable. Se añadió una excepción tipada con etapa, campos discrepantes, número de observaciones, fallos de lectura y último conjunto completo observado.
- La exportación incluye valores esperados/observados de fecha, hora, duración y `Application Session` cuando cumplen formatos acotados. Del motivo conserva solo longitud y discrepancia; texto inesperado del portal se sustituye por una marca. Contraseñas, TOTP, cookies y tokens siguen excluidos.
- Compilación `Release | x64` correcta y pruebas focalizadas del registro/exportación aprobadas. No se inició sesión en PAM ni se enviaron solicitudes o correos. La advertencia heredada MSTEST0032 permanece.
