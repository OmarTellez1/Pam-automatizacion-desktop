# Lógica prevista — Pam Automation .NET

## Herramienta de publicación versionada

`Publish-Portable.ps1` forma parte del repositorio. Conserva el empaquetado original del perfil `Portable-x64`: EXE y dependencias juntos, con `.playwright` intacto y carpeta base incluida en el ZIP. Lee la versión del proyecto, ejecuta restauración/compilación/pruebas/publicación, comprueba dependencias y metadata y genera SHA-256 del ZIP. `-ReplaceExisting` solo sustituye los artefactos de esa versión dentro de `artifacts/publish/final`. No cambia versiones ni publica en GitHub.

`Directory.Build.targets` exige que el script exista antes de compilar, excepto en compilación de diseño. El script y este contrato deben integrarse juntos en otras ramas. Una rama anterior a su incorporación no hereda automáticamente estos archivos ni esa comprobación. El script es de uso manual del usuario; los agentes validan desde `dotnet` directamente según AGENTS.md.

## Navegación de Favorites por solicitud — 1 de octubre de 2026

La preparación compartida por prueba y ejecución reutiliza la vista únicamente con URL de Accounts, categoría Favorites seleccionada, Browse by Category y filas visibles, sin panel Submit Request abierto. Da hasta dos segundos al retorno del grid tras una operación completada. No conserva filas ni posiciones: relee un snapshot estable y localiza Access mediante las tres celdas exactas System + Application + Account, incluso si el orden cambia antes del clic.

Busca primero en la página actual; si no encuentra la identidad, recorre las restantes con Next page y, al llegar al final, First page. Comprueba la categoría y la paginación durante la búsqueda. Una cuenta ausente puede deberse a una vista obsoleta o filtrada: permite recargar Accounts y Favorites una vez y exige encontrar la identidad exacta. Los duplicados observados bloquean sin elegir arbitrariamente.

Los timeouts y errores del navegador al navegar o abrir el formulario admiten esa misma recuperación única. El presupuesto compartido es de 60 segundos y cada control tiene un límite de 10 segundos; la cancelación del usuario no se reintenta. La recuperación termina antes de llenar campos: errores de calendario, verificación del formulario, rechazos o respuestas inciertas de Submit no vuelven a ejecutar esta preparación automáticamente. La reconciliación actual no cambia.

## Estado

Este documento describe la lógica objetivo de la aplicación nueva. Ya existen la estructura base, la configuración, el almacenamiento protegido de credenciales, TOTP, navegador automático restringido y oculto, acceso PAM interactivo visible, lectura de Favorites, solicitudes reales, validación segura de formularios, composición HTML saneada, Outlook clásico aislado y la coordinación del flujo PAM desde la interfaz.

## Fase 27 — Acumulado de una sola colección

El modo se presenta como un interruptor con el estilo común, a la derecha de la acción final. El cambio de presentación conserva el comando, los estados y los bloqueos del acumulado.

El estado deshabilitado se representa en gris, sin cambiar la preferencia de Outlook. La etiqueta de acumulación dispone de altura automática para evitar recortes.

**Implementada.** El modo opcional de acumulación suprime las acciones finales automáticas y reúne solo los IDs nuevos reconciliados de ejecuciones completas de la misma colección. La identidad estable vincula el lote; no se puede cambiar de colección, desactivar el modo ni eliminar la colección vinculada con IDs pendientes. Envío confirmado o descarte explícito libera el vínculo. No se agregan resultados parciales ni IDs antiguos automáticamente; los duplicados exactos en este modo bloquean la ejecución sin envío inmediato de reutilización. El comportamiento habitual fuera del modo se conserva.

El botón manual usa el correo de la colección vinculada, remitente principal y firma vigente, congelados junto con los IDs después de confirmar. El estado incierto de Outlook conserva el lote sin repetir `Send()`. Descartar nunca cancela solicitudes PAM ni retira correos. Los resultados solo viven en memoria y el cierre normal exige advertir su pérdida. Especificación y criterios completos en `Fases.md`, fase 27.

`RequestIdAccumulator` admite lotes atómicos únicamente con todas las filas procesadas y un ID numérico por solicitud nueva sin conflicto; rechaza modos de prueba/reutilización, resultados parciales y correo ya ejecutado. Deduplica entre lotes y mantiene el orden. Mientras envía/observa bloquea altas, descarte y edición de la colección vinculada. Ante fallo seguro vuelve a Ready; si existe o puede existir un correo queda en AwaitingConfirmation y solo admite observación con la instantánea original. Nuevas ejecuciones permanecen bloqueadas hasta resolver o descartar ese estado.

La actualización de Favorites conserva el GUID activo vinculado incluso sin accesos disponibles. Esa colección vacía admite correcciones de correo pero no solicitudes nuevas. Las otras colecciones siguen administrables fuera de una operación en curso. La preferencia de acción final no se persiste de nuevo por activar/desactivar acumulación. El cierre normal usa No como opción predeterminada; el fin de sesión de Windows omite el diálogo. Validación local: 119 pruebas relacionadas aprobadas, sin operaciones reales.

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

`PamAutomationExecutionService` prepara y vuelve a verificar el conteo y la configuración después de la confirmación inicial, ejecuta las solicitudes, espera la consistencia de `Pending` y produce un `PamExecutionResult` defensivo. Durante la preparación lee primero todas las filas Pending y conserva sus Request ID únicamente en memoria. Si alguna coincide exactamente con la solicitud configurada y una fila de `Favorites`, nunca permite crear otra solicitud idéntica. Cuando existe un conjunto completo y no ambiguo para toda la colección y Outlook está habilitado, la interfaz permite reutilizar esos Request ID para el correo o cancelar. Ante coincidencias parciales, ambiguas o sin correo habilitado, solo permite cancelar e indica cambiar la fecha u hora de inicio, la duración o el motivo. Si se reutiliza, no se ejecuta `Submit Request`: solo se aceptan todos los IDs previamente validados de la línea base y se envían mediante la configuración y protecciones normales de Outlook. La vista mantiene únicamente en memoria el conteo de filas, procesadas, conflictos, hora final, Request ID y estado del correo; los errores desconocidos se sustituyen por un texto fijo sin detalles internos. Un fallo después de procesar alguna fila presenta explícitamente el conteo parcial y exige revisar `Pending` antes de reintentar.

## Política de espera adaptativa

`PamOperationTimingPolicy` es la fuente única de los tiempos operativos. Cada observación comienza inmediatamente y continúa apenas se confirma el estado correcto; los límites máximos no son demoras mínimas. El portal oculto admite hasta 60 segundos para navegación o controles, consulta cada 200 ms y limita cada lectura DOM transitoria a dos segundos. Los formularios tienen hasta 15 segundos para conservar todos los valores autorizados. Favorites y Pending solo aceptan snapshots completos y estables; Pending dispone de diez refrescos de consistencia eventual y cinco reescaneos si el total cambia durante la paginación.

Outlook se observa inmediatamente después de enviar y luego, solo mientras haga falta, cada cuatro segundos hasta quince veces. Su arranque COM se comprueba primero sin espera y, si continúa iniciándose, una vez por segundo hasta quince intentos. El Edge interactivo usa 60 segundos para iniciar y responder; la ventana de tres minutos se reserva para que el usuario resuelva manualmente el certificado corporativo.

Solo pueden repetirse observaciones, navegación segura, lectura de grids y comprobaciones idempotentes. Las credenciales se envían una vez, cada TOTP se envía una vez dentro de la política controlada existente, `Submit Request` se pulsa una vez por autorización y Outlook `Send()` se llama una vez. Un rechazo explícito o error definitivo nunca se transforma en reintento automático.

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

El diálogo `Acerca de` es únicamente informativo: lee la versión pública del ensamblado y muestra el autor, sin acceder a configuración, credenciales, navegador, Outlook o red.

La portada no muestra un indicador permanente de colección activa y presenta un único aviso cuando la configuración bloquea el inicio. El texto gris de estado permanece oculto en reposo y se muestra solo durante una ejecución, para comunicar validación de Favorites/Pending y el avance de cada solicitud sin duplicar advertencias persistentes. La colección seleccionada continúa formando parte de la validación y de la confirmación previa a la ejecución.

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

La configuración de correo pertenece a cada colección y se guarda en:

    %LOCALAPPDATA%\PamAutomation\config\favorite-collections.dat

Cada colección contiene Para, CC, asunto, XHTML con un único marcador REQUEST_IDS e imágenes inline. El documento usa esquema 3 y DPAPI CurrentUser. La firma común del usuario principal se guarda separadamente en `mail-signature.dat`, esquema 1; la cuenta seleccionada en Outlook clásico se mantiene separada:

    %LOCALAPPDATA%\PamAutomation\config\outlook-account.dat

`outlook-account.dat` contiene únicamente `schemaVersion` y la dirección SMTP exacta, también protegidos con DPAPI `CurrentUser` y escritura atómica. Cuando una instalación existente solo tiene `mail-settings.dat`, la aplicación migra el remitente a este documento sin perder la plantilla.

La única persistencia propia de la aplicación será:

    %LOCALAPPDATA%\PamAutomation\config\
    ├── app-settings.json
    ├── favorite-collections.dat
    ├── mail-signature.dat
    ├── mail-settings.dat (legado conservado)
    └── outlook-account.dat

No persistir `results`, `logs`, `artifacts`, `temp`, `last-run`, `last-email` ni `delivery-state`. El resultado de una ejecución se representará mediante un modelo en memoria que contenga Request ID, conflictos, estado y errores. La interfaz lo mostrará mientras la aplicación permanezca abierta.

Si la acción final es correo, la aplicación debe preparar, validar y enviar el mensaje dentro de la misma ejecución. La protección inmediata contra duplicados utilizará el estado en memoria de la ejecución. Antes de enviar, también se consultarán Enviados, Bandeja de salida y Borradores de Outlook para detectar un mensaje con el mismo asunto y el mismo conjunto exacto de Request ID. De este modo se conserva protección entre reinicios sin crear `last-email`, `delivery-state` ni otro historial local.

No habrá auditoría local ni debugging persistente por defecto. El diagnóstico de desarrollo se activa únicamente con un depurador conectado o `PAM_AUTOMATION_DEVELOPMENT_LOGGING=1`, exige detectar la raíz real del repositorio y escribe archivos diarios bajo `logs` con retención máxima de dos días. En cualquier ejecución, incluida la aplicación publicada, un buffer acotado conserva en memoria los últimos 500 eventos seguros y desaparece con el proceso. Los eventos solo describen etapas, conteos no identificadores y metadatos técnicos; nunca direcciones, credenciales, TOTP, cookies, tokens, Request ID, contenido de correo ni mensajes de excepción potencialmente sensibles.

`Exportar diagnóstico` es la única acción que materializa ese buffer fuera del entorno de desarrollo. El usuario elige explícitamente un ZIP y puede cancelar sin crear archivos. El paquete se construye mediante un temporal junto al destino, se publica atómicamente y contiene manifiesto, entorno, resumen de ejecución y log. El resumen guarda únicamente booleanos, modo, acción, tiempos, conteos y estado de Outlook; Request ID se reduce a su cantidad. El log conserva la plantilla original y solo valores estructurados de una lista permitida, elimina rutas de código y omite el mensaje de las excepciones. Un error recuperable recomienda exportar; un error WPF fatal ofrece hacerlo antes del cierre ordenado.

Las escrituras de configuración son atómicas e incluyen `schemaVersion`. Los documentos `.dat` se cifran con DPAPI `CurrentUser`. `mail-settings.dat` solo se lee para migrar el correo y, si hace falta, el remitente legado; no se vuelve a escribir desde la interfaz ni se usa como fallback. No se escribe configuración dentro de `Program Files` ni junto al ejecutable.

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

La selección de fecha compartida entre prueba y ejecución observa un único calendario visible. Obtiene mes y año de la cabecera nativa de Flatpickr y exige coherencia con su grilla en dos lecturas. Selecciona el mes directamente por el valor exacto de la opción; las flechas permiten cruzar de año. La grilla completa se exige únicamente como compatibilidad si faltan ambos controles de cabecera. Espera cada cambio de mes; nunca repite navegación por no encontrar todavía el día. Excluye celdas de meses adyacentes, fechas deshabilitadas y calendarios ocultos. Tras elegir confirma el valor exacto de `Start Date` en dos lecturas. Conserva la comprobación integral posterior del formulario y la inmediatamente previa a Submit. `PamCalendarSelectionException` distingue apertura, lectura, transición, fecha no disponible y selección no retenida, entre otros fallos. El diagnóstico registra categorías, fechas, conteos y observaciones acotadas. Los límites de ocho segundos por lectura o transición y 45 por selección evitan recorridos indefinidos, sin relajar las validaciones ni reintentar envíos.

Solo durante la lectura inicial, si desaparece el calendario que ya se detectó abierto, se permite un clic de recuperación dentro del mismo plazo de lectura. Se comprueba que el control `Start Date` original sigue visible y habilitado, que la búsqueda del campo es única y que no hay un calendario abierto antes de actuar. No se hace clic sobre un campo de reemplazo. Si el mes no se confirma, permanece `MonthUnreadable`, con el intento de reapertura, su fallo o el bloqueo registrados en el historial. Cuando se recupera, el evento `CalendarReopened` queda correlacionado con el intento y formulario. Los cambios de mes y año conservan su espera sin reapertura.

Cada formulario conserva y comprueba fecha, hora, duración, razón y `Application Session`, luego ejecuta `Cancel > Discard Changes` y espera su cierre. El resultado permanece en memoria y fija `SubmittedCount` en cero. La integración explícita `VALIDATE_AND_DISCARD` validó y descartó 14 de 14 formularios en Edge visible con `submitted = 0`; no abrió Outlook ni persistió identidades o contenido.

La fase 12 implementa el modo real mediante un contrato distinto, `IPamRequestSubmissionPortal`. `PamRequestSubmissionService` valida primero la configuración, obtiene un snapshot nuevo de todas las `Favorites` rows y exige que coincidan tanto su conteo como la configuración completa con `PamRequestRunAuthorization`. Si cualquiera cambió desde la confirmación, termina antes de crear el portal de envío. Esta autorización es de una ejecución y no sustituye la confirmación visible que deberá conectar la interfaz en la fase 16.

Antes de cada efecto irreversible, Browser vuelve a localizar la `Favorites` row por `System + Application + Account`, abre `Access > Submit Request`, completa el formulario y verifica inmediatamente fecha, hora, duración, razón y `Application Session`. Solo entonces pulsa el botón final y espera específicamente el `POST /api/ps-portal/requests`. Una respuesta 2xx produce `Submitted`; el error 400 con código exacto `PSPortal.Requests.Policies.RemoteAppAccesssAlreadyAvailable` produce `ApprovedAccessOverlap` y permite seguir; cualquier otro estado o contenido detiene el proceso mediante un error seguro que no incluye el payload.

Cuando la verificación del formulario agota su espera, un error tipado distingue la etapa `AfterFill` o `BeforeSubmit`, campos divergentes y lecturas transitoriamente fallidas. El diagnóstico exportado incluye el último conjunto completo de valores esperados y observados de fecha, hora, días, horas, minutos y `Application Session`; si no hubo lectura completa lo indica expresamente. Del motivo solo conserva longitud y presencia de discrepancia. El exportador admite únicamente formatos acotados para estos valores, reemplaza texto inesperado del portal y nunca incluye contraseñas, TOTP, cookies, tokens ni texto libre del motivo.

La cancelación se comprueba antes de cada solicitud. Una vez pulsado `Submit Request`, el adaptador espera una respuesta acotada sin convertir la cancelación tardía en un estado falsamente conocido: un timeout se trata como fallo porque el efecto remoto podría ser ambiguo. Los resultados por fila se copian y conservan solo en memoria. El opt-in destructivo de desarrollo se limita a un intento y no forma parte de la suite ordinaria.

La fase 13 implementa `PamPendingService` sobre un contrato exclusivamente de lectura. Browser navega a `Requests`, exige un único filtro `Status`, selecciona exactamente `Pending` y espera que el menú cierre y el control vuelva a estar habilitado antes de aceptar el grid. Si el portal ofrece `items per page`, elige su mayor opción numérica —actualmente 200— y confirma el número de páginas calculado desde el contador. Los lotes que caben se leen en una sola página; el recorrido accesible desde la primera página permanece como respaldo, con un límite de 500 páginas y reescaneos acotados si el contador cambia. Un estado vacío es válido; filas incompletas, Request ID no numéricos o duplicados y cambios de página incoherentes son fallos bloqueantes.

Durante un cambio de página, PAM puede actualizar el paginador antes de terminar de renderizar todas las celdas. Cada observación obtiene todas las filas y sus `td[data-column-id]` mediante una sola evaluación síncrona del DOM del portal; la conversión y validación permanecen en C#. Esto produce una fotografía coherente y evita una comunicación Playwright independiente por cada celda. Una columna ausente, repetida, todavía vacía o un valor inválido se consideran transitorios únicamente dentro de la ventana acotada de estabilización; se vuelve a observar la página sin avanzar ni aceptar datos parciales. La página solo se acepta después de dos capturas masivas completas e iguales. Si no alcanza esa estabilidad antes del límite general, continúa siendo un fallo bloqueante.

Cada fila Pending se mantiene en memoria y redacta su representación textual. La fase 24 confirmó `System` y `Account` como campos estables entre Favorites y Pending; `Application` puede cambiar de nomenclatura y no participa en esta asociación. El comparador filtra por `Request Start Date`, `Expires` calculado desde la duración y `Description`, agrupa como multiconjunto por `System + Account` y exige el mismo número de filas nuevas que solicitudes enviadas. Claves repetidas son válidas. Una cantidad superior indistinguible es ambigua; una cantidad inferior permanece faltante y permite repetir la lectura hasta seis veces con dos segundos de espera para cubrir consistencia eventual.

`testModeEnabled` se conserva como compatibilidad interna para configuraciones de desarrollo, pero `Configurar solicitud` no permite modificarlo. `Iniciar prueba` solo será visible cuando ese valor ya sea verdadero y la configuración de credenciales y solicitud esté completa. Si la acción final es correo, también deberá existir una configuración de correo válida, pero los destinatarios guardados no se usarán: el correo de prueba se enviará exclusivamente a `tellezoe@hitss.com`, sin CC, con asunto prefijado por `[PRUEBA]` y después de una confirmación que muestre el destinatario exacto.

El modo real debe recorrer todas las páginas necesarias para resolver exactamente las filas de la colección activa y las solicitudes `Pending` relevantes. Antes del envío usa `System + Application + Account` para localizar el formulario exacto en Favorites. Después del envío valida `System`, `Account`, `Request Start Date`, `Expires` y `Description`; `Application` queda fuera de la reconciliación de Pending.

### Contrato de fase 24 para Pending

La fase 24 sustituyó la asociación anterior basada en familias de `Application`. El catálogo puede contener aproximadamente 383 accesos diferentes, pero cada ejecución procesa únicamente la colección activa; no se mantiene una lista de esas opciones ni una regla por producto. `Application` sigue formando parte de la identidad exacta de Favorites y colecciones, necesaria para volver a abrir el formulario correcto, pero no se utiliza para relacionar Favorites con Pending.

Antes de enviar, la aplicación toma una línea base en memoria de todos los Request ID que ya existen en Pending. La detección previa de solicitudes iguales compara `System + Account + Request Start Date + Expires + Description`. Si encuentra coincidencias, bloquea la creación de solicitudes idénticas. Un conjunto completo y no ambiguo puede reutilizarse exclusivamente para enviar el correo cuando Outlook está habilitado; la otra opción es cancelar. Una coincidencia parcial o ambigua obliga a cancelar y cambiar al menos la fecha u hora de inicio, la duración o el motivo antes de reintentar.

Después de enviar, se eliminan del universo candidato todos los Request ID de la línea base. De las filas restantes solo pertenecen al lote aquellas cuyos `System`, `Account`, inicio, expiración y descripción coinciden con solicitudes realmente enviadas. La asociación es por cantidades: claves repetidas `System + Account` son válidas y deben producir el mismo número de filas nuevas. Una lectura parcial se reintenta dentro de una ventana acotada; filas nuevas ajenas se ignoran y un exceso indistinguible se informa como ambigüedad. Nunca se asigna un Request ID por posición, orden del grid, texto truncado, familia, prefijo o sufijo de `Application`.

El preflight y la reconciliación posterior comparten los mismos bucles privados de recuperación en `PamPendingService`. Un timeout se clasifica como `PortalUnavailable`; los cambios de contador, paginación o falta de estabilidad se clasifican explícitamente como `SnapshotUnstable`. Solo esas categorías permiten volver a navegar a Pending y, tras dos intentos fallidos, cerrar y reconstruir exclusivamente la sesión autenticada de lectura. Hay como máximo dos sesiones por operación; el preflight persistente agota cuatro lecturas. Los refrescos de consistencia eventual conservan su límite propio. La línea base y el lote esperado se copian antes de la reconciliación y nunca se reemplazan al autenticar otra sesión.

`InvalidRequest` deja de ser recuperable: filas que siguen inválidas después de la ventana local de observación, estructuras permanentes y errores no clasificados detienen el flujo. También terminan sin reintento `DuplicateRequestId`, `AmbiguousIdentity`, `FieldMismatch` y cancelaciones. La autenticación está fuera del bloque de reintento de lectura; sus fallos no provocan otro login. Los servicios de envío PAM y correo no participan en esta recuperación.

Antes de la primera lectura de Pending, cada preparación inicia una traza de esquema 3 en memoria con etapa `Preflight`, solicitudes esperadas y línea base todavía nula. Una lectura completa actualiza defensivamente esa línea base sin borrar los intentos ni recuperaciones anteriores. La etapa posterior pasa a `Reconciliation` y conserva la línea base, decisión y conteo enviado. Cada lectura registra etapa, sesión, intento de comparación, intento de lectura y código de error; los snapshots completos posteriores conservan diferencias de IDs y multiplicidades. Los fallos transitorios recuperados no dejan `FailureCode` final; el agotamiento registra el código estable, la cancelación registra `Canceled` y un fallo de creación/disposición de sesión no clasificado registra `ReadSessionUnavailable`.

Solo `Exportar diagnóstico` materializa esta evidencia en `pending-reconciliation.json`, incluso cuando falla el primer preflight. El manifiesto ZIP conserva esquema 2; el detalle Pending usa esquema 3. El exportador omite `beforeSubmission` mientras sea nulo y conserva un snapshot explícito si Pending estaba vacío. El log ordinario continúa sin valores libres del portal; la excepción acotada de verificación del formulario incorpora fecha, hora, duración y estado de sesión esperados/observados solo en el diagnóstico exportado. Contraseñas, semillas y códigos TOTP, cookies, tokens y contenido del correo nunca forman parte de la traza.

Antes de generar `PamExecutionResult` y antes de componer un correo, el coordinador vuelve a comprobar que recibió exactamente un ID por solicitud enviada y que ninguno pertenece a la línea base. Una violación detiene el flujo sin mostrar ni enviar esos IDs.

Si PAM ya confirmó envíos pero la reconciliación posterior falla, la aplicación no afirma que la automatización no hizo nada: informa una ejecución incompleta, conserva el número de solicitudes enviadas y pide revisar Pending antes de repetir. El usuario puede recuperar el lote mediante la opción de usar los IDs existentes cuando el siguiente preflight encuentre el conjunto completo.

La excepción deliberada es la acción `Usar Request ID existentes y enviar correo`. Solo se habilita cuando la comparación previa produjo exactamente un ID de la línea base por cada solicitud esperada, sin faltantes ni ambigüedad, y `FinalAction` es `SendEmail`. Esta ruta se representa como `ExistingPending`, omite el servicio de envío PAM, conserva el conjunto exclusivamente en memoria y utiliza el mismo saneamiento HTML y validación de IDs. Como el usuario ordena explícitamente un nuevo envío con esos IDs, esta acción omite únicamente las barreras de correo duplicado en memoria y Outlook; las ejecuciones normales conservan ambas barreras. No puede combinar IDs antiguos y nuevos.

Después de ese envío explícito, la interfaz muestra inmediatamente un resultado modal acorde con el estado real devuelto por Outlook: confirma que fue localizado en Enviados, que Outlook aceptó la solicitud o que todavía permanece en Bandeja de salida. Un borrador, una falta de confirmación u otro estado no se presenta falsamente como éxito.

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

Las colecciones se guardan en `%LOCALAPPDATA%\PamAutomation\config\favorite-collections.dat` mediante DPAPI `CurrentUser`, `schemaVersion = 3` y reemplazo atómico. El lector migra los esquemas 1 y 2 conservando nombre, razón, selección y colección activa; del esquema 1 descarta la antigua configuración temporal. Copia el correo global existente a cada colección y vuelve a leer el documento cifrado migrado. El esquema 3 marca que esa copia ya se realizó: cambiar el legado nunca sobrescribe las copias, las colecciones nuevas no lo heredan y eliminar una colección elimina su correo. El archivo global anterior se conserva. Reconciliar Favorites conserva el correo, incluso si la colección pierde sus cuentas. Si una colección personalizada activa queda vacía, `All favorites` pasa a ser la activa.

En fase 26, `CollectionMailComposer` combina exclusivamente el contenido de la colección activa con el remitente principal y la firma vigente. Sanea la firma sin marcador, adapta sus CID para evitar colisiones con las imágenes del contenido y la agrega al final de una plantilla efímera. `OutlookHtmlComposer` reemplaza el único marcador por los IDs al componer el cuerpo. Nunca se guarda esa plantilla combinada en una colección. No se intenta extraer firmas del correo legado: se conserva intacto y la firma común comienza vacía; el usuario debe retirar las firmas antiguas si configura una firma común equivalente.

La confirmación inicial recibe la misma configuración efectiva que el envío normal o la reutilización de Pending; muestra colección, Para, CC, asunto y remitente. Una configuración ausente o inválida bloquea Outlook antes del preflight con un aviso que identifica la colección y el editor. Core valida nuevamente el correo antes de crear solicitudes. Con Outlook desactivado se permite ejecutar sin correo ni firma. Los editores mantienen borradores y solo guardar la colección persiste el correo; cancelar cualquier nivel conserva lo anterior. `All favorites` permite editar motivo y correo aun cuando su catálogo esté vacío, pero no se puede activar sin cuentas.

La entrada nativa WPF presenta fecha, hora de 12 horas con selector `AM/PM` y duración operativa en horas. La razón se obtiene de la colección activa. La conversión temporal se realiza en el dominio y el adaptador comprueba el valor normalizado por PAM. El recorrido confirmado, sus selectores e invariantes se mantienen en `PAM_NAVIGATION.md`.

El conflicto `PSPortal.Requests.Policies.RemoteAppAccesssAlreadyAvailable` se registra y permite continuar. Otros errores deben detener el proceso.

## Integridad y plazos de Pending — ajuste del 22 de septiembre de 2026

La completitud exige un total explícito: se interpretan contadores simples y rangos `primero-último of total items`; un contador desconocido provoca `SnapshotUnstable`. La captura masiva se rodea de lecturas del paginador/contador y solo se acepta cuando dos observaciones completas coinciden y Status continúa habilitado en Pending. Al seleccionar el máximo, el número de páginas y las filas deben corresponder al tamaño elegido. PAM puede retirar temporalmente el selector durante esa transición; no se vuelve a resolver su ancestro para confirmar la selección.

Cuando esa primera vista estable confirma cero registros y cero filas, Browser omite por completo el selector `items per page`, que puede estar deshabilitado o ausente. Continúa por el escaneo normal y vuelve a comprobar estabilidad, filas y total antes de devolver una lista vacía válida como línea base. No se acepta como vacío un contador ausente, filas incompatibles con el cero ni una vista que continúa cargando. Si aparecen registros entre la comprobación inicial y el escaneo, se leen con la paginación vigente sin asumir que el primer cero sigue siendo válido.

Cada salto revalida el estado vivo y el botón siguiente. Un cambio reinicia el recorrido desde la primera página, sin unir snapshots de distintos recorridos. El total final debe coincidir exactamente con los IDs únicos recuperados. Permanecen las validaciones por campos, multiplicidades y exclusión de la línea base.

Controles: cinco segundos; probes DOM: dos segundos; lectura completa: 30 segundos; presupuesto compartido por etapa: 120 segundos, incluida autenticación y recuperación (más el cierre de recursos). La cancelación del usuario sigue siendo cancelación; el vencimiento global se informa como `RecoveryTimeout`. Vencer la lectura cierra la página y exige otra sesión: cancelar una espera .NET no cancela por sí mismo un clic Playwright pendiente. Nunca se reenvían solicitudes ni correo como recuperación.

El detalle de diagnóstico mantiene esquema 3 con campos aditivos `elapsedMilliseconds` y `browser` en las lecturas; este último aparece ante un fallo y contiene exclusivamente operación técnica, páginas, total, filas y estado del siguiente botón.

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

El editor de correo se abre desde cada colección; su modo firma reutiliza la edición enriquecida, oculta destinatarios, asunto y acciones de Request ID, y explica que se agregará al final. El contenido y sus imágenes se guardan en la colección; la firma y sus imágenes en `mail-signature.dat`, ambos protegidos con DPAPI CurrentUser. Cada imagen se normaliza a PNG y recibe un Content-ID; el adaptador COM crea un temporal único solo para `Attachments.Add`, configura el CID y lo elimina en el mismo bloque `finally`.

Antes de enviar resultados se consulta el almacén asociado exactamente al `SmtpAddress` configurado por una propiedad privada que contiene el conjunto ordenado exacto de Request ID y se exige también el mismo asunto. La búsqueda recorre Enviados, Bandeja de salida y Borradores. La sesión actual conserva además una huella en memoria para impedir dos intentos concurrentes.

La búsqueda previa aplica `Items.Restrict` sobre el asunto estándar con escape de literales DASL; abre únicamente los candidatos filtrados, comprueba primero su clase y exige asunto y huella de Request ID exactos. No filtra directamente por campos personalizados, que pueden no existir en todas las carpetas. Un candidato ilegible produce una excepción y bloquea el envío normal; nunca equivale a un correo ausente.

Después de `Send()`, `ObserveDeliveryAsync` consulta inmediatamente Enviados y repite únicamente esa lectura hasta quince observaciones, con cuatro segundos entre ellas. Solo la última amplía la búsqueda a Bandeja de salida y Borradores si no encuentra el mensaje en Enviados. Los errores COM `0x80010001`, `0x8001010A` y `0x80040114` permiten reintentar exclusivamente la observación; errores desconocidos o permanentes detienen la comprobación. Este límite cuenta intentos y pausas: no interrumpe por la fuerza una llamada COM en curso.

Enviados confirma que existe la copia enviada, no la recepción por el destinatario. Bandeja de salida se reporta como procesamiento pendiente y nunca se mueve automáticamente; Borradores indica que Outlook no completó el envío. Si no se encuentra el mensaje o no se puede comprobar su estado, se conserva `SubmittedUnconfirmed` o `NotConfirmed` según el resultado original, sin reutilizar una observación antigua como estado actual. `Send()` no se repite y la barrera de duplicados en memoria permanece activa.

El servicio de confirmación registra una sola advertencia por lectura recuperable, un error al detenerse por fallo definitivo o agotamiento de errores y un resumen con intento, duración y recuperaciones. El adaptador aporta contexto técnico de carpeta, operación, cantidad e índice de candidatos y HRESULT, sin asunto, dirección, cuerpo ni Request ID. Los diagnósticos siguen en memoria hasta su exportación explícita.

## Seguridad

Los secretos deben almacenarse con Windows Credential Manager o DPAPI. No deben aparecer en capturas, excepciones mostradas al usuario, exportaciones de diagnóstico ni en ningún dato persistente.

## Resultados de ejecución

`Ver resultado de la automatización` abre una ventana WPF modal cuando existen Request ID. Conserva el resumen y la lista seleccionable con desplazamiento, y ofrece `Copiar Request ID` junto a `Aceptar`. El ViewModel entrega por separado el mensaje completo y el texto exacto de IDs para copiar: el botón no extrae números del resumen ni incluye encabezados, fechas o conteos. Solo la pulsación explícita escribe texto Unicode en el portapapeles, con un ID por línea en el orden obtenido. La ventana confirma la copia sin cerrarse; un fallo nativo del portapapeles muestra un aviso local y permite reintentar. Los resultados sin IDs y otros avisos conservan su presentación existente. No se añaden archivos operativos ni registros con el contenido copiado.

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

La prueba del conjunto completo de Pending con correo desactivado exige que no se permita reutilizar IDs para enviar correo y que el aviso indique cancelar y activar la opción vigente `Enviar correo con Outlook`. Las pruebas de navegador local que requieren opt-in pueden quedar omitidas en la suite ordinaria; los fallos de aserción sí impiden publicar.

Todas las dependencias externas deben tener interfaces sustituibles:

- Reloj.
- Almacenamiento de secretos.
- Sistema de archivos.
- Automatización PAM.
- Outlook.

El framework elegido es MSTest sobre Microsoft Testing Platform. Las pruebas se descubrirán y ejecutarán desde la terminal mediante el runner integrado del proyecto `PamAutomation.Tests`. Las pruebas COM que necesiten apartamento STA utilizarán el soporte STA de MSTest y permanecerán separadas de las pruebas unitarias.

Las pruebas unitarias no deben abrir Edge, PAM ni Outlook. Las pruebas de integración pueden utilizar credenciales reales y el contexto temporal `headless` de Playwright cuando exista autorización explícita; deben conservar los mismos controles de seguridad y no deben crear solicitudes ni enviar correos sin una autorización adicional. Las pruebas del botón interactivo sí pueden mostrar Edge porque validan deliberadamente el flujo visible separado.

## Contratos de estabilización — 1 de octubre de 2026

Estas reglas actualizan la verificación compartida por prueba y producción. La disponibilidad exige controles únicos, visibles, habilitados y campos de escritura editables; Start Date se selecciona por el calendario y puede ser readonly. Se sondea inmediatamente y se exige una ventana continua de 600 ms y al menos dos lecturas completas correctas, dentro de 15 segundos. Diferencias, lecturas incompletas, controles reemplazados y errores reinician la racha. Tras dos observaciones con la misma discrepancia puede corregirse una sola vez por formulario, sin renovar ese permiso en BeforeSubmit; corregir fecha vuelve a escribir la hora dependiente. Application Session desactivado bloquea el envío. El motivo se compara completo en memoria; solo se exportan longitud y nombres de campos.

El calendario conserva PamCalendarFailure compatible y añade PamCalendarDiagnostic: intento, ordinal/esperadas, etapa, tiempos monotónicos, conteos y hasta 20 cambios de condición. Un conteo de controles no sondeado queda null/Unknown; cero solo representa una ausencia observada. Un mes coherente que todavía no sea el solicitado es UnexpectedMonth, no una oscilación inventada entre válido/error. No se habilitan días prohibidos ni se acepta información incompleta. Los presupuestos de calendario siguen en 8/45 segundos.

PamRequestSubmissionException es el único transporte de una ejecución interrumpida: PartialRun incluye todos los elementos autorizados con estados Submitted, ApprovedAccessOverlap, Rejected, FailedBeforeSubmission, Uncertain y NotAttempted. Conserva evidencia disponible de clic/POST/respuesta y resultados conocidos aunque falle la limpieza. Un error HTTP no reconocido no prueba rechazo. Se detienen los envíos siguientes. La cancelación conserva resultados, impide envíos siguientes y omite nuevas lecturas de recuperación; el POST ya iniciado conserva su observación acotada, sin repetirlo. Cerrar el proceso descarta memoria, por lo que debe revisarse Pending antes de volver a solicitar.

La recuperación reutiliza los presupuestos y comparación de Pending: System + Account normalizados, inicio, expiración y motivo completos, excluyendo todos los IDs de la línea base. Application no participa. Solo grupos cuya multiplicidad sea completa permiten conservar IDs/verificación; los incompletos o excesivos siguen sin resolverse, sin asignar por orden. ConfirmedInPending representa verificación del grupo, sin adjudicar un ID a una aplicación indistinguible. UncertainCount conserva la falta histórica de respuesta; UnresolvedCount descuenta lo verificado en Pending. Incluso si aparecen todos los IDs candidatos después de un fallo, el lote conserva su condición interrumpida y no envía correo ni se agrega al acumulador.

La contabilidad se actualiza sin depender de IProgress. submissionCounts nulo indica que el envío aún no comenzó; el submittedCount legacy de preflight no sirve para inferir ausencia de solicitudes. LastAttempt describe el último intento real y LastExecution el último resultado completado, conservado aunque falle otro intento. El ID común une preparación, formularios y Pending; FailedStage no se reemplaza al iniciar recuperación. Los logs de etapa/conteos no incluyen identidades, texto del motivo, credenciales, TOTP, cookies, tokens ni cuerpos/DOM. Manifest schema 3 incorpora la separación de intento; componentes conocidos se identifican sin dependencia Infrastructure → Browser.
