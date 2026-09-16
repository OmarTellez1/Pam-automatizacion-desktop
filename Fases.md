# Fases de desarrollo — Pam Automation .NET

Este documento resume las fases implementadas, dónde se incorporó cada cambio y por qué fue necesario. El detalle cronológico permanece en `History.md` y la estructura física en `structure.md`.

## Fase 1 — Estructura base

Se creó `PamAutomation.slnx` con los proyectos `App`, `Core`, `Browser`, `Infrastructure`, `Outlook` y `Tests`, todos orientados a .NET 10 y x64. Esta separación estableció desde el inicio los límites entre interfaz, dominio e integraciones externas, evitando concentrar toda la aplicación en WPF.

## Fase 2 — Presentación WPF y MVVM

Se implementó la ventana principal en `PamAutomation.App` con WPF/XAML, recursos visuales y `MainWindowViewModel`. Los estados seguros y comandos quedaron fuera del code-behind y se cubrieron con pruebas. Esta fase creó la base de interacción sin activar todavía PAM, Edge u Outlook.

## Fase 2B — Alineación visual

Se reconstruyó la portada nativa tomando como referencia visual la aplicación anterior. Se conservaron encabezado, jerarquía de acciones y accesos de configuración sin copiar HTML, CSS o JavaScript. Fue importante para validar tempranamente la dirección visual antes de conectar funciones sensibles.

## Fase 3A — Dominio de configuración

Se agregaron en `PamAutomation.Core` los modelos y validadores de configuración no sensible: acción final, solicitud, fecha, hora, duración, razón, zona horaria y modo de prueba. La validación determinista permite aplicar las mismas reglas desde la interfaz y desde pruebas sin depender del sistema operativo.

## Fase 3B — Persistencia no sensible

Se definió `IAppSettingsStore` en Core y se implementó `JsonAppSettingsStore` en Infrastructure. La configuración se guarda con esquema explícito y sustitución atómica bajo `%LOCALAPPDATA%`. Esta fase evitó archivos `.env`, escrituras parciales y acoplamiento del dominio al sistema de archivos.

## Fase 4 — Credenciales PAM protegidas

Se definieron los modelos y el contrato de credenciales en Core y se implementó `WindowsPamCredentialStore` en Infrastructure mediante Windows Credential Manager. Usuario, contraseña y semilla TOTP se mantienen juntos, versionados y protegidos para el usuario actual. Las pruebas usan una API falsa, por lo que no leen ni modifican credenciales reales.

## Fase 5 — Generación TOTP

Se implementaron en `PamAutomation.Core/Totp` la decodificación Base32 y el algoritmo RFC 6238 con HMAC-SHA1, códigos de seis dígitos e intervalos de 30 segundos. `TotpCode` modela la ventana de validez, aplica el mínimo requerido de ocho segundos y redacta su representación textual. Las pruebas usan exclusivamente vectores públicos y cubren claves inválidas y límites temporales. Esto permite verificar el cálculo criptográfico antes de incorporar en la fase 6 la hora HTTP confiable del portal.

La fase 5 no accede a Credential Manager, Edge, BeyondInsight u Outlook. Fue validada desde la terminal en `Release | x64`: la solución compiló sin errores y las 71 pruebas terminaron correctamente.

## Fase 6 — Hora HTTP confiable

Se definieron en `PamAutomation.Core/Interfaces/Time` los contratos `IClock` e `IPamClockFactory` y los errores estables de calibración. `PamHttpDateClockFactory`, en `PamAutomation.Infrastructure/Time`, valida el encabezado HTTP `Date` en formato RFC 1123, estima el instante de recepción con la resolución del encabezado y la mitad del tiempo de ida y vuelta, y desde allí avanza únicamente con la fuente monotónica de `TimeProvider`. No existe respaldo silencioso al reloj local. Las pruebas deterministas cubren fechas ausentes o inválidas, mediciones negativas, desfase del portal y avance monotónico.

## Fase 7 — Edge y contexto Playwright restringido

Se incorporó Microsoft.Playwright 1.62.0 en `PamAutomation.Browser` y se implementaron los contratos de sesión definidos en Core. La fábrica lanza el Edge instalado mediante `Channel = "msedge"` en modo visible, crea un contexto temporal no persistente, limita la excepción HTTPS a ese contexto, bloquea service workers y aborta solicitudes HTTP(S) fuera del origen PAM exacto por esquema, host y puerto. La sesión no expone objetos Playwright ni cabeceras generales: solo devuelve URI final, estado y el encabezado `Date` necesario para la fase 6. Los dobles unitarios verifican opciones, restricción de origen, errores controlados, cancelación y liberación de recursos sin abrir Edge ni contactar PAM.

Las fases 6 y 7 fueron integradas y validadas conjuntamente en `Release | x64`: la solución compiló sin errores y las 90 pruebas terminaron correctamente. No se ejecutaron pruebas reales contra Edge o PAM porque requieren autorización explícita.

## Fase 8 — Autenticación PAM simulada

Se implementó en Core un servicio de autenticación aislado que enlaza el puerto del portal con la misma sesión temporal de navegador, calibra el reloj con HTTP `Date` y el tiempo monotónico de ida y vuelta, genera el TOTP y exige al menos ocho segundos de vigencia. El flujo simulado diferencia credenciales rechazadas, dos rechazos TOTP, fallos de comunicación, ausencia de confirmación y respuestas inesperadas. Solo reintenta una vez ante un rechazo TOTP explícito, espera que desaparezca el rechazo anterior y nunca utiliza credenciales reales, selectores Playwright ni el portal PAM. Las pruebas cubren todos esos caminos con dobles deterministas.

## Fase 9 — Autenticación PAM real visible

Se implementó en `PamAutomation.Browser` el adaptador Playwright real enlazado a la misma página y contexto temporal de `PamBrowserSession`. Los selectores accesibles observados se centralizaron, los resultados ambiguos no se clasifican como rechazo y el anuncio `Navigation complete.` se distingue del error de credenciales. Una prueba de integración opt-in lee exclusivamente Windows Credential Manager, abre Edge externo visible y limita su alcance al login. La validación autorizada confirmó `Return to dashboard` con el reloj HTTP y TOTP de las fases anteriores; después cerró la sesión sin navegar a Favorites, crear solicitudes o abrir Outlook. La solución compiló en `Release | x64` y ejecutó 127 pruebas correctas.

## Fase 10 — Navegación a Favorites

Se agregó un contrato de sesión autenticada reutilizable y `PamFavoritesService` para mantener login y lectura dentro del mismo contexto temporal. El adaptador real navega a `Accounts`, selecciona `Browse by Category > Favorites`, espera un grid estable, recorre el paginador accesible cuando existe y construye identidades redactadas `System + Application + Account` para cada `Favorites` row. Core rechaza resultados vacíos, incompletos o duplicados. La API de esta fase es exclusivamente de lectura y no permite seleccionar filas ni abrir formularios. La integración real autorizada confirmó `Favorites` y cerró Edge sin solicitar accesos. La solución compiló en `Release | x64` y ejecutó 146 pruebas correctas.

## Fase 11 — Modo de prueba de solicitudes

Se implementó `PamRequestTestService` y un contrato deliberadamente incapaz de enviar solicitudes. El flujo reutiliza la sesión autenticada de fase 10, recorre cada `Favorites` row, vuelve a comprobar `System + Application + Account`, abre `Access`, llena fecha, hora, duración y razón, valida `Application Session` y descarta el formulario mediante `Cancel > Discard Changes`. El adaptador instala además un bloqueo de red para `POST /api/ps-portal/requests`; cualquier intento interceptado hace fallar la prueba.

El resultado se mantiene en memoria, no contiene identidades y define `SubmittedCount = 0`. La integración real visible autorizada procesó 14 `Favorites` rows, validó y descartó los 14 formularios y confirmó cero envíos. La solución compiló en `Release | x64` y el proyecto completo ejecutó 154 pruebas correctas.

## Fase 12 — Solicitudes reales confirmadas

Se incorporó un flujo separado del modo de prueba para crear solicitudes reales. `PamRequestSubmissionService` valida la configuración, abre una única sesión autenticada, vuelve a leer todas las `Favorites` rows y exige una autorización ligada al conteo y a la configuración exactos antes de habilitar cualquier envío. Cada fila se vuelve a localizar por `System + Application + Account`, el formulario se llena y verifica inmediatamente antes de `Submit Request`, y solo una respuesta HTTP 2xx se clasifica como enviada.

El conflicto conocido `PSPortal.Requests.Policies.RemoteAppAccesssAlreadyAvailable` se conserva como resultado controlado y permite continuar; cualquier otra respuesta detiene la ejecución sin exponer el contenido devuelto por PAM. Los resultados permanecen en memoria. La integración destructiva está excluida del flujo normal y requiere el opt-in exacto `PAM_AUTOMATION_ALLOW_SINGLE_REAL_REQUEST=CREATE_ONE_REQUEST_TODAY`.

Con autorización explícita del usuario se ejecutó una sola vez esa integración limitada a un intento. El 10 de septiembre de 2026 PAM aceptó una solicitud para la primera `Favorites` row, con inicio `08:20 AM` —normalizado como `08:20`— y duración de 5 minutos. Se guardaron capturas locales ignoradas por Git antes y después del envío; no se abrió Outlook ni se envió correo.

## Fase 13 — Lectura y comparación de Pending

Se agregó `PamPendingService` y un contrato de portal exclusivamente de lectura para navegar a `Requests`, seleccionar exactamente `Status = Pending` y recorrer hasta 500 páginas. Cada fila se convierte en un modelo redactado con Request ID numérico, `System`, `Application`, `Account`, `Request Start Date`, `Response`, `Expires` y `Description`; se rechazan filas incompletas, Request ID duplicados, estados distintos de `Pending`, paginación cambiante y asociaciones ambiguas.

La comparación localiza por fecha/hora completas más `System + Account` y valida por separado el `Application` solicitado, `Expires` calculado y `Description`. El portal puede representar `Application` como `nombre solicitado - destino`; solo se admite el nombre completo solicitado, con ese sufijo estructurado opcional, sin inferir familias desde `System`. Distingue coincidencias y faltantes sin sustituir filas, rechaza asociaciones múltiples y ofrece una espera acotada para la consistencia eventual. Todos los resultados permanecen en memoria.

La integración real visible confirmó primero el estado vacío y después filas reales autorizadas de 5 minutos. La validación final utilizó el 10 de septiembre de 2026 de `05:05 PM` a `05:10 PM`, comprobó una coincidencia ya existente sin crear una tercera solicitud y verificó `Request Start Date`, `Expires`, `Description`, `Application`, `System`, `Account` y `Response = Pending`. Las capturas locales muestran las columnas iniciales y finales mediante desplazamiento horizontal; están excluidas de Git.

## Fase 14 — Outlook falso y Outlook COM

Se definieron en Core los contratos, modelos redactados, validaciones y errores estables de Outlook. `PamAutomation.Outlook` incorporó inicialmente Microsoft Office Interop Outlook 15.0.4797.1004, resolución exacta de cuenta por `SmtpAddress`, comprobación mediante mensaje temporal descartado, creación de un mensaje nuevo por envío, asignación previa de `SendUsingAccount`, ejecución en un hilo STA y liberación explícita de referencias COM. La fase 16.7 sustituyó posteriormente esa PIA incompleta por COM tardío sin cambiar los contratos. Las pruebas usan un seam COM falso: no abren Outlook ni envían correo. Como todavía no se consulta Enviados, un `Send()` correcto se informa honestamente como `SubmittedUnconfirmed`, no como entrega confirmada.

Las fases 8 y 14 fueron integradas y validadas conjuntamente en `Release | x64`: la solución compiló sin errores y las 116 pruebas terminaron correctamente. Permanece una advertencia MSTest heredada fuera de estas fases.

## Fase 15 — HTML saneado y marcador Request ID

Se incorporó `OutlookHtmlComposer` en Core como única ruta para preparar el cuerpo final del correo. Recibe XHTML generado por la aplicación, lo analiza con el parser XML de .NET y DTD deshabilitados, elimina elementos activos, atributos de eventos, estilos no permitidos y recursos externos, y conserva un subconjunto explícito de párrafos, formato de texto, listas, tablas e imágenes `cid:`. El límite de entrada es de 8 millones de caracteres y los errores no reproducen contenido privado.

La plantilla debe contener `{{REQUEST_IDS}}` exactamente una vez en un nodo de texto. La composición acepta únicamente Request ID numéricos y únicos, conserva su orden y sustituye el marcador por un ID por línea. `OutlookMessageValidator` vuelve a validar que el cuerpo sea idéntico a su representación saneada y contenga todos los identificadores antes de crear cualquier objeto COM, evitando que un consumidor omita accidentalmente el compositor.

Esta fase no implementa todavía el adaptador WPF desde `FlowDocument`, la persistencia protegida de la configuración de correo ni adjuntos CID; define y prueba el límite seguro que esos componentes deberán utilizar. No abrió PAM ni Outlook y no creó solicitudes ni correos. La solución compiló en `Release | x64` y la suite ordinaria ejecutó 225 pruebas correctas.

## Fase 16 — Resultados en memoria e interfaz

Se agregó `PamAutomationExecutionService` como coordinador de aplicación en Core. Una preparación real lee dinámicamente todas las `Favorites` rows y entrega a WPF un snapshot no sensible; el diálogo muestra conteo, fecha, hora, duración y razón. Solo después de una respuesta afirmativa se crea una autorización de un uso. El servicio real vuelve a comprobar el snapshot, procesa solicitudes y consulta `Pending` en una sesión autenticada para recuperar los Request ID exactos de las filas enviadas.

`MainWindowViewModel` carga la configuración no sensible y el estado de Windows Credential Manager, bloquea todas las acciones durante una ejecución y conecta comandos asíncronos separados para prueba y modo real. La aplicación permite configurar credenciales sin recuperar secretos y editar la solicitud con hora `AM/PM`; los resultados muestran conteos, conflictos, Request ID y hora final en memoria, además de un diálogo modal. Los fallos conocidos conservan mensajes controlados y cualquier excepción desconocida se presenta mediante un mensaje fijo sin detalles internos.

La configuración de correo se completa mediante un editor nativo `RichTextBox`/`FlowDocument` para texto con formato, listas, tablas e imágenes pegadas. El convertidor genera únicamente XHTML saneado; las imágenes se convierten a PNG, se persisten dentro de `mail-settings.dat` protegido con DPAPI `CurrentUser` y se materializan como adjuntos CID en una carpeta temporal única que se elimina inmediatamente después de agregarlos a Outlook.

Antes de guardar se valida la cuenta mediante un mensaje temporal descartado. La acción manual `Guardar y enviar prueba` exige confirmación y solo puede enviar desde y hacia la cuenta SMTP configurada. El correo del modo de prueba PAM exige otra confirmación inmediata, utiliza únicamente `tellezoe@hitss.com`, deja CC vacío, antepone `[PRUEBA]` y usa la lista simulada canónica. El modo real utiliza sus Request ID reconciliados.

La protección de correo combina una huella en memoria con una propiedad privada de Outlook que representa el asunto y el conjunto exacto de Request ID. Antes y después de enviar se consultan Enviados, Bandeja de salida y Borradores; un duplicado no se vuelve a crear. El ajuste posterior a la fase 22 hizo esa observación tolerante y no destructiva: un mensaje en Bandeja de salida permanece allí para que Outlook continúe procesándolo. El resultado muestra el estado de entrega sin afirmar confirmación cuando Outlook no puede demostrarla. Los fallos parciales de PAM muestran cuántas `Favorites` rows alcanzaron un resultado conocido y advierten revisar `Pending` antes de reintentar.

La validación ordinaria compiló en `Release | x64`, ejecutó 243 pruebas correctas y arrancó la ventana WPF sin cierre inesperado. No se abrió PAM, no se envió correo y no se consumió ninguna de las diez pruebas reales autorizadas.

## Fase 16.5 — Paridad funcional y claridad de configuración

Se revisó en Edge visible la aplicación JavaScript anterior y se compararon su portada, `Editar credenciales`, `Editar solicitud` y `Editar envío de correo` con las ventanas WPF. El objetivo no fue copiar píxeles ni reutilizar código anterior, sino conservar su orientación al usuario y alcanzar paridad funcional sobre la arquitectura segura de C#.

`Configurar credenciales` reúne ahora dos tarjetas sin mezclar sus mecanismos de almacenamiento. PAM muestra si existe una credencial, conserva los secretos vacíos durante edición, permite mostrar u ocultar solo los valores recién escritos y ofrece `Guardar cambios y probar acceso`; la prueba abre una sesión temporal visible, confirma el dashboard y se cierra sin navegar a `Favorites` ni crear solicitudes. Outlook explica que es opcional, que exige Outlook clásico y que no guarda contraseña; permite guardar y verificar la cuenta exacta y mantiene el correo de prueba como una acción separada, confirmada y dirigida desde y hacia esa misma cuenta.

La cuenta SMTP se desacopló de la plantilla mediante `OutlookAccountSettings` y `outlook-account.dat`, protegido con DPAPI `CurrentUser`, esquema versionado y escritura atómica. Una configuración anterior que solo tenga `mail-settings.dat` migra su remitente automáticamente. El editor de correo muestra la cuenta como solo lectura, incluye ayuda de destinatarios, conserva formato, tablas e imágenes y agrega una acción para insertar `{{REQUEST_IDS}}` en el cursor con estado visible de unicidad.

`Configurar solicitud` presenta la fecha completa localizada, un único campo `hh:mm` con `AM/PM`, duración y razón en bloques separados, modo de prueba secundario y errores dentro del formulario. Las tres ventanas comparten tarjetas, avisos, botones principales amplios, desplazamiento y estados visuales coherentes con la portada.

La validación compiló en `Release | x64` y ejecutó 253 pruebas correctas, incluidas la carga STA conjunta de las tres ventanas, coordinación no destructiva de credenciales, orden verificar-guardar de Outlook, destinatario único de prueba, validación SMTP, persistencia DPAPI y migración. Las pruebas automatizadas no abrieron PAM ni Outlook, no enviaron correos y no crearon solicitudes reales.

## Fase 16.6 — Logging seguro de desarrollo

Se incorporó logging estructurado en los límites de la aplicación, configuración, ejecución y Outlook clásico. El diagnóstico distingue apertura COM, resolución exacta de `Account`, creación y descarte del `MailItem`, asignación de `SendUsingAccount`, persistencia protegida, búsqueda de duplicados y envío, además de capturar excepciones no controladas de WPF, tareas y dominio de aplicación.

El proveedor solo se activa con un depurador conectado o `PAM_AUTOMATION_DEVELOPMENT_LOGGING=1` y después de confirmar que el proceso se ejecuta dentro del repositorio. Escribe `logs/pam-automation-AAAA-MM-DD.log`, elimina únicamente esos archivos cuando superan dos días y omite direcciones, secretos, TOTP, cookies, tokens, Request ID, contenido del correo y mensajes de excepción. La aplicación publicada conserva un logger nulo y no genera persistencia operativa.

La validación relacionada compiló en `Release | x64` y ejecutó pruebas del proveedor, retención, flujo de credenciales, Outlook y coordinación WPF. No abrió PAM, no creó solicitudes y no envió correos.

## Fase 16.7 — Corrección de activación COM de Outlook

Los logs de desarrollo demostraron que la cuenta no era rechazada: `OutlookComApplicationFactory.Create` fallaba antes de consultar `Accounts` con `FileNotFoundException` y `HRESULT 0x80070002`. La PIA `Microsoft.Office.Interop.Outlook` incluida en la salida exigía `office, Version=15.0.0.0`, pero su paquete no declaraba ni copiaba esa dependencia y .NET 10 no la resolvía desde el GAC de .NET Framework. La aplicación anterior funcionaba porque activaba COM tardíamente sin cargar esa PIA.

Se reemplazó exclusivamente el límite nativo por activación de `Outlook.Application` mediante `Type.GetTypeFromProgID` y despacho COM tardío. Se conservaron `IOutlookComApplicationFactory`, los wrappers propietarios, el hilo STA, la resolución exacta de `SmtpAddress`, mensajes temporales descartados, carpetas, huellas, adjuntos CID y liberación explícita. No se incorporaron scripts, PowerShell, procesos auxiliares ni copias locales de `office.dll` o `stdole.dll`.

Las salidas `Debug | x64` y `Release | x64` compilaron correctamente y sus archivos `.deps.json` ya no declaran la PIA ni sus dependencias ausentes. Pasaron las 257 pruebas ordinarias, incluida una nueva prueba del recorrido COM tardío con objetos falsos. No se abrió PAM, no se creó ninguna solicitud y las pruebas automatizadas no abrieron Outlook ni enviaron correo. La comprobación real desde la interfaz queda a cargo del usuario porque requiere Outlook clásico explícitamente autorizado.

## Fase 16.8 — Retroalimentación visual de credenciales y Outlook

La prueba manual del usuario confirmó que la fase 16.7 verifica la cuenta y envía el correo de prueba. Se refinó la experiencia para que guardar y comprobar credenciales PAM, verificar Outlook y enviar el correo muestren de inmediato un mensaje de solicitud recibida junto con un círculo de carga animado. Los controles permanecen deshabilitados durante la operación y el indicador desaparece ante éxito, error o cancelación.

El correo de prueba ya no reproduce el estado técnico `SubmittedUnconfirmed`, que resultaba ambiguo para esta acción manual. Cuando `Send()` es aceptado muestra `El correo de prueba se envió con éxito`. Si el adaptador devuelve `NotConfirmed` u otro estado no exitoso, la operación se presenta como error y conserva orientación de reintento, evitando un panel verde engañoso.

Una prueba manual posterior detectó que el `EventTrigger` original intentaba resolver `SpinnerRotation` al cargar la ventana, aunque el indicador estuviera oculto, y provocaba una excepción no controlada en el `Dispatcher`. Se retiró esa animación automática del template. La ventana crea, inicia y detiene ahora el `RotateTransform` únicamente dentro del ciclo de guardar/comprobar PAM, verificar Outlook o enviar el correo de prueba. Los fallos inesperados del workflow o de la presentación quedan registrados, muestran un mensaje seguro cuando la interfaz sigue disponible y restauran los controles mediante el `Dispatcher`. Como última barrera, una excepción WPF no recuperable muestra un aviso genérico y cierra la aplicación ordenadamente en vez de dejarla bloqueada o continuar en un estado incierto.

La solución compiló en `Release | x64` y las 260 pruebas ordinarias finalizaron correctamente. La validación muestra y renderiza realmente la ventana STA, comprueba que el indicador está inactivo al abrir, que solo se anima durante una operación, que se detiene al finalizar y que una excepción inesperada no escapa del evento WPF. También conserva las pruebas de mensajes del workflow, rechazo de `NotConfirmed` y regresión del adaptador Outlook. Las pruebas no abrieron PAM ni Outlook, no crearon solicitudes y no enviaron correos.

## Fase 17 — Evidencias técnicas y diagnóstico exportable

Se incorporó un buffer circular que conserva en memoria un máximo de 500 eventos técnicos durante toda la ejecución. Comparte los eventos estructurados de aplicación, configuración, PAM y Outlook, pero guarda la plantilla en lugar de los valores formateados, admite únicamente propiedades de estado o conteo previamente autorizadas y reduce las excepciones a tipo, `HRESULT` y stack trace sin mensajes ni rutas locales. El buffer no crea archivos y desaparece al terminar el proceso.

La portada incluye `Exportar diagnóstico`. Tras elegir manualmente un destino se genera un ZIP con `manifest.json`, `environment.json`, `execution-summary.json` y `diagnostic.log`; la escritura utiliza un temporal en la misma carpeta, se publica atómicamente y limpia restos ante cancelación o error. El resumen nunca recibe los Request ID, solo su conteo, y tampoco incluye credenciales, TOTP, cookies, identidades del portal, correos, razones, HTML o adjuntos. Los errores recuperables sugieren la exportación y una excepción WPF fatal ofrece exportarla antes de cerrar ordenadamente.

La solución compiló en `Release | x64` y las 271 pruebas ordinarias finalizaron correctamente. Se validaron capacidad y orden del buffer, lista permitida, redacción de valores, excepciones sin mensaje ni ruta, estructura y contenido del ZIP, cancelación, fallo y limpieza de temporales, restauración del ViewModel, resumen sin identificadores y renderizado STA de la nueva acción. No se abrió PAM ni Outlook, no se crearon solicitudes y no se enviaron correos.

## Fase 18 — Publicación portable y aviso de actualización

La distribución se simplificó por decisión explícita del usuario: no habrá MSI/MSIX ni escritura en `Program Files`. `PamAutomation.App` tiene versión nativa `0.0.1`, nombre de producto `PAM Automation` y título dinámico `PAM Automation V. 0.0.1`. El perfil `Portable-x64` publica `Release`, `win-x64`, autocontenido y como archivo único, sin trimming, AOT, símbolos ni archivos auxiliares. La salida verificada contiene únicamente `PAM Automation.exe`; WPF inició fuera del repositorio y la extracción controlada en `%TEMP%` incluyó el driver Windows de Playwright.

Se añadió un contrato de actualización en Core, un lector HTTP limitado y validado en Infrastructure y un banner descartable en WPF. La consulta ocurre después de mostrar y cargar la ventana, tiene timeout, cancelación y manejo seguro de todos los fallos. Compara la versión del ensamblado con `latest.json`; no utiliza fechas, no descarga, no reemplaza y no elimina ejecutables. El usuario administrará manualmente las versiones.

La compilación y 280 pruebas ordinarias validan versión, comparación, manifiesto inválido, límite de tamaño, repositorio inesperado, fallo remoto y renderizado del banner. El origen se migró desde Google Drive, bloqueado por Fortinet, a la URL `raw` pública del repositorio GitHub. Tras corregir `repositoryUrl`, la integración real de solo lectura pasó 1/1 y confirmó red, TLS, descarga, análisis y validación del repositorio. La salida canónica contiene un único ejecutable portable. No se abrió PAM ni Outlook, no se crearon solicitudes y no se enviaron correos. **Fase completada.**

## Fase 18.5 — Compactación visual y entrada horaria separada

La aplicación principal inicia maximizada y reduce anchos, alturas, tipografía, márgenes y espaciados de sus acciones principales. Las ventanas `Editar credenciales`, `Editar solicitud` y `Editar envío de correo` usan dimensiones iniciales y mínimas más contenidas; conservan scroll y la barra del editor de correo ahora puede envolver sus acciones para evitar recortes en pantallas estrechas.

La entrada de `Hora de inicio` se dividió en un desplegable `hh` con `01` a `12`, un campo editable `mm` de hasta dos dígitos y el selector `AM/PM`. Un parser interno rechaza horas, minutos o meridiano inválidos y convierte correctamente medianoche, mediodía y el resto de horas al formato de 24 horas que consume el dominio. La compilación `Release | x64`, 14 pruebas focalizadas y la suite ordinaria ampliada a 290 pruebas finalizaron correctamente. Solo permanece la advertencia heredada `MSTEST0032`. **Fase completada.**

## Fase 18.6 — Firma enriquecida desde el portapapeles

`Editar envío de correo` redujo nuevamente dimensiones, márgenes, tarjeta, editor y acciones. El pegado ahora prioriza RTF nativo de Outlook y admite CF_HTML de navegador o aplicaciones Office mediante un importador acotado. Texto, fuentes, tamaño, énfasis, colores, fondo, alineación, tablas, enlaces seguros e imágenes quedan representados en `FlowDocument`; las imágenes se normalizan a PNG y se guardan como CID mediante el flujo DPAPI existente.

`OutlookHtmlComposer` añadió saneamiento de fragmentos sin marcador y enlaces limitados a `mailto:` y HTTPS. El importador limita el contenido a 8 MB, 20.000 nodos y 64 niveles, elimina contenido activo y no descarga imágenes remotas. `HtmlAgilityPack` 1.13.0 se incorporó únicamente para analizar HTML real malformado; toda autorización continúa en el saneador propio. La compilación `Release | x64`, 25 pruebas focalizadas y las 294 pruebas ordinarias pasaron. Solo permanece `MSTEST0032`. **Fase completada.**

## Fase 18.7 — Tablas pegadas y editor simplificado

La comparación con la aplicación JavaScript confirmó que el usuario no necesita construir formato dentro de la aplicación: prepara el correo externamente y lo pega completo. Por ello `Editar envío de correo` retiró negrita, cursiva, subrayado, lista, `Tabla 2×2` y `Pegar imagen`; conserva únicamente `Insertar {{REQUEST_IDS}}`. El pegado ahora prioriza CF_HTML sobre RTF, igual que la referencia anterior, sin omitir el saneamiento C#.

`FlowDocumentHtmlConverter` conserva `colgroup`, anchos de columnas, `rowspan`, `colspan`, padding y bordes básicos. Cuando el fragmento termina en una tabla añade una línea vacía editable, evitando que nuevos clics o pulsaciones prolonguen la última celda. Una corrección posterior cubrió la diferencia entre el DOM del navegador y WPF: el portapapeles puede entregar HTML como `HTML Format` o `text/html` y como string, bytes o stream, por lo que el importador normaliza todas esas representaciones antes de convertir. El flujo registra únicamente etapa, tamaño, presencia de imagen y conteos estructurales, nunca contenido. Pruebas STA reproducen la tabla, ambas formas de portapapeles y la inserción efectiva en el cursor. La compilación `Release | x64` y las 298 pruebas ordinarias pasaron. Solo permanece `MSTEST0032`. No se abrió Outlook, no se envió correo y no se contactó PAM. **Fase completada.**

## Fase 18.8 — Tabla manual estable para el correo

Se conservó el pegado enriquecido que ya funciona para texto y firma, pero la interfaz ahora advierte explícitamente que las tablas externas no deben pegarse. `Insertar tabla 2×2` crea una estructura controlada con columnas 30/70, las filas `Solicitudes de PAM` y `Motivo del acceso`, bordes y padding. La tabla se inserta en la posición real del cursor dividiendo el párrafo, no al final del documento; deja un párrafo posterior editable y traslada a su celda el único marcador `{{REQUEST_IDS}}` para evitar duplicados.

Si el cursor está dentro de otra tabla, la operación se rechaza con una indicación segura y no crea una tabla anidada ni amplía la existente. Tres pruebas STA cubren inserción en mitad de contenido, conservación de un solo marcador y segundo clic dentro de la tabla. La compilación `Release | x64` y las 301 pruebas ordinarias pasaron. No se abrió Outlook, no se enviaron correos y no se contactó PAM. **Fase completada.**

## Fase 18.9 — Nomenclatura real de Application en Pending

El primer flujo completo del usuario creó correctamente 13 solicitudes, pero la reconciliación se detuvo antes del correo. El diagnóstico y las capturas horizontales confirmaron que `Request Start Date`, `Expires`, `Description`, `System` y `Account` eran exactos; la divergencia estaba en `Application`. Favorites usa nombres como `PLSQL - Nodo1`, `PuTTY - Nodo2` y `WinSCP - nodo1`, mientras Pending presenta respectivamente `PLSQL - BDMOVIL-…`, `PuTTY 0.77 - …` y `WinSCP 5.21 N2 - …`.

`PamPendingService` mantiene la asociación por fecha/hora más `System + Account` y admite equivalencia solamente para las familias observadas `NOSQLBOOSTER`, `PLSQL`, `PuTTY` y `WinSCP`. No usa `Ticket`, no compara aplicaciones desconocidas aproximadamente y conserva el rechazo ambiguo cuando dos filas de la misma familia compiten bajo una cuenta. Se añadieron seis ejecuciones de prueba que cubren las cuatro familias, el caso real PuTTY/WinSCP bajo el mismo `System + Account` y la ambigüedad. La compilación `Release | x64` y las 307 pruebas ordinarias pasaron. No se contactó PAM, no se crearon solicitudes y no se envió correo. **Fase completada.**

## Fase 18.10 — Motivo visible de automatización bloqueada

La ventana principal ya no depende del mensaje operativo genérico para explicar por qué `Iniciar automatización` está desactivado. Presenta un aviso ámbar independiente y accionable cuando faltan credenciales PAM, la solicitud no es válida o el envío por Outlook está seleccionado sin una configuración de correo completa. Para fechas u horas vencidas distingue ambos casos y dirige al usuario a `Configurar solicitud` para elegir un inicio futuro.

El aviso se oculta cuando la configuración está completa y también durante una operación, de modo que no compite con el progreso. Dos pruebas de presentación cubren una hora vencida al iniciar la aplicación y la desaparición del aviso con una configuración válida. La compilación `Release | x64` y las 309 pruebas ordinarias pasaron. No se contactó PAM, no se crearon solicitudes y no se envió correo. **Fase completada.**

## Fase 18.11 — Resultado bajo demanda y presentación final del correo

La finalización dejó de abrir automáticamente un cuadro modal mientras el comando todavía estaba cerrando su estado de ejecución. La ventana principal vuelve primero al estado inactivo, muestra únicamente `Ver resultado de la automatización` y abre los detalles —resumen y Request ID— cuando el usuario pulsa ese botón. Así el progreso no permanece visible detrás de un resultado y la pantalla principal no duplica la misma información.

El estado técnico `SubmittedUnconfirmed` se conserva para logs y diagnóstico, pero su texto normal ahora comunica que Outlook completó `Send()` correctamente, sin presentar como fallo la ausencia de una confirmación externa. El HTML compuesto queda envuelto en Aptos 11 con Calibri y Segoe UI como respaldo para evitar la tipografía serif implícita; el formato explícito de la firma continúa prevaleciendo. Las tablas generadas ocupan el 85%, quedan centradas y alinean sus celdas arriba para reducir espacio horizontal vacío. La compilación `Release | x64` y las 309 pruebas ordinarias pasaron. No se contactó PAM, no se crearon solicitudes y no se envió correo. **Fase completada.**

## Fase 21 — Acceso interactivo persistente a PAM

La ventana principal incorpora `Acceder a PAM`, habilitado únicamente cuando existen credenciales PAM y no hay otra operación en curso. El comando carga los secretos desde Windows Credential Manager, abre BeyondInsight en Microsoft Edge visible y reutiliza el flujo de autenticación protegido con reloj HTTP y TOTP. No lee `Favorites`, no abre formularios, no consulta `Pending`, no crea solicitudes y no ejecuta la acción de correo.

Esta fase aplica la excepción documental exclusivamente a este botón. `PamInteractiveBrowserSessionFactory` inicia o recupera un Edge externo mediante CDP limitado a loopback y usa `%LOCALAPPDATA%\PamAutomation\browser\pam-access-profile` como perfil dedicado persistente. Durante el login la aplicación controla la página; después libera la conexión Playwright sin cerrar página, contexto ni proceso. Edge queda como navegador normal, sin la política de bloqueo de orígenes, y permanece abierto aunque PAM Automation se cierre. Si el perfil ya conserva una sesión válida, se confirma el dashboard sin reenviar usuario, contraseña o TOTP. La automatización de solicitudes mantiene intacto su contexto temporal y restringido.

Se añadieron pruebas para el comando WPF, errores controlados, sesión ya autenticada, liberación sin cierre de recursos externos y rechazo de sesiones no separables. La compilación `Release | x64` y las 315 pruebas ordinarias pasaron. La validación fue simulada: no se inició una sesión PAM real, no se crearon solicitudes y no se envió correo. **Fase completada.**

Una prueba manual posterior detectó que el perfil dedicado limpio no heredaba la aceptación del certificado HTTPS interno que sí tenía el perfil habitual utilizado por `ABRIR_SESION_PAM.cmd` en la aplicación anterior. El diagnóstico confirmó que Edge y CDP iniciaban correctamente y que el fallo ocurría en el primer `GotoAsync`, antes de enviar credenciales. La fábrica interactiva ahora habilita la excepción mediante una sesión CDP asociada a la página durante el login y restaura la validación normal antes de separar Playwright. La liberación está protegida tanto al completar como ante un fallo de autenticación. La compilación `Release | x64` y las 316 pruebas ordinarias pasaron; la advertencia heredada `MSTEST0032` permanece fuera de esta fase. La corrección no se probó contra PAM real sin una nueva autorización explícita.

## Fase 21.1 — Acceso PAM estable y repetición controlada de Pending

El segundo diagnóstico distinguió el bloqueo real: Edge publicaba `DevToolsActivePort`, pero `ConnectOverCDPAsync` agotaba el tiempo al usar el endpoint HTTP de descubrimiento. La fábrica ahora valida las dos líneas del archivo, construye el WebSocket loopback exacto publicado por Edge, rechaza rutas inseguras o incompletas y reintenta tanto excepciones Playwright como timeouts. Se conserva la excepción temporal del certificado mostrada por Edge, su restablecimiento posterior y la entrega del navegador sin cerrarlo. Una integración real explícitamente autorizada autenticó el dashboard y confirmó que el Edge dedicado permanece abierto tras finalizar la prueba.

La preparación de una ejecución real ahora lee `Pending` antes de presentar confirmaciones y conserva en memoria sus Request ID como línea base. Las filas que coinciden exactamente en `System`, `Account`, familia controlada de `Application`, `Request Start Date`, `Expires` y `Description` se comunican al usuario antes de crear solicitudes. Puede elegir `Sí` para volver a pedirlas o `No` para cancelar sin efectos. Cuando acepta repetir, la reconciliación posterior excluye todos los Request ID observados antes del envío y asocia únicamente las solicitudes nuevas; así las repeticiones permitidas por PAM no producen una ambigüedad falsa.

La compilación `Release | x64` pasó y la suite ordinaria terminó 323/323. La prueba real creó cero solicitudes y envió cero correos. Solo permanece la advertencia heredada `MSTEST0032`. **Fase completada.**

## Fase 21.2 — Edge normal, certificado manual y automatización accesible

La validación operativa mostró que un Edge dedicado iniciado para CDP no equivalía al navegador cotidiano: la conexión podía expirar y los archivos descargados no conservaban el comportamiento normal esperado por el usuario. Se sustituyó esa arquitectura únicamente para `Acceder a PAM`. La acción abre `msedge.exe --new-window` sin depuración remota ni perfil alterno y utiliza Windows UI Automation para reconocer la barra de direcciones y los cinco controles del login. El perfil normal conserva sus descargas, asociaciones, cookies y sesión; la aplicación libera sus referencias sin cerrar Edge.

La aplicación nunca acepta silenciosamente el certificado del navegador. Si reconoce `Su conexión no es privada`, explica `Avanzado` y `Continuar a 10.30.17.117`, espera la decisión manual hasta que aparezca `Username` y recién entonces continúa con contraseña y TOTP. Una petición auxiliar acepta el certificado solo para el origen PAM y únicamente para leer el encabezado `Date`; no modifica la confianza del navegador. La detección y el uso de foco son tolerantes a cambios transitorios del árbol accesible, y todos los timeouts se convierten en fallos controlados. El flujo de solicitudes conserva sin cambios su Edge temporal de Playwright, bloqueo de orígenes y ciclo de vida aislado.

La compilación `Release | x64` pasó, la suite ordinaria terminó 321/321 y una integración real autorizada confirmó el dashboard mediante el Edge normal, que permaneció abierto. La prueba creó cero solicitudes y envió cero correos. Solo permanece la advertencia heredada `MSTEST0032`. **Fase completada.**

## Fase 21.3 — Zoom de PAM al 67 %

`Acceder a PAM` ajusta únicamente la ventana autenticada que acaba de abrir. Después de validar el origen y el dashboard, enfoca la barra de direcciones, confirma que ese elemento recibió el foco, ejecuta `Ctrl+0` y cuatro reducciones, y reconoce `67 %` tanto si Edge lo expone como texto, grupo o botón. El ajuste se intenta una sola vez y un fallo queda registrado de forma controlada sin cerrar la sesión ya iniciada ni enviar teclas cuando el foco es incierto.

Como ajuste de jerarquía visual, `Acceder a PAM` se trasladó al encabezado entre `Exportar diagnóstico` y el toggle `Enviar correo con Outlook`; su comando, disponibilidad y comportamiento no cambiaron.

La compilación `Release | x64` pasó y una integración real autorizada exigió y confirmó programáticamente el indicador `67 %`. Edge permaneció abierto; no se crearon solicitudes ni se envió correo. **Fase completada.**

## Fase 21.4 — Automatización de solicitudes oculta

El contexto temporal utilizado por `PamBrowserSessionFactory` cambió de Edge visible a `Headless = true`. Por tanto, autenticación, lectura de `Favorites`, apertura y envío de formularios y reconciliación de `Pending` se ejecutan en segundo plano sin mostrar al usuario el recorrido del portal. Se conservan el canal externo `msedge`, el perfil temporal, el bloqueo de orígenes, la excepción HTTPS limitada, los selectores y la liberación completa de recursos.

La modificación no alcanza a `PamInteractiveBrowserSessionFactory`: `Acceder a PAM` continúa abriendo una ventana normal y visible, permite la intervención manual del certificado, mantiene la sesión y aplica el zoom al 67 %. La compilación `Release | x64`, las 12 pruebas de la fábrica y un login real no destructivo en modo oculto pasaron. La integración no abrió `Favorites`, no creó solicitudes y no ejecutó Outlook. **Fase completada.**

## Fase 21.5 — Confirmación inmediata y única

La confirmación general de una ejecución real se trasladó al inicio de `StartAutomationAsync`, antes de `PrepareRealAsync` y de cualquier acceso a PAM. El diálogo utiliza la fecha, hora, duración, razón y acción final ya disponibles localmente; ya no espera a que finalicen las lecturas de `Favorites` y `Pending`, ni muestra una segunda confirmación general después de esas lecturas. Si el usuario responde `No`, la ejecución termina sin iniciar validaciones remotas ni crear solicitudes.

Después de responder `Sí`, la preparación, las comprobaciones y el envío continúan automáticamente en segundo plano. Solo se vuelve a consultar al usuario cuando la ejecución necesita una decisión nueva para continuar, actualmente la existencia de solicitudes exactamente iguales en `Pending`; los errores y condiciones bloqueantes se comunican, pero no se convierten en confirmaciones redundantes. Las 22 pruebas del ViewModel verifican cancelación antes de preparar, una sola confirmación inicial y conservación del aviso imprescindible de duplicados. **Fase completada.**

## Fase 21.6 — Resumen previo claro y solicitud simplificada

La confirmación inicial presenta la hora configurada en el mismo formato de 12 horas que eligió el usuario, incluyendo `AM` o `PM`. Cuando la acción final es Outlook, identifica por separado los destinatarios `Para` y `CC`; si no se configuró copia, muestra `CC: Ninguno` para evitar ambigüedad.

`Configurar solicitud` dejó de mostrar el apartado `Habilitar modo de prueba`. El valor interno se conserva al editar para mantener compatibilidad con configuraciones de desarrollo existentes. La fase 20.4 retiró posteriormente la razón de esta pantalla, que ahora se edita en la colección activa. **Fase completada.**

## Fase 21.7 — Avisos urgentes en primer plano

Se creó `UrgentMessageBox` como único punto de presentación para los cuadros importantes. Utiliza el propietario WPF activo y las opciones nativas de Windows `task-modal`, `topmost` y `set-foreground`; además restaura la ventana propietaria cuando estaba minimizada. De esta forma una confirmación necesaria aparece sobre las aplicaciones normales del escritorio y detiene el flujo hasta que el usuario responda.

La confirmación inicial, las solicitudes repetidas de `Pending`, la intervención manual por certificado, las confirmaciones de correo, los errores, los resultados solicitados y los avisos internos de configuración utilizan este mecanismo. Los banners no bloqueantes, como actualizaciones disponibles, permanecen sin interrupciones. La compilación `Release | x64` y 7 pruebas específicas pasaron; no se contactó PAM ni Outlook. **Fase completada.**

## Fase 21.8 — Progreso por Application y acceso PAM concurrente

`PamRequestSubmissionService` informa un `PamRequestSubmissionProgress` después de cada resultado conocido. La ventana conserva `Validando Favorites y Pending en segundo plano…` hasta que PAM acepta la primera solicitud; a partir de ahí muestra `Se solicitó {Application}.` con el valor exacto de la columna `Application`. Los solapamientos con accesos aprobados no se presentan como solicitudes creadas.

`Acceder a PAM` permanece habilitado durante una automatización real porque abre el Edge normal del usuario, separado del contexto temporal y oculto de solicitudes. Continúa bloqueado si ya hay otra apertura interactiva o se está exportando un diagnóstico. Cuando ambos flujos coinciden, el acceso interactivo no reemplaza el progreso ni el resultado de la automatización, y sus fallos se notifican de forma urgente e independiente. La compilación `Release | x64` y 39 pruebas específicas pasaron; no se contactó PAM ni Outlook. **Fase completada.**

## Fase 21.9 — Vencimiento automático de la hora configurada

La ventana principal ejecuta una comprobación local cada minuto mediante `DispatcherTimer`. Si la fecha u hora configurada pasa al pasado mientras la aplicación permanece abierta, actualiza `IsRequestConfigured`, deshabilita `Iniciar automatización` y muestra el aviso que dirige a `Configurar solicitud`. El temporizador se detiene al cerrar la ventana y nunca contacta PAM ni Outlook.

El comando valida nuevamente la configuración justo antes de mostrar la confirmación. También repite esa validación al responder `Sí`, de modo que un cuadro abierto durante el vencimiento se cancela con un mensaje claro antes de autenticar, leer `Favorites` o crear solicitudes. La compilación `Release | x64` y 32 pruebas específicas pasaron; no se contactó PAM ni Outlook. **Fase completada.**

## Fase 21.10 — Margen operativo mínimo de 6 minutos

`RequestSettingsValidator` exige que el inicio configurado esté al menos 6 minutos después de la hora actual de Ecuador. Exactamente 6 minutos es válido; cualquier margen inferior produce `StartTimeTooClose`. Al ser una regla central, se aplica al guardar, al refresco local de cada minuto, antes y después de la confirmación, al preparar la ejecución y nuevamente antes del primer envío. El botón se deshabilita con un mensaje que explica el margen requerido.

El diagnóstico `pam-automation-diagnostic-20260913-015429` mostró además que PAM devolvió una respuesta HTTP no exitosa cuyo JSON no tenía un objeto en la raíz. `PamSubmissionResponseClassifier` ahora rechaza de forma controlada objetos, arreglos, texto, números o `null` no reconocidos, sin permitir que `TryGetProperty` produzca una excepción técnica. La compilación `Release | x64`, las 51 pruebas específicas y la suite ordinaria completa de 337 pruebas pasaron; no se contactó PAM ni Outlook. **Fase completada.**

## Fase 20.1 — Catálogo dinámico de solicitudes favoritas

La portada incorpora un botón propio `Colecciones` que abre una ventana WPF inicialmente vacía. La primera acción se llama `Obtener solicitudes favoritas`; utiliza las credenciales protegidas, autentica mediante el contexto temporal oculto y reutiliza el lector paginado para obtener todas las `Favorites` rows. Cada elemento se presenta únicamente como `Application - IP Address`, mientras Core conserva la identidad exacta `System + Application + Account` para las colecciones posteriores.

El snapshot se guarda en `%LOCALAPPDATA%\PamAutomation\config\favorite-catalog.dat` con `schemaVersion = 1`, cifrado DPAPI `CurrentUser` y reemplazo atómico. Al existir un snapshot, incluso después de reiniciar, la acción cambia a `Actualizar solicitudes favoritas`. Cada actualización reemplaza el catálogo completo y calcula cuántas identidades aparecieron o desaparecieron; este contrato permitirá que la fase siguiente retire de las colecciones las claves exactas que ya no pertenezcan a `Favorites`, sin incorporar automáticamente filas nuevas.

La lectura continúa siendo no destructiva: no abre formularios, no consulta `Pending`, no crea solicitudes y no ejecuta Outlook. La primera validación desde la interfaz alcanzó `Favorites`, pero agotó el tiempo de estabilización porque el selector nuevo supuso `IPAddress`; una inspección real limitada a los nombres técnicos de columna confirmó que el portal representa el encabezado visible `IP Address` como `data-column-id="SystemIP"`. El lector usa ahora ese contrato real y conserva como causa interna el nombre seguro de cualquier columna ausente. Tras la corrección pasaron la compilación `Release | x64`, las 350 pruebas ordinarias y una prueba real explícita de solo lectura que exigió valores no vacíos para `System`, `Application`, `Account` e `IP Address`. **Fase completada y validada.**

## Fase 20.2 — Creación, edición y selección de colecciones

La ventana `Colecciones` muestra varias definiciones guardadas y permite crear o editar cada una. Tras el ajuste de fase 20.4, el editor reúne nombre, selección múltiple de `Favorites` mediante `Application - IP Address` y razón. Un toggle por fila selecciona de manera exclusiva una colección para automatización; seleccionar otra sustituye la anterior y desactivar la activa limpia la selección.

Las definiciones se guardan en `favorite-collections.dat` bajo la configuración privada del usuario con DPAPI `CurrentUser` y escritura atómica. La fase 20.4 elevó el documento a `schemaVersion = 2`. Al actualizar Favorites se retiran automáticamente de las colecciones personalizadas las claves exactas ausentes; las nuevas no se agregan solas. **Fase completada.**

## Fase 20.3 — Ejecución restringida a la colección activa

Esta fase introdujo la ejecución restringida a identidades de colección y originalmente añadió un toggle de modo. La fase 20.4 retiró ese toggle y convirtió la colección activa en el único alcance. La selección se valida contra una lectura viva antes de `Pending` y vuelve a comprobarse dentro de la sesión de envío mediante la autorización de un solo uso. Cualquier identidad retirada o cambio de total detiene el flujo antes de `Submit Request`; la reconciliación posterior continúa usando únicamente las cuentas enviadas.

La compilación `Release | x64` y las 363 pruebas ordinarias pasaron. Las pruebas cubren persistencia DPAPI, exclusividad, reconciliación, cambio de modo, configuración efectiva, confirmación explícita del alcance y filtrado exacto en el límite de envío. No se crearon solicitudes ni se envió correo durante la validación. **Implementación completada; prueba real de creación de solicitudes con una colección pendiente de autorización futura.**

## Fase 20.4 — Colecciones como único alcance de automatización

Se eliminó el modo alternativo y el toggle global de la portada: toda automatización utiliza obligatoriamente la colección activa. Al obtener Favorites se crea y activa automáticamente la colección reservada `All favorites`; cada actualización la sincroniza con el snapshot completo. El usuario puede editar únicamente su razón, mientras nombre y solicitudes quedan administrados por la aplicación. Las colecciones personalizadas conservan nombre, razón y selección.

`Editar solicitud` dejó de editar la razón y ahora solo configura fecha, hora y duración. En el límite de ejecución esos valores se combinan con el motivo de la colección activa; el envío continúa limitado por autorización de un uso a sus claves exactas. `favorite-collections.dat` usa `schemaVersion = 2` y migra el esquema anterior conservando nombres, motivos, selecciones y colección activa. La compilación `Release | x64` y las 368 pruebas ordinarias pasaron; no se contactó PAM, no se crearon solicitudes y no se envió correo. **Fase completada.**

## Fase 20.5 — Eliminación protegida de colecciones personalizadas

Cada colección creada por el usuario muestra un botón rojo con el icono nativo de papelera. El borrado requiere confirmación explícita y aclara que solo elimina la definición local, sin modificar `Favorites` en PAM. `All favorites` oculta el botón y Core rechaza además cualquier intento de eliminarla fuera de la interfaz.

Si la colección eliminada estaba activa, la aplicación selecciona automáticamente `All favorites`; si estaba inactiva, conserva la selección existente. La operación utiliza el almacén DPAPI y la escritura atómica actuales sin cambiar `schemaVersion`. La compilación `Release | x64` y las 374 pruebas ordinarias pasaron; no se contactó PAM, no se crearon solicitudes y no se envió correo. **Fase completada.**

## Ajuste posterior a la fase 20 — Normalización temporal al iniciar

Al inicializar la aplicación, una fecha y hora guardadas que ya estén en el pasado se reemplazan una sola vez por la primera hora válida de Ecuador: hora actual más 6 minutos, redondeada hacia arriba al minuto completo. El valor corregido se persiste para que `Editar solicitud` y la confirmación muestren la misma configuración. Las horas futuras no se reescriben y el refresco periódico posterior conserva su responsabilidad de deshabilitar y explicar, no de cambiar automáticamente la selección del usuario.

La compilación `Release | x64` y las 369 pruebas ordinarias pasaron. No se contactó PAM, no se crearon solicitudes y no se envió correo. **Ajuste completado.**

## Ajuste posterior a la fase 20 — Identidad visual y versión 1.0.1

El monograma circular `PA` con transparencia exterior es el icono oficial de Windows. Un `.ico` con tamaños de 16 a 256 píxeles queda incrustado en el ejecutable para Explorer, barra de tareas y ventanas. La portada conserva por separado la insignia azul redondeada original y cambia únicamente su texto de `P` a `PA`. La política DWM común configura las barras de título en gris oscuro con texto claro en Windows compatible, sin sustituir los controles nativos ni alterar su comportamiento.

La metadata de producto, archivo, ensamblado e información avanzó de forma coherente a `1.0.1`; `Publish-Portable.ps1` queda listo para producir `artifacts\publish\final\PAM-Automation-1.0.1\PAM Automation.exe`. La compilación `Release | x64` y las 369 pruebas ordinarias pasaron, y se comprobó que Windows extrae un icono del binario y reporta `ProductVersion 1.0.1` y `FileVersion 1.0.1.0`. **Ajuste completado.**

## Fase 22 — Arranque optimizado y paquete ZIP autocontenido

La publicación dejó de concentrar .NET, WPF, Playwright y las bibliotecas nativas en un único EXE de aproximadamente 248 MB. `Portable-x64` produce ahora una carpeta autocontenida multifichero y `Publish-Portable.ps1` la empaqueta automáticamente como `PAM-Automation-1.0.2-win-x64.zip`, conservando la carpeta base. El usuario no necesita instalar .NET, pero debe extraer y mantener juntos todos los archivos. Esta decisión evita la extracción del bundle en cada arranque y reduce el trabajo previo a que WPF pueda aparecer.

La ventana entra en un estado explícito `Preparando PAM Automation…`, mantiene sus acciones deshabilitadas hasta cargar la configuración y se recupera incluso si la inicialización falla. El diagnóstico seguro admite `ElapsedMilliseconds` y registra hitos de inicio sin datos privados. La consulta de actualización comienza después de que la configuración esté disponible y ya no define el final del arranque funcional.

La versión avanzó a `1.0.2`. La compilación `Release | x64`, 44 pruebas específicas y las 377 pruebas ordinarias iniciales pasaron; tras incorporar la recuperación de Outlook, la suite final alcanzó 383 pruebas. El publicador produjo 523 archivos, un ZIP final de 105.062.496 bytes y SHA-256 `5D141DD2EDDD0C5DB67F497819B39443ADD067BB0C32A0C2951383693E10758D`. En una prueba local visible, la ventana 1.0.2 apareció receptiva en aproximadamente 4,9 segundos; la publicación 1.0.1 de archivo único todavía no había mostrado ventana dentro del intervalo comparativo. No se abrió PAM, no se crearon solicitudes y no se envió correo. **Fase completada; falta confirmar el arranque frío en el equipo corporativo afectado.**

## Ajuste posterior a la fase 22 — Recuperación controlada de Outlook COM

El diagnóstico `pam-automation-diagnostic-20260914-180710` confirmó dos fallos de aproximadamente 31 segundos en `ComApplicationCreate` con `HRESULT 0x80080005`; la cuenta SMTP nunca llegó a enumerarse. La ruta propuesta bajo `ProgramData\Microsoft\Windows\Start Menu` se descartó porque contiene accesos directos y no resuelve una falla del servidor COM.

El adaptador intenta ahora conectar con la instancia de Outlook clásico ya abierta. Si no existe, conserva la activación por `ProgID`; únicamente ante `CO_E_SERVER_EXEC_FAILURE` inicia `outlook.exe` mediante el registro de aplicaciones de Windows, espera tres segundos en el hilo STA y reintenta una vez. La recuperación no hardcodea rutas, no mata procesos, no eleva permisos y produce `ApplicationUnavailable` con orientación específica si Outlook continúa sin responder.

La compilación `Release | x64`, 30 pruebas específicas y las 383 pruebas ordinarias pasaron. Las pruebas cubren instancia activa, activación normal, recuperación, HRESULT no relacionado, agotamiento controlado y mensaje al usuario. No se abrió Outlook real ni se envió correo porque esta ejecución no tenía autorización explícita para una prueba COM real. **Ajuste implementado; validación real pendiente en el equipo que produjo el diagnóstico.**

## Ajuste posterior a la fase 22 — Confirmación tolerante de correo

El diagnóstico `pam-automation-diagnostic-20260914-211847` demostró que Outlook había aceptado el correo final, pero la aplicación consultó Bandeja de salida casi inmediatamente y movió el mensaje a Borradores. Esa acción podía impedir el envío precisamente en equipos o conexiones más lentos.

La reconciliación posterior a `Send()` es ahora observacional: espera cuatro segundos y consulta hasta diez veces Enviados, Bandeja de salida y Borradores. Las lecturas COM transitorias se reintentan dentro de la misma ventana acotada. Un mensaje que continúa en Bandeja de salida se deja intacto y se informa como pendiente; la protección previa reconoce ese estado para no crear duplicados. Nunca se reintenta automáticamente `Send()` cuando su resultado externo es incierto.

La compilación `Release | x64`, 26 pruebas específicas iniciales y la suite ordinaria completa final de 387 pruebas pasaron. No se abrió Outlook real ni se envió correo durante la validación automática. **Ajuste implementado; validación real pendiente en el equipo que presentó la intermitencia.**

## Fase 23 — Usuario principal y cuentas secundarias de acceso PAM

La fase 23 reemplaza por completo el diseño anterior de perfiles aislados y portables. La aplicación conservará una sola configuración funcional para automatización, solicitudes, colecciones y Outlook. Esa configuración pertenecerá al **usuario principal**. Las cuentas secundarias existirán exclusivamente para abrir una sesión interactiva mediante `Acceder a PAM`; no tendrán colecciones, configuración de solicitud, correo, Outlook, resultados ni capacidad para ejecutar solicitudes automáticas.

El término **usuario principal** identifica al propietario funcional de la instalación. Su rol es permanente: no se puede eliminar, sustituir por una cuenta secundaria ni convertir en secundario. Su nombre visible sí podrá editarse. El término **cuenta PAM secundaria** identifica únicamente un nombre descriptivo y un juego propio de usuario, contraseña y semilla TOTP protegidos.

### Fase 23.1 — Primer inicio e identidad principal

En el primer inicio, si todavía no existe identidad principal, la aplicación deberá bloquear el acceso a la ventana principal hasta que el usuario elija un nombre válido. La creación no solicitará nuevamente las credenciales que ya se gestionan desde `Editar credenciales`; solo registrará el nombre y marcará de forma permanente esa identidad como principal.

En los inicios posteriores no se volverá a mostrar este asistente. La ventana principal continuará cargando la configuración única existente. `Editar credenciales`, `Editar solicitud`, `Colecciones`, `Editar envío de correo`, el toggle de Outlook y `Iniciar automatización` siempre operarán sobre el usuario principal, sin importar qué cuenta se haya utilizado recientemente para abrir PAM.

El nombre principal será obligatorio, normalizado, tendrá un límite explícito y no contendrá caracteres de control. Su registro no incluirá secretos y se guardará con esquema versionado y escritura atómica bajo `%LOCALAPPDATA%\PamAutomation\config`. La transición desde una instalación existente creará el usuario principal sin mover ni reinterpretar la configuración actual.

La aplicación implementa este arranque mediante `EnsurePrincipalIdentityAsync`: antes de mostrar la portada carga el registro protegido y, si está vacío, abre un diálogo modal obligatorio para crear el principal. Cancelar ese diálogo cierra la aplicación de forma segura. El registro conserva únicamente GUID, nombre normalizado y rol; las credenciales, colecciones, correo y demás configuración existente permanecen en sus almacenes actuales. **Fase completada.**

### Fase 23.2 — Administración de cuentas PAM

La portada incorporará un botón `Usuarios PAM`. Abrirá una ventana que mostrará primero al usuario principal, claramente identificado. Desde esa fila solo podrá editarse el nombre visible; no habrá acción de eliminación ni cambio de rol. La misma ventana permitirá crear, editar y eliminar cuentas secundarias.

Crear una cuenta secundaria solicitará un nombre descriptivo, usuario PAM, contraseña PAM y semilla TOTP. La primera configuración exigirá los cuatro valores. Al editar, los secretos vacíos conservarán su valor actual y nunca se rellenarán nuevamente en pantalla. Los nombres visibles no podrán repetirse sin distinguir mayúsculas y minúsculas. El borrado exigirá confirmación y eliminará únicamente el registro y la entrada exacta de Credential Manager de esa cuenta.

Se conservará el límite previamente acordado de cinco identidades en total: un usuario principal y hasta cuatro cuentas secundarias. La interfaz explicará el límite antes de permitir otra creación.

El índice no sensible de cuentas utilizará identificadores GUID; los nombres nunca se usarán como claves de almacenamiento. Cada juego de credenciales se guardará en Windows Credential Manager bajo un destino independiente derivado del GUID. Ningún usuario, contraseña, semilla TOTP o código generado se escribirá en archivos, logs, diagnósticos o mensajes de error. Las mutaciones del índice y Credential Manager deberán ser recuperables: un fallo no podrá dejar una cuenta visible sin credencial completa ni eliminar una credencial que el índice todavía necesite.

Las credenciales que actualmente usa la automatización conservarán su destino y pasarán a representar al usuario principal. Las cuentas secundarias no podrán reemplazar ese destino ni suministrarse al servicio de creación de solicitudes.

La portada ya incorpora `Usuarios PAM`. La ventana de administración permite renombrar al principal y crear, editar o eliminar hasta cuatro cuentas secundarias. La edición secundaria deja intactos los secretos cuyos campos se envían vacíos; el borrado exige confirmación. El índice se protege con DPAPI CurrentUser en `pam-accounts.dat`, usa escritura atómica y GUID estables. Cada secundaria utiliza un destino independiente de Credential Manager, mientras el principal reutiliza `PamAutomation/PamCredentials/v1`. El servicio valida la semilla TOTP y revierte el índice o la credencial cuando una mutación coordinada falla. **Fase completada.**

### Fase 23.3 — Selección de cuenta al acceder a PAM

Al pulsar `Acceder a PAM`, la aplicación mostrará siempre una selección breve para que el usuario confirme con qué identidad desea iniciar sesión, incluso cuando solo exista la principal. Cada opción se identificará únicamente por su nombre descriptivo y marcará cuál es el usuario principal; jamás mostrará el usuario técnico, contraseña o semilla.

Después de elegir, el flujo interactivo actual recibirá exclusivamente las credenciales de esa identidad, generará su TOTP en memoria e iniciará sesión en el Edge normal del usuario. Se conservarán el zoom al 67 %, la posible intervención manual ante el certificado corporativo, la permanencia de la ventana de Edge y la independencia respecto del navegador oculto de solicitudes.

Elegir una cuenta secundaria para `Acceder a PAM` no cambia el usuario activo de la aplicación ni afecta la siguiente automatización. `Iniciar automatización` utilizará siempre y únicamente las credenciales del usuario principal. La eliminación o edición de una cuenta secundaria no podrá cambiar la configuración de solicitudes, Favorites, colecciones u Outlook.

La disponibilidad de ambas acciones se calculará por separado. `Iniciar automatización` permanecerá deshabilitado si el principal no tiene credenciales completas. `Acceder a PAM` podrá habilitarse cuando exista al menos una identidad con credenciales completas; una identidad todavía sin configurar aparecerá claramente como pendiente y no podrá seleccionarse para iniciar sesión.

### Fase 23.4 — Migración, seguridad y aceptación

La fase incorporará una migración idempotente desde la configuración actual: conservará las credenciales PAM existentes como credenciales del principal, solicitará únicamente el nombre faltante y no tocará correo, cuenta Outlook, plantillas, Favorites, colecciones ni configuración temporal. Un fallo mantendrá utilizable el estado anterior y permitirá reintentar.

Las pruebas cubrirán primer inicio, cancelación obligatoria segura, nombre principal editable, imposibilidad de eliminar o degradar al principal, altas y modificaciones parciales de cuentas, nombres repetidos, eliminación con rollback, aislamiento por GUID y selección correcta en `Acceder a PAM`. También demostrarán que una cuenta secundaria nunca llega a la automatización de solicitudes y que los secretos no aparecen en archivos, interfaz, logs o diagnóstico.

Quedan expresamente fuera de alcance la importación y exportación de perfiles o cuentas, el intercambio de credenciales entre computadoras, configuraciones distintas de correo o colecciones por cuenta y la selección de una cuenta secundaria para crear solicitudes. La fase se considerará completa solo después de compilar y aprobar la suite en `Release | x64` y validar visualmente los recorridos sin realizar solicitudes PAM ni enviar correos. **Estado: planificada.**

## Fases pendientes

La clasificación siguiente se refiere únicamente a dependencias entre fases pendientes:

- **Independiente:** no necesita que termine otra fase pendiente y puede desarrollarse en paralelo en módulos separados.
- **Independiente condicionada:** puede avanzar en paralelo solo después de que el coordinador fije el contrato compartido, asigne archivos sin solapamiento y limite el alcance indicado.
- **Dependiente:** necesita resultados integrados de las fases señaladas o comparte una superficie de cambio que hace más segura la ejecución secuencial.

| Fase | Clasificación | Dependencias pendientes | Alcance y posibilidad de paralelismo |
|---|---|---|---|
| 19 — Pruebas finales administradas | **Independiente** | Ninguna fase pendiente. | Es la validación final del producto publicado en el entorno Windows objetivo. |
| 20.6+ — Administración ampliada de colecciones | **Dependiente** | Fases 20.2–20.5 implementadas. | Podrá añadir duplicación u otras mejoras que el usuario confirme; no son necesarias para crear, editar, eliminar, seleccionar y ejecutar una colección. |
| 23.3 — Selección de cuenta al acceder a PAM | **Dependiente** | Fases 23.1–23.2 completadas. | Conectará las identidades disponibles exclusivamente con el flujo interactivo `Acceder a PAM`. |
| 23.4 — Migración, seguridad y aceptación | **Dependiente** | Fase 23.3. | Cerrará la migración, el aislamiento entre flujos y la aceptación visual de la fase. |

## Mapa recomendado de trabajo paralelo

```text
Fases 6 a 18 completadas ──► Fase 19

Fases 21–21.10 completadas de forma independiente

Fases 20.1–20.5 completadas ──► validación real futura

Fases 20–22 completadas ──► Fase 23.1 completada ──► 23.2 completada ──► 23.3 ──► 23.4
```

La fase 18 quedó completada tras validar el manifiesto GitHub real. El coordinador solo podrá paralelizar una parte futura si vuelve a demostrar independencia, contratos congelados y ausencia de archivos compartidos.

La fase 20 registra la ampliación solicitada después de la migración. Las fases 20.1–20.5 implementan el catálogo protegido, las colecciones nombradas y eliminables, `All favorites` administrada y la colección activa como único alcance de ejecución. Su diseño, navegación, reconciliación y seguridad se detallan en `PAM_NAVIGATION.md`; ninguna de estas fases hereda autorización para crear solicitudes reales.

La fase 23 no reutiliza el diseño descartado de perfiles completos. Las fases 23.1 y 23.2 ya incorporan la identidad principal y la administración segura de cuentas. La configuración funcional sigue siendo única; conectar las identidades adicionales al botón interactivo `Acceder a PAM` corresponde a la fase 23.3.

Esta clasificación es una guía de planificación, no una autorización automática para crear subagentes. Antes de cada oleada, el coordinador debe inspeccionar el diff y la estructura real; si el trabajo resultó pequeño o apareció solapamiento, debe conservarlo en el agente principal o ejecutarlo secuencialmente.

Cada fase deberá restaurar dependencias cuando corresponda, compilar en `Release | x64` y ejecutar sus pruebas relacionadas desde la terminal antes de considerarse completa.
