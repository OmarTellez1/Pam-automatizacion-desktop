# Lógica prevista — Pam Automation .NET

## Estado

Este documento describe la lógica objetivo de la aplicación nueva. Ya existen la estructura base, la configuración, el almacenamiento protegido de credenciales, TOTP, navegador automático restringido y oculto, acceso PAM interactivo visible, lectura de Favorites, solicitudes reales, validación segura de formularios, composición HTML saneada, Outlook clásico aislado y la coordinación del flujo PAM desde la interfaz.

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

La composición visual toma como referencia la aplicación anterior: encabezado con marca, selector de acción final en la esquina superior, acción principal azul centrada y tres accesos horizontales de configuración. La ventana principal inicia maximizada y usa controles compactos; las ventanas secundarias reducen tamaño, márgenes y alturas y conservan desplazamiento cuando la resolución lo requiere. La implementación permanece enteramente en controles WPF y recursos XAML, con adaptación al tamaño de la ventana y escalado DPI.

La aplicación comienza en estado incompleto y seguro. `Iniciar automatización` solo puede habilitarse cuando credenciales y solicitud estén configuradas y, si la acción final incluye correo, exista también una configuración de correo válida. Mientras una ejecución esté activa, los controles y comandos de configuración quedan deshabilitados. Cuando el botón está desactivado por configuración incompleta, un aviso separado del estado operativo explica la causa prioritaria: credenciales ausentes, solicitud inválida —incluidas fecha u hora vencidas— o correo pendiente, y dirige al usuario a la sección correspondiente.

La validez temporal no queda congelada al cargar la aplicación. Si al iniciar la fecha y hora guardadas ya pasaron, la aplicación las reemplaza y persiste una sola vez con la primera hora válida de Ecuador: hora actual más el margen obligatorio de 6 minutos, redondeada hacia arriba al minuto completo. Una configuración futura no se modifica, aunque todavía esté demasiado cerca. Después de la inicialización no se vuelve a cambiar silenciosamente el valor guardado.

Toda solicitud debe comenzar al menos 6 minutos después de la hora actual de Ecuador; exactamente 6 minutos es válido y cualquier margen menor se rechaza, incluso al cruzar medianoche. Un `DispatcherTimer` de presentación vuelve a evaluarlo localmente cada minuto, sin red ni acceso al portal, y actualiza el botón y su aviso cuando el margen deja de cumplirse. El comando realiza además una validación fresca antes de abrir la confirmación y otra inmediatamente después de que el usuario responde `Sí`; si el margen cae por debajo de 6 minutos con el cuadro abierto, cancela antes de contactar PAM y muestra una explicación urgente.

El botón de prueba permanece separado del botón real. Solo es visible cuando `testModeEnabled` está activo y toda la configuración requerida es válida; `Editar solicitud` no presenta un control para cambiar ese modo. La interfaz permite guardar credenciales sin volver a mostrar secretos y editar fecha, hora `AM/PM` y duración; la razón se edita en la colección. Al pulsar la acción real se muestra inmediatamente una única confirmación general con la colección activa y la configuración combinada. La hora se presenta en formato de 12 horas con `AM/PM` y, cuando la acción final es correo, aparecen `Para` y `CC` por separado. Si el usuario cancela, no se contacta PAM; si acepta, la preparación y ejecución continúan en segundo plano sin otra confirmación general y los controles permanecen bloqueados hasta terminar o fallar.

La fase 16.5 toma la aplicación JavaScript anterior únicamente como referencia funcional y de claridad. `Editar credenciales` presenta dos tarjetas: PAM muestra si existen secretos, permite revelar temporalmente únicamente lo recién escrito y ejecuta un login visible no destructivo después de guardar; Outlook explica los requisitos, muestra su estado, verifica la cuenta clásica exacta y separa el correo de prueba confirmado. Los mensajes de operación permanecen dentro de la ventana y nunca reproducen secretos ni detalles internos.

La fase 16.8 aporta retroalimentación explícita a esas operaciones asíncronas. Guardar y comprobar PAM, verificar Outlook y enviar el correo de prueba muestran inmediatamente `Solicitud recibida`, un indicador circular animado y los controles deshabilitados. Abrir la ventana no inicia animaciones: cada operación crea e inicia su rotación al comenzar y la detiene al presentar éxito, error o cancelación. Las continuaciones actualizan controles mediante el `Dispatcher`; una excepción inesperada queda registrada, se sustituye por un mensaje seguro y no escapa del evento WPF. Como defensa final, un fallo de interfaz no recuperable produce un cierre ordenado con aviso genérico en lugar de conservar un proceso bloqueado. El correo de prueba informa `El correo de prueba se envió con éxito` únicamente cuando COM acepta `Send()`; `NotConfirmed` se considera fallo visible.

Todos los cuadros que comunican una decisión o información importante usan una única presentación urgente de Windows. El cuadro es modal respecto de la tarea, solicita primer plano y permanece sobre las ventanas normales del escritorio; si PAM Automation estaba minimizada, su ventana propietaria se restaura antes de mostrarlo. Esto cubre la confirmación inicial, duplicados en `Pending`, intervención por certificado, confirmaciones de correo, errores y resultados abiertos por el usuario. El flujo continúa bloqueado hasta recibir una respuesta. Las notificaciones informativas no bloqueantes, como el aviso de actualización, permanecen en la interfaz y no interrumpen el trabajo del usuario.

`Editar solicitud` muestra la fecha completa en español y separa la hora en un selector `hh` de `01` a `12`, un campo editable `mm` válido de `0` a `59` y el selector `AM/PM`; al guardar convierte esa entrada a `TimeOnly` de 24 horas. También separa duración y razón y presenta errores junto al formulario. `Editar envío de correo` usa una ventana compacta, informa la incompatibilidad del nuevo Outlook, muestra la cuenta remitente sin permitir cambiarla allí, explica `Para` y `CC`, conserva las herramientas enriquecidas en una barra adaptable e inserta `{{REQUEST_IDS}}` en el cursor mostrando si existe exactamente una vez.

El editor de correo prioriza CF_HTML al pegar, como hacía la aplicación JavaScript de referencia; utiliza RTF únicamente como alternativa cuando el portapapeles no ofrece HTML. Como WPF no normaliza el portapapeles a una API DOM, `RichClipboardHtmlImporter` reconoce `HTML Format`, `text/html` y variantes `+html`, y decodifica tanto cadenas como bytes o streams UTF-8. Después extrae el fragmento, tolera marcado real mediante `HtmlAgilityPack`, limita tamaño, profundidad y cantidad de nodos, y lo entrega al saneamiento estricto antes de incorporarlo al `FlowDocument`. Se conservan tipografía, tamaño, énfasis, colores, fondo, alineación, enlaces seguros `mailto:`/HTTPS, tablas con sus anchos y combinaciones de celdas e imágenes convertidas a PNG/CID. Se eliminan scripts, elementos activos, recursos remotos y protocolos no permitidos. Toda tabla importada deja un párrafo posterior editable. Al guardar y volver a abrir, `FlowDocumentHtmlConverter` mantiene ese subconjunto seguro y la persistencia DPAPI existente protege tanto el XHTML como las imágenes.

`PamAutomationExecutionService` prepara y vuelve a verificar el conteo y la configuración después de la confirmación inicial, ejecuta las solicitudes, espera la consistencia de `Pending` y produce un `PamExecutionResult` defensivo. Durante la preparación lee primero todas las filas Pending y conserva sus Request ID únicamente en memoria. Si alguna coincide exactamente con la solicitud configurada y una `Favorites` row, la interfaz obliga a elegir entre repetirla o cancelar antes de cualquier envío; esta es una decisión adicional imprescindible, no una repetición de la confirmación general. Si se autoriza repetir, la comparación posterior excluye la línea base y exige una fila nueva por cada solicitud enviada. La vista mantiene únicamente en memoria el conteo de filas, procesadas, conflictos, hora final, Request ID y estado del correo; los errores desconocidos se sustituyen por un texto fijo sin detalles internos. Un fallo después de procesar alguna fila presenta explícitamente el conteo parcial y exige revisar `Pending` antes de reintentar.

Mientras se leen `Favorites` y `Pending`, la portada conserva `Validando Favorites y Pending en segundo plano…`. Cada resultado conocido del formulario se comunica desde Core mediante `PamRequestSubmissionProgress`; la UI cambia a `Se solicitó {Application}.` únicamente cuando PAM devuelve `Submitted`, usando el nombre de la columna `Application`. Un solapamiento con acceso ya aprobado no se anuncia como solicitud creada. El progreso no contiene credenciales, TOTP, Request ID ni otros secretos.

Al completar una ejecución, la interfaz restaura primero el estado inactivo y habilita `Ver resultado de la automatización`; no abre un modal automáticamente ni deja una tarjeta duplicada en la pantalla principal. El botón abre bajo demanda una ventana pequeña con el resumen y los Request ID conservados en memoria. Cuando Outlook acepta `Send()` sin excepción, el usuario recibe un mensaje de envío correcto; la clasificación interna `SubmittedUnconfirmed` continúa disponible para diagnóstico, mientras que borradores, duplicados y fallos siguen diferenciándose.

`Acceder a PAM` es un flujo interactivo separado. Solo requiere credenciales PAM configuradas: abre una ventana del Edge normal del usuario, valida el origen por la barra de direcciones, entrega usuario y contraseña desde Windows Credential Manager, genera TOTP con el reloj HTTP del portal y termina al confirmar que la URL salió de `/login`. No ejecuta ninguna operación de `Favorites`, solicitudes, `Pending` o Outlook. La ventana no pertenece al ciclo de vida de PAM Automation, conserva las capacidades normales de descarga y permanece abierta cuando se cierra la aplicación.

El botón permanece disponible mientras la automatización de solicitudes está en curso porque ambos flujos utilizan navegadores y ciclos de vida separados. Solo se bloquea durante otra apertura interactiva o durante la exportación de diagnóstico. Si se usa concurrentemente, sus mensajes de éxito o fallo no sustituyen el progreso de las solicitudes; un fallo se comunica mediante el aviso urgente independiente.

Este flujo no usa CDP, depuración remota, `userDataDir` ni un perfil dedicado. Windows UI Automation se limita a las ventanas del proceso `msedge`, al origen PAM exacto y a los controles accesibles `Username`, `Password`, `Log In`, `Enter Response` y `Submit`. Si aparece el certificado interno, la aplicación informa los pasos pero no lo omite: espera la decisión manual del usuario y reanuda al detectar el login. La única excepción HTTPS automática es una petición HTTP auxiliar restringida al origen PAM para leer `Date` y calibrar TOTP; no cambia la confianza de Edge.

Después de confirmar el dashboard, el flujo interactivo enfoca de forma comprobable la barra de direcciones de esa ventana PAM, restablece el zoom con `Ctrl+0`, lo reduce cuatro niveles y valida en el árbol accesible de Edge el indicador `67 %`. El ajuste se intenta una sola vez por apertura y nunca se envía si no puede demostrarse el foco correcto. Un fallo de zoom se registra sin secretos y no cierra la sesión PAM ya autenticada.

El cuerpo HTML final hereda Aptos 11 y usa Calibri y Segoe UI como respaldos compatibles con Outlook clásico. Los estilos explícitos importados —incluida la firma— prevalecen por cascada. Las tablas generadas por el editor se serializan al 85% del ancho, centradas y con contenido alineado arriba.

## Persistencia y configuración

### Identidades PAM de la fase 23

La aplicación tendrá un único **usuario principal** y podrá registrar cuentas PAM secundarias. En el primer inicio sin identidad registrada será obligatorio elegir el nombre del principal antes de entrar a la portada. El rol principal será permanente y no eliminable; únicamente podrá cambiarse su nombre visible.

Toda la configuración funcional continuará siendo única y pertenecerá al principal: credenciales para automatización, solicitudes, Favorites, colecciones, Outlook, destinatarios, contenido del correo y acción final. Los botones de edición actuales y `Iniciar automatización` nunca cambiarán de ámbito. Una cuenta secundaria almacenará exclusivamente nombre descriptivo y credenciales PAM completas y solo podrá utilizarse desde `Acceder a PAM`.

Las fases 23.1 y 23.2 ya implementan el alta obligatoria del principal y la administración `Usuarios PAM`. Cada identidad tiene GUID; el principal conserva el destino estable existente y cada secundaria usa un destino propio en Credential Manager. Ningún secreto se persiste en el índice no sensible ni se muestra al volver a editar. El registro admite como máximo cinco identidades en total y se protege mediante DPAPI CurrentUser con escritura atómica.

La fase 23.3 conecta esas identidades exclusivamente con `Acceder a PAM`. Cada pulsación abre un selector, incluso si solo existe el principal. Las identidades sin credenciales se muestran como pendientes y no pueden confirmarse. Cancelar no carga credenciales ni abre el navegador; confirmar resuelve por GUID únicamente la credencial elegida y la mantiene en memoria durante esa apertura interactiva. Esta elección no cambia el usuario de la aplicación ni las credenciales que utiliza la automatización, que continúa exigiendo específicamente el almacén principal.

La fase 23.4 hace idempotente y comprobable el arranque mediante `PrincipalIdentityBootstrapper`. Si el registro ya contiene un principal, no muestra el asistente ni escribe cambios. Si está vacío, solicita únicamente el nombre; cancelar deja el estado intacto. La creación del principal no mueve las credenciales existentes ni modifica solicitudes, Favorites, colecciones, Outlook o correo. Un registro DPAPI alterado o con esquema incompatible se rechaza explícitamente y se conserva para diagnóstico.

La portada muestra la identidad principal como nombre e inicial, pero ese indicador no cambia el ámbito funcional de la aplicación. `Usuarios PAM` siempre lista al principal primero; permite renombrarlo, nunca eliminarlo, y administra el alta, edición de nombre/credenciales y eliminación confirmada de secundarios. Los campos secretos vacíos durante una edición conservan el valor protegido existente. La plantilla de la lista usa enlaces explícitamente unidireccionales porque sus textos de rol y estado son propiedades calculadas de solo lectura.

No habrá importación o exportación de perfiles, configuración por cuenta, intercambio de usuarios entre equipos ni automatización de solicitudes con una cuenta secundaria.

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

No habrá auditoría local ni debugging persistente por defecto. El diagnóstico de desarrollo se activa únicamente con un depurador conectado o `PAM_AUTOMATION_DEVELOPMENT_LOGGING=1`, exige detectar la raíz real del repositorio y escribe archivos diarios bajo `logs` con retención máxima de dos días. En cualquier ejecución, incluida la aplicación publicada, un buffer acotado conserva en memoria los últimos 500 eventos seguros y desaparece con el proceso. Los eventos solo describen etapas, conteos no identificadores y metadatos técnicos; nunca direcciones, credenciales, TOTP, cookies, tokens, Request ID, contenido de correo ni mensajes de excepción potencialmente sensibles.

`Exportar diagnóstico` es la única acción que materializa ese buffer fuera del entorno de desarrollo. El usuario elige explícitamente un ZIP y puede cancelar sin crear archivos. El paquete se construye mediante un temporal junto al destino, se publica atómicamente y contiene manifiesto, entorno, resumen de ejecución y log. El resumen guarda únicamente booleanos, modo, acción, tiempos, conteos y estado de Outlook; Request ID se reduce a su cantidad. El log conserva la plantilla original y solo valores estructurados de una lista permitida, elimina rutas de código y omite el mensaje de las excepciones. Un error recuperable recomienda exportar; un error WPF fatal ofrece hacerlo antes del cierre ordenado.

Las escrituras de `app-settings.json`, `mail-settings.dat` y `outlook-account.dat` son atómicas, incluyen `schemaVersion` y permiten migraciones futuras. Los dos documentos `.dat` se cifran con DPAPI `CurrentUser`; `mail-settings.dat` protege remitente, destinatarios, asunto, XHTML saneado y bytes PNG de imágenes inline. No se escribe configuración dentro de `Program Files` ni junto al ejecutable.

Windows o Playwright pueden utilizar `%TEMP%` para archivos internos transitorios durante el lanzamiento del navegador. No tratar esa ubicación como almacenamiento persistente, no escribir secretos allí y limpiar lo creado por la aplicación cuando sea posible.

## Diferencia entre desarrollo y aplicación publicada

La ausencia de archivos operativos persistentes aplica al ejecutable instalado que utilizan los compañeros. El repositorio de desarrollo sí necesita evidencias para investigar fallos y validar cambios.

Durante desarrollo, pruebas e integración controlada, se podrán crear en la raíz del repositorio:

    results\
    logs\
    artifacts\
    temp\

Estas carpetas estarán excluidas de Git y no se incluirán en la publicación portable. Contendrán únicamente resultados y diagnóstico técnico sin secretos. El perfil de publicación debe usar un almacenamiento en memoria y no debe escribir esas carpetas aunque el ejecutable se inicie desde otra ubicación.

## Versión y aviso de actualización

`PAM Automation.exe` incorpora la versión numérica estable `1.0.2` en los metadatos nativos de Windows y la presenta como `PAM Automation V. 1.0.2`. El recurso Win32 del ejecutable contiene el icono oficial circular `PA` en varias resoluciones. La portada no renderiza ese gráfico: conserva la insignia azul redondeada original con el monograma textual `PA`. Una política visual central aplica a todas las ventanas una barra de título gris oscuro, texto claro y modo inmersivo oscuro mediante DWM; en versiones de Windows que no ofrecen los atributos de color modernos, la llamada falla de forma segura y conserva el estilo administrado por el sistema.

Al iniciar, la ventana se muestra en estado seguro de preparación y bloquea temporalmente sus acciones mientras carga la configuración local. Una vez disponible, la consulta HTTP del manifiesto se inicia como trabajo posterior y no prolonga el tiempo de arranque funcional. El manifiesto público tiene un máximo de 16 KiB, debe contener una versión válida y la dirección exacta del repositorio esperado. Solo una versión numéricamente superior hace visible el banner; las fechas no participan en la decisión.

La distribución autocontenida usa varios archivos preextraídos dentro de un ZIP. Esto evita que cada proceso deba preparar un único binario de aproximadamente 248 MB antes de mostrar WPF. El usuario debe extraer y conservar la carpeta completa. Los hitos `ManagedStartup`, `MainWindowShown`, `ConfigurationReady`, `ReadyForUser` y `UpdateCheckCompleted` registran únicamente etapa y milisegundos transcurridos en el diagnóstico seguro.

El banner únicamente informa y puede descartarse. La aplicación no abre automáticamente el repositorio, no descarga, no reemplaza y no elimina ejecutables. El usuario descarga una nueva versión y elimina manualmente la anterior. Cualquier timeout, error HTTP, TLS, JSON inválido, tamaño excesivo o repositorio inesperado se registra de forma segura y no altera la operación PAM ni muestra detalles técnicos. Nunca se desactiva la validación TLS.

El manifiesto se consulta en la URL `raw` pública de GitHub y debe declarar exactamente `https://github.com/OmarTellez1/Pam-automation-installer` como repositorio. Esa coincidencia evita que un manifiesto válido redirija al usuario hacia otro origen. La integración real de solo lectura confirmó que la red corporativa, TLS, el contenido publicado y la validación completa funcionan con ese origen.

## Autenticación

La fase 5 implementa en Core el cálculo RFC 6238 con HMAC-SHA1, intervalos de 30 segundos y códigos de seis dígitos. La clave de enrolamiento se decodifica como Base32 sin almacenarla; se aceptan mayúsculas, minúsculas y los espacios utilizados para mostrarla en grupos, mientras que símbolos, longitudes, padding o bits residuales inválidos se rechazan con un error que no reproduce la clave.

El resultado TOTP incluye su inicio y expiración UTC y puede evaluar el requisito de al menos ocho segundos restantes. Su representación textual y del depurador está redactada. Los buffers binarios de clave y hash se sobrescriben al terminar el cálculo. El algoritmo no decide la hora por sí mismo: recibe una referencia explícita para conservarse determinista. La fase 6 implementa `IPamClockFactory`, que exige una fecha HTTP válida del portal, incorpora la mitad del tiempo de ida y vuelta y una compensación central de 500 ms por la resolución de un segundo del encabezado, y después avanza con tiempo monotónico; nunca recurre silenciosamente al reloj local.

La fase 8 implementa `PamAuthenticationService` con un puerto del portal ligado a la misma sesión temporal creada para el intento. El servicio mide y entrega el tiempo monotónico de ida y vuelta al reloj, espera asincrónicamente una ventana TOTP segura y permite un segundo código únicamente cuando el primero fue rechazado explícitamente. Las respuestas desconocidas, fallos de conexión y falta de confirmación no se consideran éxito ni provocan reintentos. Esta fase utiliza dobles del portal y no contiene selectores reales.

La fase 9 implementa en Browser `PamAuthenticationPortalFactory` y `PamAuthenticationPortal`, ligados exclusivamente a la página de un `PamBrowserSession`. El adaptador usa los nombres accesibles reales `Username`, `Password`, `Log In`, `Enter Response`, `Submit` y `Return to dashboard`, y reconoce el rechazo TOTP observado sin registrar su contenido. La transición del portal anuncia `Navigation complete.` con `role=alert`; ese anuncio se ignora expresamente porque no representa un rechazo de credenciales. Los timeouts o estados ambiguos tampoco se convierten en rechazos explícitos.

La integración real permanece detrás de `PAM_AUTOMATION_ALLOW_REAL_LOGIN=LOGIN_ONLY`, recupera los valores únicamente desde Windows Credential Manager y ejecuta Edge temporal en segundo plano. La validación autorizada confirmó el dashboard y cerró el contexto; no navegó a Favorites, no creó solicitudes y no abrió Outlook.

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

La fase 7 implementa `IPamBrowserSessionFactory` e `IPamBrowserSession`. Cada sesión crea un navegador `headless` y un contexto nuevo no persistente, bloquea service workers e instala una ruta que aborta solicitudes HTTP(S) cuyo esquema, host o puerto efectivo no coincidan con el origen PAM autorizado. La excepción HTTPS queda limitada a ese contexto. La respuesta pública de navegación contiene únicamente URI final, código de estado y el encabezado HTTP `Date`; no enumera ni expone cookies, tokens u otras cabeceras.

No se debe incluir Chromium. La aplicación debe utilizar la opción 1: lanzar el Edge instalado mediante el canal msedge y crear un contexto temporal de Playwright. No debe seleccionar, reutilizar ni modificar el perfil existente del usuario, sus cookies, extensiones o sesiones. No utilizar LaunchPersistentContext, userDataDir ni conexión CDP a un navegador existente. El aislamiento es interno de Playwright y no debe presentarse como una ventana de incógnito.

Por decisión posterior, la comprobación de credenciales y la automatización real temporal utilizan Edge `headless`; no muestran la navegación, pero conservan las mismas restricciones, selectores y ciclo de vida. `Acceder a PAM` es la única excepción visible y utiliza el Edge normal descrito anteriormente.

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

Cada fila Pending se mantiene en memoria y redacta su representación textual. Para asociarla con una `Favorites` row se exige la misma fecha y hora completas y la misma combinación `System + Account`; luego se validan expresamente `Application`, `Expires` calculado desde la duración y `Description`. La ejecución real de 13 solicitudes demostró que Pending no siempre conserva el nombre completo de `Application`: puede añadir versión y destino o sustituir el sufijo de nodo. La comparación admite únicamente las familias observadas `NOSQLBOOSTER`, `PLSQL`, `PuTTY` y `WinSCP`; para cualquier otra aplicación conserva igualdad o el sufijo estructurado anterior. Si dos filas de la misma familia bajo un mismo `System + Account` producen una asociación no única, informa ambigüedad en lugar de elegir por posición. La comparación devuelve coincidencias ordenadas y faltantes explícitos. Tras una creación puede repetirse la lectura hasta seis veces con dos segundos de espera para cubrir consistencia eventual.

`testModeEnabled` se conserva como compatibilidad interna para configuraciones de desarrollo, pero `Configurar solicitud` no permite modificarlo. `Iniciar prueba` solo será visible cuando ese valor ya sea verdadero y la configuración de credenciales y solicitud esté completa. Si la acción final es correo, también deberá existir una configuración de correo válida, pero los destinatarios guardados no se usarán: el correo de prueba se enviará exclusivamente a `tellezoe@hitss.com`, sin CC, con asunto prefijado por `[PRUEBA]` y después de una confirmación que muestre el destinatario exacto.

El modo real debe recorrer todas las páginas necesarias para resolver exactamente las filas de la colección activa y las solicitudes `Pending` relevantes. Debe validar `System`, `Application`, `Account`, `Request Start Date`, `Expires` y `Description` antes de aceptar una reconciliación.

## Colecciones personalizadas posteriores a la migración

Toda automatización real se ejecuta mediante una colección activa. Cada colección conserva únicamente su nombre, razón y claves exactas `System + Application + Account`; fecha, hora y duración son la configuración temporal común editada desde `Editar solicitud` después de escoger la colección.

La fase 20.1 incorpora el primer límite funcional: la portada abre una ventana propia `Colecciones`. Antes de la primera sincronización muestra un estado vacío y `Obtener solicitudes favoritas`; después de cargar un snapshot protegido muestra cada fila únicamente como `Application - IP Address` y cambia la acción a `Actualizar solicitudes favoritas`. La actualización autentica mediante la sesión temporal oculta, recorre todas las páginas de `Favorites`, reemplaza el snapshot completo y comunica solamente conteos de filas nuevas y eliminadas.

El catálogo conserva internamente `System + Application + Account` como identidad y `IP Address` como metadata viva. Se persiste en `%LOCALAPPDATA%\PamAutomation\config\favorite-catalog.dat`, con `schemaVersion = 1`, DPAPI `CurrentUser` y reemplazo atómico. Una carga local no afirma que PAM haya sido comprobado en ese momento; cada actualización correcta convierte el nuevo snapshot en la única fuente para la siguiente reconciliación.

La colección guardará claves lógicas completas `System + Application + Account`, nunca índices del grid. `Favorites` continuará siendo la fuente de verdad: cada apertura o ejecución actualizará todas sus páginas y clasificará las filas guardadas como `Available`, `Missing` o `Ambiguous`. Las filas `New in Favorites` no se agregarán automáticamente ni se reemplazarán identidades por coincidencias aproximadas.

La fase 20.2 permite crear y editar varias colecciones. Cada colección personalizada tiene nombre, razón y cualquier subconjunto no vacío del catálogo. Un toggle por colección selecciona cuál se utilizará y desactiva implícitamente cualquier selección anterior; solo puede existir una colección activa.

La fase 20.4 elimina `UseCollectionsForAutomation` y el toggle de la portada. Al obtener Favorites por primera vez se crea y activa `All favorites`; contiene exactamente todas las filas del snapshot. Cada actualización posterior agrega o retira automáticamente en esa colección las claves que cambiaron en el portal. Su nombre y selección son administrados por la aplicación y el usuario solo puede editar su razón.

La fase 20.5 permite eliminar colecciones creadas por el usuario desde un botón rojo de papelera. La acción exige confirmación urgente y aclara que no modifica `Favorites` en PAM. `All favorites` no muestra esa acción y el servicio vuelve a rechazar cualquier intento directo de eliminarla. Si se borra la colección activa, `All favorites` pasa a ser la colección seleccionada; eliminar una colección inactiva conserva la selección existente. El cambio se persiste atómicamente dentro del mismo documento DPAPI y no requiere una migración de esquema.

`Editar solicitud` exige una colección activa y solo modifica `StartDate`, `StartTime` y `SessionHours`. Al confirmar o ejecutar, la aplicación combina esos valores con `RequestReason` de la colección activa. La confirmación inicial identifica la colección y la autorización de un solo uso limita el envío a sus claves exactas.

Antes del preflight de `Pending`, la ejecución con colección vuelve a leer todas las páginas de `Favorites` y resuelve cada clave exacta. Antes del primer envío, `PamRequestRunAuthorization` vuelve a aplicar la misma selección sobre la lectura realizada dentro de la sesión de envío. Si falta una identidad, cambia el total o no coincide la configuración confirmada, no se pulsa `Submit Request`. La reconciliación posterior en `Pending` continúa limitada a las cuentas realmente enviadas.

Las colecciones se guardan separadamente en `%LOCALAPPDATA%\PamAutomation\config\favorite-collections.dat` mediante DPAPI `CurrentUser`, `schemaVersion = 2` y reemplazo atómico. El lector migra el esquema 1 conservando nombre, razón, selección y colección activa, pero descarta la antigua configuración temporal por colección. Si una colección personalizada activa queda vacía durante la reconciliación, `All favorites` pasa a ser la activa.

La entrada nativa WPF presenta fecha, hora de 12 horas con selector `AM/PM` y duración operativa en horas. La razón se obtiene de la colección activa. La conversión temporal se realiza en el dominio y el adaptador comprueba el valor normalizado por PAM. El recorrido confirmado, sus selectores e invariantes se mantienen en `PAM_NAVIGATION.md`.

El conflicto `PSPortal.Requests.Policies.RemoteAppAccesssAlreadyAvailable` se registra y permite continuar. Otros errores deben detener el proceso.

## Outlook

La fase 14 integra Outlook clásico mediante COM detrás de `IOutlookClassicClient`. Verifica una única cuenta por `SmtpAddress`, valida la cuenta con un mensaje temporal que siempre se descarta, asigna `SendUsingAccount` antes de `Send()` y crea un mensaje nuevo por operación. La ejecución COM se realiza en STA y las referencias se liberan explícitamente.

La fase 16.7 activa `Outlook.Application` mediante su `ProgID` y despacho COM tardío. No carga la PIA antigua `Microsoft.Office.Interop.Outlook` ni depende de `office.dll` o `stdole.dll` en la salida administrada. Se conservan el seam interno, los adaptadores propietarios de objetos COM, las constantes numéricas oficiales del modelo de Outlook, la ejecución STA y la liberación explícita; no se invocan PowerShell.

Ante una operación de Outlook, el adaptador intenta primero obtener la instancia clásica ya publicada en la Running Object Table. Si no existe, usa la activación COM normal. Exclusivamente cuando Windows devuelve `0x80080005` (`CO_E_SERVER_EXEC_FAILURE`), inicia `outlook.exe` mediante ShellExecute y el registro de aplicaciones de Windows, espera tres segundos en el hilo STA dedicado y reintenta una vez. No utiliza una ruta fija, no cierra Outlook y no eleva permisos. Si la recuperación falla, Core expone `ApplicationUnavailable` y la configuración muestra instrucciones específicas sin revelar detalles COM.

Los modelos que contienen dirección, asunto, HTML, destinatarios o Request ID redactan su representación textual. Invocar `Send()` solo produce `SubmittedUnconfirmed`; la entrega no se marcará confirmada hasta que una fase posterior la compruebe en Enviados. Un fallo COM devuelve un resultado seguro sin exponer el contenido.

La fase 15 incorpora `OutlookHtmlComposer` como límite determinista previo a Outlook. Solo acepta fragmentos XHTML bien formados generados por la aplicación, con DTD prohibidos y un máximo de 8 millones de caracteres. Conserva párrafos, encabezados, negrita, cursiva, subrayado, listas, tablas, estilos explícitamente permitidos e imágenes locales referenciadas mediante `cid:`; elimina scripts, estilos activos, formularios, marcos, objetos, eventos, vínculos y recursos externos. No acepta HTML web arbitrario.

La plantilla saneada debe contener `{{REQUEST_IDS}}` exactamente una vez dentro del texto. La composición valida Request ID numéricos y únicos, mantiene el orden recibido y los inserta separados por `<br />`. Antes de crear COM, `OutlookMessageValidator` exige que el cuerpo final sea idéntico a su versión saneada, que ya no contenga el marcador y que cada ID esperado aparezca como token numérico completo. `FlowDocumentHtmlConverter` produce ese XHTML y no puede evitar esta validación.

La integración COM debe ejecutarse en STA, liberar referencias y no enviar mensajes durante pruebas automáticas.

Las pruebas unitarias e integraciones automatizadas nunca enviarán correo. El envío de un correo de modo de prueba es una acción manual distinta, autorizada desde la interfaz y limitada a `tellezoe@hitss.com`.

La pantalla de configuración de credenciales y Outlook tendrá otra acción manual distinta: `Enviar correo de prueba`. Su propósito es comprobar que la cuenta Outlook configurada puede enviar correctamente y su único destinatario será esa misma dirección SMTP configurada, sin CC y con confirmación explícita. No contiene resultados de una prueba PAM y no cambia la restricción de `tellezoe@hitss.com` aplicable al modo de prueba de la automatización.

## Editor enriquecido de correo

La interfaz utiliza `RichTextBox` y `FlowDocument` como modelo nativo de edición. El usuario pega texto y firma ya compuestos y dispone de `Insertar {{REQUEST_IDS}}`. Debido a la falta de fidelidad consistente de las tablas procedentes de portapapeles externos, la interfaz advierte que no deben pegarse y ofrece `Insertar tabla 2×2`: divide el párrafo en la posición del cursor, inserta una tabla 30/70 con las filas `Solicitudes de PAM` y `Motivo del acceso`, mueve allí el único marcador y deja un párrafo editable después. No permite anidar una tabla dentro de otra. Un convertidor C# propio recorre el `FlowDocument` y genera un subconjunto permitido de HTML para Outlook. No se acepta HTML arbitrario ni se afirma compatibilidad completa.

El documento y sus imágenes se guardan dentro de `mail-settings.dat` protegido con DPAPI CurrentUser. El editor convierte texto con formato, listas, tablas e imágenes pegadas a XHTML saneado. Cada imagen se normaliza a PNG y recibe un Content-ID; el adaptador COM crea un temporal único solo para `Attachments.Add`, configura el CID y lo elimina en el mismo bloque `finally`.

Antes de enviar resultados se consulta el almacén asociado exactamente al `SmtpAddress` configurado por una propiedad privada que contiene el conjunto ordenado exacto de Request ID y se exige también el mismo asunto. La búsqueda recorre Enviados, Bandeja de salida y Borradores. La sesión actual conserva además una huella en memoria para impedir dos intentos concurrentes.

Después de `Send()` la aplicación espera y consulta de forma no destructiva hasta diez veces, con cuatro segundos entre lecturas. Los fallos transitorios de una lectura COM se toleran dentro de ese límite. Enviados confirma el resultado; Bandeja de salida se reporta como procesamiento pendiente y nunca se mueve automáticamente; Borradores indica que Outlook no completó el envío. Si Outlook todavía no expone el mensaje al terminar la ventana acotada, se conserva `SubmittedUnconfirmed` o `NotConfirmed` según el resultado original. El envío no se reintenta automáticamente porque repetir una operación cuyo estado externo es incierto podría crear correos duplicados.

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

Las pruebas unitarias no deben abrir Edge, PAM ni Outlook. Las pruebas de integración pueden utilizar credenciales reales y el contexto temporal `headless` de Playwright cuando exista autorización explícita; deben conservar los mismos controles de seguridad y no deben crear solicitudes ni enviar correos sin una autorización adicional. Las pruebas del botón interactivo sí pueden mostrar Edge porque validan deliberadamente el flujo visible separado.
