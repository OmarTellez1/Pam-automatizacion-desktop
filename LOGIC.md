# Lógica prevista — Pam Automation .NET

## Estado

Este documento describe la lógica objetivo de la aplicación nueva. Ya existen la estructura base, la configuración, el almacenamiento protegido de credenciales, TOTP, navegador restringido, autenticación PAM real visible, lectura de Favorites, solicitudes reales, validación segura de formularios, composición HTML saneada, Outlook clásico aislado y la coordinación del flujo PAM desde la interfaz.

## Capas

- WPF/XAML: interfaz y presentación.
- Core: modelos, reglas, validación, TOTP e interfaces.
- Browser: Playwright .NET, Edge externo y portal PAM.
- Outlook: integración con Outlook clásico mediante COM.
- Infrastructure: DPAPI o Credential Manager, archivos de configuración, reloj y dependencias del sistema.
- Tests: unitarias, simulaciones del portal y pruebas controladas de integración.

La lógica de negocio no debe vivir en el code-behind de las ventanas WPF.

La estructura física, la dirección permitida de dependencias y la arquitectura objetivo se mantienen detalladas en `structure.md`. Ese documento debe reflejar cada módulo cuando pase de planeado a implementado.

## Estado de presentación

La ventana principal utiliza MVVM mediante `CommunityToolkit.Mvvm`. El ViewModel expone el estado de cada configuración, los comandos visibles y las condiciones para habilitar acciones; el code-behind se limita a inicializar la ventana.

La composición visual toma como referencia la aplicación anterior: encabezado con marca, selector de acción final en la esquina superior, acción principal azul centrada y tres accesos horizontales de configuración. La implementación permanece enteramente en controles WPF y recursos XAML, con adaptación al tamaño de la ventana y escalado DPI.

La aplicación comienza en estado incompleto y seguro. `Iniciar automatización` solo puede habilitarse cuando credenciales y solicitud estén configuradas y, si la acción final incluye correo, exista también una configuración de correo válida. Mientras una ejecución esté activa, los controles y comandos de configuración quedan deshabilitados.

El botón de prueba permanece separado del botón real. Solo es visible cuando `testModeEnabled` está activo y toda la configuración requerida es válida. La interfaz permite guardar credenciales sin volver a mostrar secretos y editar fecha, hora `AM/PM`, duración, razón y modo de prueba. La acción real hace una prelectura de `Favorites`, muestra el conteo y la configuración en una confirmación modal y conserva los controles bloqueados hasta terminar o fallar.

La fase 16.5 toma la aplicación JavaScript anterior únicamente como referencia funcional y de claridad. `Editar credenciales` presenta dos tarjetas: PAM muestra si existen secretos, permite revelar temporalmente únicamente lo recién escrito y ejecuta un login visible no destructivo después de guardar; Outlook explica los requisitos, muestra su estado, verifica la cuenta clásica exacta y separa el correo de prueba confirmado. Los mensajes de operación permanecen dentro de la ventana y nunca reproducen secretos ni detalles internos.

`Editar solicitud` muestra la fecha completa en español, conserva un campo `hh:mm` con selector `AM/PM`, separa duración y razón y presenta errores junto al formulario. `Editar envío de correo` informa la incompatibilidad del nuevo Outlook, muestra la cuenta remitente sin permitir cambiarla allí, explica `Para` y `CC`, conserva las herramientas enriquecidas e inserta `{{REQUEST_IDS}}` en el cursor mostrando si existe exactamente una vez.

`PamAutomationExecutionService` vuelve a verificar el conteo y la configuración después de confirmar, ejecuta las solicitudes, espera la consistencia de `Pending` y produce un `PamExecutionResult` defensivo. La vista mantiene únicamente en memoria el conteo de filas, procesadas, conflictos, hora final, Request ID y estado del correo; los errores desconocidos se sustituyen por un texto fijo sin detalles internos. Un fallo después de procesar alguna fila presenta explícitamente el conteo parcial y exige revisar `Pending` antes de reintentar.

## Persistencia y configuración

La aplicación no utilizará archivos .env.

Los secretos PAM se almacenarán en Windows Credential Manager, asociados al usuario actual de Windows. La aplicación recuperará estos valores únicamente cuando vaya a validar credenciales o iniciar una automatización:

- Usuario PAM.
- Contraseña PAM.
- Semilla TOTP.

La fase 4 define `IPamCredentialStore`, `PamCredentials` y `PamCredentialUpdate` en Core e implementa `WindowsPamCredentialStore` en Infrastructure. Usuario, contraseña y semilla se almacenan juntos como una única credencial genérica bajo el destino estable `PamAutomation/PamCredentials/v1`. El blob interno tiene versión propia y longitudes explícitas; no es JSON ni se escribe en archivos.

`CredWriteW` sustituye la entrada completa para evitar estados parciales entre contraseña y semilla. `CredReadW` recupera la entrada asociada al usuario actual y su búfer nativo siempre se libera con `CredFree`. Las copias administradas y no administradas del blob se sobrescriben con ceros tan pronto termina cada operación. Las cadenas administradas se mantienen solo el tiempo necesario, reconociendo que .NET no permite borrarlas de memoria de forma fiable.

Durante una edición, cada campo nulo, vacío o compuesto solo por espacios conserva su valor anterior dentro del almacén; la interfaz no necesita recuperar secretos para rellenar controles. La primera configuración exige los tres valores. `ToString()` y la vista del depurador de los modelos sensibles están redactados, y las excepciones controladas no incluyen usuario, contraseña ni semilla.

Las pruebas de esta fase utilizan un adaptador falso de la API de Windows. Verifican formato, actualización parcial, límites, corrupción, borrado y limpieza de buffers sin leer ni escribir el Credential Manager real.

La fase 3A representa la configuración no sensible mediante `AppSettings`, `RequestSettings` y el enum `FinalAction`. `AppSettings` establece `schemaVersion = 1`, `America/Guayaquil`, `Mostrar IDs` y `testModeEnabled = false` como valores predeterminados seguros.

`AppSettingsValidator` y `RequestSettingsValidator` devuelven todas las fallas detectadas mediante códigos estables y mensajes de presentación. La hora actual de Ecuador se entrega explícitamente al validador para mantener la lógica determinista; su obtención desde el sistema se implementará detrás de una abstracción en una fase posterior.

Los tipos `DateOnly` y `TimeOnly` impiden representar días u horas inexistentes. La validación rechaza fechas anteriores al día actual de Ecuador, horas pasadas para el mismo día, duración fuera de 1 a 8 horas, razón vacía, esquema incompatible, acción final desconocida y una zona horaria distinta de `America/Guayaquil`.

La fase 3B define `IAppSettingsStore` en Core e implementa `JsonAppSettingsStore` en Infrastructure. Si aún no existe un archivo, la carga devuelve los valores predeterminados seguros sin crear carpetas ni archivos. El guardado crea `%LOCALAPPDATA%\PamAutomation\config` únicamente cuando es necesario y reemplaza `app-settings.json` mediante un archivo temporal único escrito en la misma carpeta.

El documento JSON es plano y contiene exactamente `schemaVersion`, `finalAction`, `requestReason`, `sessionHours`, `startDate`, `startTime`, `timeZone` y `testModeEnabled`. La fecha y la hora utilizan la representación ISO de `DateOnly` y `TimeOnly`. Las propiedades desconocidas, el contenido incompleto, JSON inválido y acciones finales desconocidas se rechazan sin usar valores implícitos. Una versión distinta de `schemaVersion = 1` produce un error específico, dejando un punto explícito para agregar migraciones futuras sin reinterpretar silenciosamente el archivo.

La sustitución del archivo existente utiliza una operación de reemplazo en la misma unidad después de vaciar el contenido del temporal al disco. Ante cancelación o error se intenta retirar el temporal y nunca se modifica deliberadamente el archivo anterior mediante una escritura parcial.

La configuración general se guardará como JSON en:

    %LOCALAPPDATA%\PamAutomation\config\app-settings.json

Sus propiedades serán equivalentes a la configuración actual:

- finalAction equivale a PAM_FINAL_ACTION.
- requestReason equivale a PAM_REQUEST_REASON.
- sessionHours equivale a PAM_SESSION_HOURS.
- startDate equivale a PAM_START_DATE, persistida como fecha ISO.
- startTime equivale a PAM_START_TIME.
- timeZone debe ser America/Guayaquil.
- testModeEnabled habilita el modo de prueba y será `false` por defecto.

La configuración de correo se guardará en:

    %LOCALAPPDATA%\PamAutomation\config\mail-settings.dat

El archivo contendrá remitente, Para, CC, asunto y HTML enriquecido con el marcador REQUEST_IDS. Debe protegerse con DPAPI CurrentUser. La cuenta seleccionada en Outlook clásico se mantiene separada para poder verificarla antes de que exista una plantilla completa:

    %LOCALAPPDATA%\PamAutomation\config\outlook-account.dat

`outlook-account.dat` contiene únicamente `schemaVersion` y la dirección SMTP exacta, también protegidos con DPAPI `CurrentUser` y escritura atómica. Cuando una instalación existente solo tiene `mail-settings.dat`, la aplicación migra el remitente a este documento sin perder la plantilla.

La única persistencia propia de la aplicación será:

    %LOCALAPPDATA%\PamAutomation\config\
    ├── app-settings.json
    ├── mail-settings.dat
    └── outlook-account.dat

No persistir `results`, `logs`, `artifacts`, `temp`, `last-run`, `last-email` ni `delivery-state`. El resultado de una ejecución se representará mediante un modelo en memoria que contenga Request ID, conflictos, estado y errores. La interfaz lo mostrará mientras la aplicación permanezca abierta.

Si la acción final es correo, la aplicación debe preparar, validar y enviar el mensaje dentro de la misma ejecución. La protección inmediata contra duplicados utilizará el estado en memoria de la ejecución. Antes de enviar, también se consultarán Enviados, Bandeja de salida y Borradores de Outlook para detectar un mensaje con el mismo asunto y el mismo conjunto exacto de Request ID. De este modo se conserva protección entre reinicios sin crear `last-email`, `delivery-state` ni otro historial local.

No habrá auditoría local ni debugging persistente por defecto. Un modo de diagnóstico futuro, si se necesita, deberá activarse explícitamente y exportar manualmente la información a una ubicación seleccionada por el desarrollador o soporte. No debe formar parte del almacenamiento normal.

Las escrituras de `app-settings.json`, `mail-settings.dat` y `outlook-account.dat` son atómicas, incluyen `schemaVersion` y permiten migraciones futuras. Los dos documentos `.dat` se cifran con DPAPI `CurrentUser`; `mail-settings.dat` protege remitente, destinatarios, asunto, XHTML saneado y bytes PNG de imágenes inline. No se escribe configuración dentro de `Program Files` ni junto al ejecutable.

Windows o Playwright pueden utilizar `%TEMP%` para archivos internos transitorios durante el lanzamiento del navegador. No tratar esa ubicación como almacenamiento persistente, no escribir secretos allí y limpiar lo creado por la aplicación cuando sea posible.

## Diferencia entre desarrollo y aplicación publicada

La ausencia de archivos operativos persistentes aplica al ejecutable instalado que utilizan los compañeros. El repositorio de desarrollo sí necesita evidencias para investigar fallos y validar cambios.

Durante desarrollo, pruebas e integración controlada, se podrán crear en la raíz del repositorio:

    results\
    logs\
    artifacts\
    temp\

Estas carpetas estarán excluidas de Git y no se incluirán en el instalador. Contendrán únicamente resultados y diagnóstico técnico sin secretos. El perfil de publicación debe usar un almacenamiento en memoria y no debe escribir esas carpetas aunque el ejecutable se inicie desde otra ubicación.

## Autenticación

La fase 5 implementa en Core el cálculo RFC 6238 con HMAC-SHA1, intervalos de 30 segundos y códigos de seis dígitos. La clave de enrolamiento se decodifica como Base32 sin almacenarla; se aceptan mayúsculas, minúsculas y los espacios utilizados para mostrarla en grupos, mientras que símbolos, longitudes, padding o bits residuales inválidos se rechazan con un error que no reproduce la clave.

El resultado TOTP incluye su inicio y expiración UTC y puede evaluar el requisito de al menos ocho segundos restantes. Su representación textual y del depurador está redactada. Los buffers binarios de clave y hash se sobrescriben al terminar el cálculo. El algoritmo no decide la hora por sí mismo: recibe una referencia explícita para conservarse determinista. La fase 6 implementa `IPamClockFactory`, que exige una fecha HTTP válida del portal, incorpora la mitad del tiempo de ida y vuelta y una compensación central de 500 ms por la resolución de un segundo del encabezado, y después avanza con tiempo monotónico; nunca recurre silenciosamente al reloj local.

La fase 8 implementa `PamAuthenticationService` con un puerto del portal ligado a la misma sesión temporal creada para el intento. El servicio mide y entrega el tiempo monotónico de ida y vuelta al reloj, espera asincrónicamente una ventana TOTP segura y permite un segundo código únicamente cuando el primero fue rechazado explícitamente. Las respuestas desconocidas, fallos de conexión y falta de confirmación no se consideran éxito ni provocan reintentos. Esta fase utiliza dobles del portal y no contiene selectores reales.

La fase 9 implementa en Browser `PamAuthenticationPortalFactory` y `PamAuthenticationPortal`, ligados exclusivamente a la página de un `PamBrowserSession`. El adaptador usa los nombres accesibles reales `Username`, `Password`, `Log In`, `Enter Response`, `Submit` y `Return to dashboard`, y reconoce el rechazo TOTP observado sin registrar su contenido. La transición del portal anuncia `Navigation complete.` con `role=alert`; ese anuncio se ignora expresamente porque no representa un rechazo de credenciales. Los timeouts o estados ambiguos tampoco se convierten en rechazos explícitos.

La integración real permanece detrás de `PAM_AUTOMATION_ALLOW_REAL_LOGIN=LOGIN_ONLY`, recupera los valores únicamente desde Windows Credential Manager y abre Edge visible. La validación autorizada confirmó el dashboard y cerró el contexto temporal; no navegó a Favorites, no creó solicitudes y no abrió Outlook.

El flujo debe:

1. Abrir únicamente https://10.30.17.117.
2. Obtener la fecha del encabezado HTTP Date.
3. Calcular el TOTP con la hora del portal.
4. Esperar una ventana segura de vigencia.
5. Intentar el código una vez.
6. Si PAM lo rechaza explícitamente, esperar el siguiente intervalo y reintentar una sola vez.
7. No reintentar fallos de red o confirmación.
8. Confirmar el login con Return to dashboard.

La aplicación debe diferenciar credenciales rechazadas, TOTP rechazado, fallo de conexión y ausencia de confirmación.

## Navegador

Playwright .NET debe lanzar el Edge instalado mediante el canal msedge.

La fase 7 implementa `IPamBrowserSessionFactory` e `IPamBrowserSession`. Cada sesión crea un navegador visible y un contexto nuevo no persistente, bloquea service workers e instala una ruta que aborta solicitudes HTTP(S) cuyo esquema, host o puerto efectivo no coincidan con el origen PAM autorizado. La excepción HTTPS queda limitada a ese contexto. La respuesta pública de navegación contiene únicamente URI final, código de estado y el encabezado HTTP `Date`; no enumera ni expone cookies, tokens u otras cabeceras.

No se debe incluir Chromium. La aplicación debe utilizar la opción 1: lanzar el Edge instalado mediante el canal msedge y crear un contexto temporal de Playwright. No debe seleccionar, reutilizar ni modificar el perfil existente del usuario, sus cookies, extensiones o sesiones. No utilizar LaunchPersistentContext, userDataDir ni conexión CDP a un navegador existente. El aislamiento es interno de Playwright y no debe presentarse como una ventana de incógnito.

La primera versión utilizará Edge visible para la comprobación de credenciales y la automatización real. Se conservará el comportamiento funcional validado de la aplicación anterior, no su uso de Chrome, perfiles existentes o ejecución headless. Cualquier modo headless futuro requerirá una decisión explícita y la misma cobertura de seguridad e integración.

Los locators se refieren al DOM HTML del portal externo. La interfaz propia no utilizará HTML, CSS ni JavaScript. Priorizar roles, labels y atributos estables; CSS o XPath solo como último recurso.

La fase 10 permite que `PamAuthenticationService` entregue una sesión temporal ya autenticada cuyo consumidor debe cerrar. `PamFavoritesService` utiliza esa misma sesión para navegar a `/webconsole/ps/portal?tab=accounts`, identificar `Browse by Category` y seleccionar `Favorites`. El adaptador lee mediante locators las columnas visibles `System`, `Application` y `Account`, cuyos identificadores DOM actuales son `SystemName`, `ApplicationName` y `AccountName`; espera filas completas y estables y recorre el paginador accesible cuando existe, con un límite de 500 páginas.

Las identidades se conservan únicamente en memoria, redactan su representación textual y se validan como completas y únicas. El contrato no contiene métodos para seleccionar filas, abrir formularios o enviar solicitudes. La integración real `READ_ONLY` confirmó la navegación y lectura sin imprimir las cuentas ni producir efectos posteriores.

## Solicitudes

La aplicación debe separar claramente:

- Modo prueba: llena y valida formularios, descarta cada formulario y nunca pulsa Submit Request.
- Modo real: requiere confirmación y crea solicitudes.

La fase 11 implementa el modo de prueba mediante `PamRequestTestService`. El servicio reutiliza una única sesión autenticada para leer todas las `Favorites` rows y entregar cada identidad `System + Application + Account` al contrato `IPamRequestTestPortal`, que solo permite abrir, llenar, verificar y descartar; no expone ninguna operación de envío. Antes de interactuar con formularios, el adaptador Browser instala además una ruta que aborta cualquier acceso al endpoint `POST /api/ps-portal/requests` y trata un intento interceptado como fallo de seguridad.

Cada formulario conserva y comprueba fecha, hora, duración, razón y `Application Session`, luego ejecuta `Cancel > Discard Changes` y espera su cierre. El resultado permanece en memoria y fija `SubmittedCount` en cero. La integración explícita `VALIDATE_AND_DISCARD` validó y descartó 14 de 14 formularios en Edge visible con `submitted = 0`; no abrió Outlook ni persistió identidades o contenido.

La fase 12 implementa el modo real mediante un contrato distinto, `IPamRequestSubmissionPortal`. `PamRequestSubmissionService` valida primero la configuración, obtiene un snapshot nuevo de todas las `Favorites` rows y exige que coincidan tanto su conteo como la configuración completa con `PamRequestRunAuthorization`. Si cualquiera cambió desde la confirmación, termina antes de crear el portal de envío. Esta autorización es de una ejecución y no sustituye la confirmación visible que deberá conectar la interfaz en la fase 16.

Antes de cada efecto irreversible, Browser vuelve a localizar la `Favorites` row por `System + Application + Account`, abre `Access > Submit Request`, completa el formulario y verifica inmediatamente fecha, hora, duración, razón y `Application Session`. Solo entonces pulsa el botón final y espera específicamente el `POST /api/ps-portal/requests`. Una respuesta 2xx produce `Submitted`; el error 400 con código exacto `PSPortal.Requests.Policies.RemoteAppAccesssAlreadyAvailable` produce `ApprovedAccessOverlap` y permite seguir; cualquier otro estado o contenido detiene el proceso mediante un error seguro que no incluye el payload.

La cancelación se comprueba antes de cada solicitud. Una vez pulsado `Submit Request`, el adaptador espera una respuesta acotada sin convertir la cancelación tardía en un estado falsamente conocido: un timeout se trata como fallo porque el efecto remoto podría ser ambiguo. Los resultados por fila se copian y conservan solo en memoria. El opt-in destructivo de desarrollo se limita a un intento y no forma parte de la suite ordinaria.

La fase 13 implementa `PamPendingService` sobre un contrato exclusivamente de lectura. Browser navega a `Requests`, exige un único filtro `Status`, selecciona exactamente `Pending` y recorre el paginador accesible desde la primera página, con un límite de 500 páginas y hasta tres lecturas si el contador cambia. Un estado vacío es válido; filas incompletas, Request ID no numéricos o duplicados y cambios de página incoherentes son fallos bloqueantes.

Cada fila Pending se mantiene en memoria y redacta su representación textual. Para asociarla con una `Favorites` row se exige la misma fecha y hora completas y la misma combinación `System + Account`; luego se validan expresamente el `Application` solicitado, `Expires` calculado desde la duración y `Description`. El portal puede agregar a `Application` un sufijo de destino con el formato `nombre solicitado - destino`; no se aceptan familias aproximadas ni se deriva el nombre desde `System`. Si dos filas o dos cuentas producen una asociación no única se informa ambigüedad en lugar de elegir por posición. La comparación devuelve coincidencias ordenadas y faltantes explícitos. Tras una creación puede repetirse la lectura hasta seis veces con dos segundos de espera para cubrir consistencia eventual.

El usuario podrá cambiar `testModeEnabled` mediante `Habilitar modo de prueba` en una sección avanzada de Configuración de solicitud; no necesitará editar el JSON. `Iniciar prueba` solo será visible cuando esa opción sea verdadera y la configuración de credenciales y solicitud esté completa. Si la acción final es correo, también deberá existir una configuración de correo válida, pero los destinatarios guardados no se usarán: el correo de prueba se enviará exclusivamente a `tellezoe@hitss.com`, sin CC, con asunto prefijado por `[PRUEBA]` y después de una confirmación que muestre el destinatario exacto.

El modo real debe recorrer todas las `Favorites` rows, todas las páginas y las solicitudes `Pending` relevantes. Debe validar `System`, `Application`, `Account`, `Request Start Date`, `Expires` y `Description` antes de aceptar una reconciliación.

## Colecciones personalizadas posteriores a la migración

Después de completar la migración inicial se incorporará una fase para crear colecciones locales editables de `Favorites` rows. Una colección permitirá ejecutar un subconjunto elegido por el usuario, tendrá nombre propio y conservará una configuración predeterminada de fecha, hora, duración y razón. Se prevén sobrescrituras opcionales por fila, sujetas a confirmación durante el diseño de esa fase.

La colección guardará claves lógicas completas `System + Application + Account`, nunca índices del grid. `Favorites` continuará siendo la fuente de verdad: cada apertura o ejecución actualizará todas sus páginas y clasificará las filas guardadas como `Available`, `Missing` o `Ambiguous`. Las filas `New in Favorites` no se agregarán automáticamente ni se reemplazarán identidades por coincidencias aproximadas.

La referencia visual aportada establece una futura entrada nativa WPF con día, mes, descripción completa de fecha, hora de 12 horas y selector `AM/PM`, duración operativa en horas y razón. La conversión temporal se realizará en el dominio y el adaptador comprobará el valor normalizado por PAM. El recorrido confirmado, sus selectores e invariantes se mantienen en `PAM_NAVIGATION.md`.

El conflicto `PSPortal.Requests.Policies.RemoteAppAccesssAlreadyAvailable` se registra y permite continuar. Otros errores deben detener el proceso.

## Outlook

La fase 14 integra Outlook clásico mediante COM detrás de `IOutlookClassicClient`. Verifica una única cuenta por `SmtpAddress`, valida la cuenta con un mensaje temporal que siempre se descarta, asigna `SendUsingAccount` antes de `Send()` y crea un mensaje nuevo por operación. La ejecución COM se realiza en STA y las referencias se liberan explícitamente.

Los modelos que contienen dirección, asunto, HTML, destinatarios o Request ID redactan su representación textual. Invocar `Send()` solo produce `SubmittedUnconfirmed`; la entrega no se marcará confirmada hasta que una fase posterior la compruebe en Enviados. Un fallo COM devuelve un resultado seguro sin exponer el contenido.

La fase 15 incorpora `OutlookHtmlComposer` como límite determinista previo a Outlook. Solo acepta fragmentos XHTML bien formados generados por la aplicación, con DTD prohibidos y un máximo de 8 millones de caracteres. Conserva párrafos, encabezados, negrita, cursiva, subrayado, listas, tablas, estilos explícitamente permitidos e imágenes locales referenciadas mediante `cid:`; elimina scripts, estilos activos, formularios, marcos, objetos, eventos, vínculos y recursos externos. No acepta HTML web arbitrario.

La plantilla saneada debe contener `{{REQUEST_IDS}}` exactamente una vez dentro del texto. La composición valida Request ID numéricos y únicos, mantiene el orden recibido y los inserta separados por `<br />`. Antes de crear COM, `OutlookMessageValidator` exige que el cuerpo final sea idéntico a su versión saneada, que ya no contenga el marcador y que cada ID esperado aparezca como token numérico completo. `FlowDocumentHtmlConverter` produce ese XHTML y no puede evitar esta validación.

La integración COM debe ejecutarse en STA, liberar referencias y no enviar mensajes durante pruebas automáticas.

Las pruebas unitarias e integraciones automatizadas nunca enviarán correo. El envío de un correo de modo de prueba es una acción manual distinta, autorizada desde la interfaz y limitada a `tellezoe@hitss.com`.

La pantalla de configuración de credenciales y Outlook tendrá otra acción manual distinta: `Enviar correo de prueba`. Su propósito es comprobar que la cuenta Outlook configurada puede enviar correctamente y su único destinatario será esa misma dirección SMTP configurada, sin CC y con confirmación explícita. No contiene resultados de una prueba PAM y no cambia la restricción de `tellezoe@hitss.com` aplicable al modo de prueba de la automatización.

## Editor enriquecido de correo

La interfaz utilizará `RichTextBox` y `FlowDocument` como modelo nativo de edición. Solo se admitirán capacidades declaradas y testeadas: párrafos, negrita, cursiva, subrayado, listas, tablas, imágenes pegadas y el marcador `{{REQUEST_IDS}}`. Un convertidor C# propio recorrerá el `FlowDocument` y generará un subconjunto permitido de HTML para Outlook; no se aceptará HTML arbitrario ni se afirmará compatibilidad completa.

El documento y sus imágenes se guardan dentro de `mail-settings.dat` protegido con DPAPI CurrentUser. El editor convierte texto con formato, listas, tablas e imágenes pegadas a XHTML saneado. Cada imagen se normaliza a PNG y recibe un Content-ID; el adaptador COM crea un temporal único solo para `Attachments.Add`, configura el CID y lo elimina en el mismo bloque `finally`.

Antes de enviar resultados se consulta el almacén asociado exactamente al `SmtpAddress` configurado por una propiedad privada que contiene el conjunto ordenado exacto de Request ID y se exige también el mismo asunto. La búsqueda recorre Enviados, Bandeja de salida y Borradores. La sesión actual conserva además una huella en memoria para impedir dos intentos concurrentes. Después de `Send()` se vuelve a consultar: Enviados confirma, Bandeja de salida se mueve a Borradores y cualquier estado no demostrado permanece `SubmittedUnconfirmed` o `NotConfirmed`.

## Seguridad

Los secretos deben almacenarse con Windows Credential Manager o DPAPI. No deben aparecer en capturas, excepciones mostradas al usuario, exportaciones de diagnóstico ni en ningún dato persistente.

## Resultados de ejecución

Cada ejecución mantendrá en memoria, sin persistirlo al cerrar la aplicación:

- Modo.
- Fecha y hora.
- Cuentas procesadas.
- Request ID.
- Conflictos.
- Errores.
- Acción final.
- Estado de confirmación del correo.

## Contratos de prueba

Todas las dependencias externas deben tener interfaces sustituibles:

- Reloj.
- Almacenamiento de secretos.
- Sistema de archivos.
- Automatización PAM.
- Outlook.

El framework elegido es MSTest sobre Microsoft Testing Platform. Las pruebas se descubrirán y ejecutarán desde la terminal mediante el runner integrado del proyecto `PamAutomation.Tests`. Las pruebas COM que necesiten apartamento STA utilizarán el soporte STA de MSTest y permanecerán separadas de las pruebas unitarias.

Las pruebas unitarias no deben abrir Edge, PAM ni Outlook. Las pruebas de integración visibles pueden utilizar credenciales reales y el contexto temporal de Playwright cuando exista autorización explícita; deben conservar los mismos controles de seguridad y no deben crear solicitudes ni enviar correos sin una autorización adicional.
