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

La protección de correo combina una huella en memoria con una propiedad privada de Outlook que representa el asunto y el conjunto exacto de Request ID. Antes y después de enviar se consultan Enviados, Bandeja de salida y Borradores; un duplicado no se vuelve a crear y un mensaje idéntico en Bandeja de salida se mueve a Borradores. El resultado muestra el estado de entrega sin afirmar confirmación cuando Outlook no puede demostrarla. Los fallos parciales de PAM muestran cuántas `Favorites` rows alcanzaron un resultado conocido y advierten revisar `Pending` antes de reintentar.

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

## Fases pendientes

La clasificación siguiente se refiere únicamente a dependencias entre fases pendientes:

- **Independiente:** no necesita que termine otra fase pendiente y puede desarrollarse en paralelo en módulos separados.
- **Independiente condicionada:** puede avanzar en paralelo solo después de que el coordinador fije el contrato compartido, asigne archivos sin solapamiento y limite el alcance indicado.
- **Dependiente:** necesita resultados integrados de las fases señaladas o comparte una superficie de cambio que hace más segura la ejecución secuencial.

| Fase | Clasificación | Dependencias pendientes | Alcance y posibilidad de paralelismo |
|---|---|---|---|
| 17 — Evidencias técnicas de desarrollo | **Dependiente** | Fases 16 a 16.8, con flujos operativos, logging, Outlook y estados visuales cerrados. | Completará la exportación explícita y revisable de evidencias a partir del diagnóstico seguro; no conviene paralelizarla con cambios activos en esos flujos. |
| 18 — Instalador y publicación autocontenida | **Dependiente** | Fases 6 a 17. | Requiere que la aplicación y sus dependencias externas estén definidas y validadas. |
| 19 — Pruebas finales administradas | **Dependiente** | Fase 18. | Es la validación final del producto publicado en el entorno Windows objetivo. |
| 20 — Colecciones personalizadas post-migración | **Dependiente** | Migración inicial y fase 19 completadas. | Permitirá seleccionar subconjuntos editables de `Favorites` rows, guardar configuración general y posibles sobrescrituras por fila. Es alcance nuevo y no debe mezclarse con las fases 12 a 19. |

## Mapa recomendado de trabajo paralelo

```text
Fases 6 a 16.8 completadas ──► Fase 17 ──► Fase 18 ──► Fase 19

Fase 19 ──► Fase 20 (ampliación posterior a la migración)
```

La fase 17 constituye el siguiente paso y depende de los flujos integrados, la configuración, el logging seguro, la corrección COM y los estados visuales cerrados en las fases 16 a 16.8. El coordinador solo podrá paralelizar una parte futura si vuelve a demostrar independencia, contratos congelados y ausencia de archivos compartidos.

La fase 20 registra una ampliación solicitada para después de terminar la migración: colecciones locales nombradas y editables que seleccionan cualquier subconjunto de Favorites. Su diseño preliminar, navegación, reconciliación y seguridad se detallan en `PAM_NAVIGATION.md`; no modifica el alcance ni la autorización de la fase 12.

Esta clasificación es una guía de planificación, no una autorización automática para crear subagentes. Antes de cada oleada, el coordinador debe inspeccionar el diff y la estructura real; si el trabajo resultó pequeño o apareció solapamiento, debe conservarlo en el agente principal o ejecutarlo secuencialmente.

Cada fase deberá restaurar dependencias cuando corresponda, compilar en `Release | x64` y ejecutar sus pruebas relacionadas desde la terminal antes de considerarse completa.
